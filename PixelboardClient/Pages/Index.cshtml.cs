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

        public PixelColor[,] Pixels { get; set; } = new PixelColor[16, 16];
        public string? ErrorMessage { get; set; }
        public long LoadTimeMs { get; set; } = 0;

        public IndexModel(
            ILogger<IndexModel> logger,
            IBoardStateService boardStateService)
        {
            _logger = logger;
            _boardStateService = boardStateService;
        }

        public void OnGet()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();

                Pixels = _boardStateService.GetAllPixels();

                stopwatch.Stop();
                LoadTimeMs = stopwatch.ElapsedMilliseconds;

                _logger.LogInformation("Pixelboard geladen in {ms}ms", LoadTimeMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Laden der Seite");
                ErrorMessage = $"Fehler: {ex.Message}";

                for (int x = 0; x < 16; x++)
                {
                    for (int y = 0; y < 16; y++)
                    {
                        Pixels[x, y] = new PixelColor(0, 0, 0);
                    }
                }
            }
        }
    }
}
