using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PixelboardClient.Models;
using PixelboardClient.Services;
using System.Diagnostics;

namespace PixelboardClient.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly PixelboardService _pixelboardService;
        private readonly IConfiguration _configuration;

        public PixelColor[,]? Pixels { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsLoading { get; set; }
        public long? LoadTimeMs { get; set; }
        public string? LoadMethod { get; set; }
        public int TotalPixels => 16 * 16;
        public string CurrentEnvironment { get; set; }
        public string ApiBaseUrl { get; set; }

        [BindProperty]
        public int SelectedX { get; set; }

        [BindProperty]
        public int SelectedY { get; set; }

        [BindProperty]
        public int Red { get; set; } = 255;

        [BindProperty]
        public int Green { get; set; } = 0;

        [BindProperty]
        public int Blue { get; set; } = 0;

        public string? SetPixelResult { get; set; }

        public IndexModel(
            ILogger<IndexModel> logger,
            PixelboardService pixelboardService,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _logger = logger;
            _pixelboardService = pixelboardService;
            _configuration = configuration;
            CurrentEnvironment = environment.EnvironmentName;
            ApiBaseUrl = _configuration["ApiSettings:BaseUrl"] ?? "Unknown";
        }

        public void OnGet()
        {
            // Zeige leeres Board beim ersten Laden
            _logger.LogInformation("Index-Seite geladen");
        }

        public async Task<IActionResult> OnPostLoadParallelAsync()
        {
            try
            {
                _logger.LogInformation("Starte paralleles Laden...");
                var stopwatch = Stopwatch.StartNew();
                Pixels = await _pixelboardService.GetBoardParallelAsync();
                stopwatch.Stop();

                LoadTimeMs = stopwatch.ElapsedMilliseconds;
                LoadMethod = "Parallel";

                _logger.LogInformation($"Parallel geladen in {LoadTimeMs}ms");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim parallelen Laden");
                ErrorMessage = $"Fehler beim parallelen Laden: {ex.Message}";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostLoadSequentialAsync()
        {
            try
            {
                _logger.LogInformation("Starte sequenzielles Laden...");
                var stopwatch = Stopwatch.StartNew();
                Pixels = await _pixelboardService.GetBoardSequentialAsync();
                stopwatch.Stop();

                LoadTimeMs = stopwatch.ElapsedMilliseconds;
                LoadMethod = "Sequenziell (1 nach dem anderen)";

                _logger.LogInformation($"Sequenziell geladen in {LoadTimeMs}ms");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim sequenziellen Laden");
                ErrorMessage = $"Fehler beim sequenziellen Laden: {ex.Message}";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostSetPixelAsync()
        {
            try
            {
                _logger.LogInformation($"Versuche Pixel ({SelectedX},{SelectedY}) zu setzen auf RGB({Red},{Green},{Blue})");

                var success = await _pixelboardService.SetPixelAsync(
                    SelectedX, SelectedY, Red, Green, Blue);

                if (success)
                {
                    SetPixelResult = $"✓ Pixel ({SelectedX},{SelectedY}) erfolgreich auf RGB({Red},{Green},{Blue}) gesetzt!";

                    // Board neu laden um Änderung zu sehen
                    var stopwatch = Stopwatch.StartNew();
                    Pixels = await _pixelboardService.GetBoardParallelAsync();
                    stopwatch.Stop();
                    LoadTimeMs = stopwatch.ElapsedMilliseconds;
                    LoadMethod = "Parallel (nach Pixel setzen)";
                }
                else
                {
                    SetPixelResult = "✗ Fehler beim Setzen des Pixels. Prüfen Sie die Logs.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception beim Setzen des Pixels");
                SetPixelResult = $"✗ Fehler: {ex.Message}";
            }

            return Page();
        }
    }
}
