using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace JiraInsight.Services
{
    public class JiraExportService
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public JiraExportService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<string> GenerateAsync(
            string email,
            string apiToken,
            string baseUrl,
            string projectKey)
        {
            var authToken = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{email}:{apiToken}")
            );

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", authToken);
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json")
            );

            var body = new
            {
                jql = $"project = {projectKey}",
                maxResults = 100,
                fields = new[]
    {
        "summary",
        "issuetype",
        "status",
        "assignee",
        "reporter",
        "priority",
        "created",
        "updated",
        "resolutiondate",
        "labels",
        "components",
        "fixVersions",
        "duedate"
    }
            };


            var response = await client.PostAsync(
                $"{baseUrl}/rest/api/3/search/jql",
                new StringContent(
                    JsonSerializer.Serialize(body),
                    Encoding.UTF8,
                    "application/json"
                )
            );

            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(
                    $"Jira API error: {response.StatusCode}\n{json}"
                );
            }

            // För MVP: returnera rå JSON
            return json;
        }
    }
}
