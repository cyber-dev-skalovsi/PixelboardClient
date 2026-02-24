using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PixelboardClient.Models;
using PixelboardClient.Services;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;

namespace PixelboardClient.Pages
{
    [AllowAnonymous]
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly IBoardStateService _boardStateService;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpFactory;

        public PixelColor[,] Pixels { get; set; } = new PixelColor[16, 16];
        public string? ErrorMessage { get; set; }
        public long LoadTimeMs { get; set; } = 0;
        public string ApiUrl { get; set; } = "Unknown";
        public string LoadMode { get; set; } = "cache";
        public bool IsAuthenticated { get; set; }
        public string? UserName { get; set; }
        public string? UserId { get; set; }

        [BindProperty]
        public int TeamNumber { get; set; } = 1;

        public IndexModel(
            ILogger<IndexModel> logger,
            IBoardStateService boardStateService,
            IConfiguration configuration,
            IHttpClientFactory httpFactory)
        {
            _logger = logger;
            _boardStateService = boardStateService;
            _configuration = configuration;
            _httpFactory = httpFactory;
        }
        public IActionResult OnGetIdToken()
        {
            var idToken = HttpContext.GetTokenAsync("id_token").Result;
            return Content($"ID_TOKEN:\n{idToken}\n\nDECODE AUF jwt.io");
        }
        public void OnGet()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();

                IsAuthenticated = User.Identity?.IsAuthenticated ?? false;
                UserName = User.Identity?.Name ?? "Gast";
                UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "keine ID";

                ApiUrl = _configuration["ApiUrl"] ?? "http://localhost:5085";
                Pixels = _boardStateService.GetAllPixels();

                stopwatch.Stop();
                LoadTimeMs = stopwatch.ElapsedMilliseconds;

                _logger.LogInformation("Index Seite geladen in {ms}ms (Auth: {auth}, User: {user})",
                    LoadTimeMs, IsAuthenticated, UserName);
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
        public IActionResult OnGetLogin()
        {
            return Challenge(
                new AuthenticationProperties { RedirectUri = "/" },
                OpenIdConnectDefaults.AuthenticationScheme);
        }

        public IActionResult OnPostLogout()
        {
            return SignOut(
                new AuthenticationProperties { RedirectUri = "/" },
                CookieAuthenticationDefaults.AuthenticationScheme,
                OpenIdConnectDefaults.AuthenticationScheme);
        }
        public async Task<IActionResult> OnGetTeamBudget(int teamId = 1)
        {
            try
            {
                var apiUrl = _configuration["ApiUrl"];
                using var client = _httpFactory.CreateClient();
                var token = await HttpContext.GetTokenAsync("access_token");

                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                var resp = await client.GetAsync($"{apiUrl}/api/game/team/{teamId}");

                if (resp.IsSuccessStatusCode)
                {
                    var content = await resp.Content.ReadAsStringAsync();
                    return new JsonResult(JsonSerializer.Deserialize<object>(content));
                }
                return new JsonResult(new { budgetRemaining = 100 });
            }
            catch
            {
                return new JsonResult(new { budgetRemaining = 100 });
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
