using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PixelboardClient.Models;
using PixelboardClient.Services;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace PixelboardClient.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly IBoardStateService _boardStateService;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public PixelColor[,] Pixels { get; set; } = new PixelColor[16, 16];
        public string? ErrorMessage { get; set; }
        public long LoadTimeMs { get; set; }
        public bool UseCache { get; set; } = true;
        public string? ApiUrl { get; set; }  // ✅ NEU

        [BindProperty]
        public int TeamNumber { get; set; } = 1;

        public IndexModel(
            ILogger<IndexModel> logger,
            IBoardStateService boardStateService,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _boardStateService = boardStateService;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        public void OnGet()
        {
            var stopwatch = Stopwatch.StartNew();

            // Lade Pixels aus dem Cache
            Pixels = _boardStateService.GetAllPixels();

            // ✅ NEU: API URL anzeigen
            ApiUrl = _configuration["ApiUrl"];

            stopwatch.Stop();
            LoadTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation("Index Seite geladen in {ms}ms (mit Cache)", LoadTimeMs);
        }

        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> OnPostSetPixelAsync(int x, int y, int team)
        {
            try
            {
                var apiUrl = _configuration["ApiUrl"];
                var client = _httpClientFactory.CreateClient();

                var payload = new
                {
                    x = x,
                    y = y,
                    team = team
                };

                var jsonContent = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await client.PostAsync($"{apiUrl}/api/color", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Pixel ({x},{y}) erfolgreich gesetzt für Team {team}. Response: {response}",
                        x, y, team, responseBody);

                    return new JsonResult(new { success = true, message = responseBody });
                }
                else
                {
                    _logger.LogWarning("Fehler beim Setzen von Pixel ({x},{y}): Status {status}, Body: {body}",
                        x, y, response.StatusCode, responseBody);

                    return new JsonResult(new
                    {
                        success = false,
                        error = $"Status: {response.StatusCode}, Message: {responseBody}"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception beim Setzen von Pixel ({x},{y})", x, y);
                return new JsonResult(new { success = false, error = ex.Message });
            }
        }
    }
}
