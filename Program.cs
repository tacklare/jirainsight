using jira2;
using JiraInsight.Services;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddHttpClient();
builder.Services.AddScoped<jiraanalyzer>();
builder.Services.AddScoped<JiraExportService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.UseStaticFiles();

app.MapRazorPages();


app.MapPost("/analyze/jira", async (
    HttpRequest request,
    IHttpClientFactory httpClientFactory,
    IConfiguration config) =>
{
    using var reader = new StreamReader(request.Body);
    var jiraJson = await reader.ReadToEndAsync();

    if (string.IsNullOrWhiteSpace(jiraJson))
        return Results.BadRequest("Empty payload");

    var apiKey = config["OpenAI:ApiKey"];
    var model = config["OpenAI:Model"];

    if (string.IsNullOrWhiteSpace(apiKey))
        return Results.Problem("OpenAI API key missing");

    var prompt = $"""
You are a senior agile delivery expert with 20+ years of experience.

Analyze the Jira export below and answer:
- Can delivery performance be assessed?
- What does the data clearly show?
- What cannot be concluded due to data quality?
- Give 2–3 concrete recommendations.

Be honest and explicit when data is insufficient.
Avoid dashboards and vanity metrics.

Jira export:
{jiraJson}
""";

    var client = httpClientFactory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

    var body = new
    {
        model = model,
        messages = new[]
        {
            new { role = "system", content = "You are an expert agile delivery analyst." },
            new { role = "user", content = prompt }
        },
        temperature = 0.2
    };

    var response = await client.PostAsync(
        "https://api.openai.com/v1/chat/completions",
        new StringContent(
            System.Text.Json.JsonSerializer.Serialize(body),
            System.Text.Encoding.UTF8,
            "application/json"
        )
    );

    var resultJson = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
        return Results.Problem(resultJson);

    using var doc = System.Text.Json.JsonDocument.Parse(resultJson);

    var analysis =
        doc.RootElement
           .GetProperty("choices")[0]
           .GetProperty("message")
           .GetProperty("content")
           .GetString();

    return Results.Ok(new
    {
        generatedAt = DateTime.UtcNow,
        analysis
    });
});


app.Run();
