using Microsoft.AspNetCore.Mvc.RazorPages;
using PixelboardClient.Models;
using PixelboardClient.Services;

namespace PixelboardClient.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly PixelboardService _pixelboardService;

        public PixelColor[,]? Pixels { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsLoading { get; set; }

        public IndexModel(ILogger<IndexModel> logger, PixelboardService pixelboardService)
        {
            _logger = logger;
            _pixelboardService = pixelboardService;
        }

        public async Task OnGetAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Lade Pixelboard...");

                // Parallel laden (schneller!)
                Pixels = await _pixelboardService.GetBoardParallelAsync();

                _logger.LogInformation("Pixelboard erfolgreich geladen");
                IsLoading = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Laden des Pixelboards");
                ErrorMessage = $"Fehler: {ex.Message}";
                IsLoading = false;
            }
        }
    }
}
