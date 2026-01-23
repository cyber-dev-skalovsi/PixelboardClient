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
        private readonly IBoardStateService _boardStateService;
        private readonly IConfiguration _configuration;

        public PixelColor[,] Pixels { get; set; } = new PixelColor[16, 16];
        public string? ErrorMessage { get; set; }
        public long LoadTimeMs { get; set; } = 0;
        public string ApiUrl { get; set; } = "Unknown";
        public string LoadMode { get; set; } = "cache";

        [BindProperty]
        public int TeamNumber { get; set; } = 1;

        public IndexModel(
            ILogger<IndexModel> logger,
            IBoardStateService boardStateService,
            IConfiguration configuration)
        {
            _logger = logger;
            _boardStateService = boardStateService;
            _configuration = configuration;
        }

        public void OnGet()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();

                ApiUrl = _configuration["ApiUrl"] ?? "http://localhost:5085";
                Pixels = _boardStateService.GetAllPixels();

                stopwatch.Stop();
                LoadTimeMs = stopwatch.ElapsedMilliseconds;

                _logger.LogInformation("Index Seite geladen in {ms}ms (mit Cache)", LoadTimeMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Laden der Index Seite");
                ErrorMessage = $"Fehler beim Laden: {ex.Message}";

                for (int x = 0; x < 16; x++)
                {
                    for (int y = 0; y < 16; y++)
                    {
                        Pixels[x, y] = new PixelColor(0, 0, 0);
                    }
                }
            }
        }

        public async Task<IActionResult> OnGetLoadPixelsAsync(string mode = "cache")
        {
            try
            {
                var (pixels, elapsedMs) = mode.ToLower() switch
                {
                    "parallel" => await _boardStateService.LoadPixelsParallelAsync(),
                    "sequential" => await _boardStateService.LoadPixelsSequentialAsync(),
                    "cache" => await _boardStateService.LoadPixelsCachedAsync(),
                    _ => await _boardStateService.LoadPixelsCachedAsync()
                };

                var pixelList = new List<dynamic>();
                for (int x = 0; x < 16; x++)
                {
                    for (int y = 0; y < 16; y++)
                    {
                        pixelList.Add(new
                        {
                            x,
                            y,
                            red = pixels[x, y].Red,
                            green = pixels[x, y].Green,
                            blue = pixels[x, y].Blue
                        });
                    }
                }

                return new JsonResult(new
                {
                    success = true,
                    loadTimeMs = elapsedMs,
                    mode = mode.ToLower(),
                    pixels = pixelList
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Laden der Pixels im Modus: {mode}", mode);
                return new JsonResult(new
                {
                    success = false,
                    error = ex.Message,
                    mode = mode.ToLower()
                });
            }
        }
    }
}
