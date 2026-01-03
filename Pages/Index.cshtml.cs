using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using JiraInsight.Services;

namespace JiraInsight.Pages
{
    public class IndexModel : PageModel
    {
        private readonly JiraExportService _jiraExportService;
        private readonly jiraanalyzer _jiraAnalyzer;
        private readonly IWebHostEnvironment _environment;

        public IndexModel(
            JiraExportService jiraExportService,
            jiraanalyzer jiraAnalyzer,
            IWebHostEnvironment environment)
        {
            _jiraExportService = jiraExportService;
            _jiraAnalyzer = jiraAnalyzer;
            _environment = environment;
        }

        // =========================
        // INPUT (STEG 1)
        // =========================
        [BindProperty] public string JiraEmail { get; set; } = "";
        [BindProperty] public string JiraApiToken { get; set; } = "";
        [BindProperty] public string JiraBaseUrl { get; set; } = "";
        [BindProperty] public string ProjectKey { get; set; } = "";

        // =========================
        // STATE
        // =========================
        public bool Step1Completed { get; set; }
        [TempData]
        public string? ExportFilePath { get; set; }
        public string? AnalysisResult { get; set; }
        public string? Error { get; set; }

        // =========================
        // STEG 1: GENERERA
        // =========================
        public async Task<IActionResult> OnPostGenerateAsync()
        {
            try
            {
                var json = await _jiraExportService.GenerateAsync(
                    JiraEmail,
                    JiraApiToken,
                    JiraBaseUrl,
                    ProjectKey
                );

                var exportDir = Path.Combine(
                    _environment.ContentRootPath,
                    "App_Data",
                    "exports"
                );

                Directory.CreateDirectory(exportDir);

                var fileName =
                    $"jira-{ProjectKey}-{DateTime.UtcNow:yyyy-MM-dd-HH-mm-ss}.json";

                ExportFilePath = Path.Combine(exportDir, fileName);

                await System.IO.File.WriteAllTextAsync(ExportFilePath, json);

                Step1Completed = true;
            }
            catch (Exception ex)
            {
                Error = ex.Message;
            }

            return Page();
        }

        // =========================
        // STEG 2: ANALYSERA
        // =========================
        public async Task<IActionResult> OnPostAnalyzeAsync()
        {
            if (string.IsNullOrWhiteSpace(ExportFilePath) ||
                !System.IO.File.Exists(ExportFilePath))
            {
                Error = "Exportfilen saknas. Kör Steg 1 igen.";
                Step1Completed = false;
                return Page();
            }

            try
            {
                var json = await System.IO.File.ReadAllTextAsync(ExportFilePath);
                AnalysisResult = await _jiraAnalyzer.AnalyzeAsync(json);
            }
            catch (Exception ex)
            {
                Error = ex.Message;
            }

            Step1Completed = true;
            return Page();
        }
    }
}
