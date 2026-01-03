using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace JiraInsight.Services
{
    public class jiraanalyzer
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public jiraanalyzer(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<string> AnalyzeAsync(string jiraJson)
        {
            if (string.IsNullOrWhiteSpace(jiraJson))
                return "Ingen Jira-data mottogs.";

            var apiKey = _configuration["OpenAI:ApiKey"];
            var model = _configuration["OpenAI:Model"] ?? "gpt-4.1-mini";

            if (string.IsNullOrWhiteSpace(apiKey))
                return "OpenAI API-nyckel saknas i konfigurationen.";

            var prompt = $"""
You are a senior agile delivery expert with 20+ years of experience.

Analyze the Jira export below and answer clearly:

1. Can delivery performance be assessed?
2. What does the data clearly show?
3. What cannot be concluded due to data quality?
4. 2–3 concrete recommendations.

Be honest. If the data is insufficient, say so explicitly.
Avoid vanity metrics and dashboards.

Jira export:
{jiraJson}
""";

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            var requestBody = new
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
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                )
            );

            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return $"OpenAI API error: {response.StatusCode}\n{responseJson}";
            }

            using var doc = JsonDocument.Parse(responseJson);

            var analysis =
                doc.RootElement
                   .GetProperty("choices")[0]
                   .GetProperty("message")
                   .GetProperty("content")
                   .GetString();

            return analysis ?? "Ingen analys kunde genereras.";
        }
    }
}