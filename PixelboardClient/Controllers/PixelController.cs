using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PixelboardClient.Models;
using System.Text;
using System.Text.Json;

namespace PixelboardClient.Controllers;

[Authorize]
[ApiController]
[Route("api/pixel")]
public class PixelController : ControllerBase
{
    private readonly ILogger<PixelController> _logger;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;

    public PixelController(
        ILogger<PixelController> logger,
        IConfiguration config,
        IHttpClientFactory httpFactory)
    {
        _logger = logger;
        _config = config;
        _httpFactory = httpFactory;
    }

    [HttpPost("set")]
    public async Task<IActionResult> SetPixel([FromBody] SetPixelRequest req)
    {
        _logger.LogInformation("🔥 SETPIXEL START - X:{X} Y:{Y} Team:{Team}", req.X, req.Y, req.Team);

        var baseUrl = _config["ApiUrl"]?.TrimEnd('/') ?? throw new InvalidOperationException("ApiUrl missing");
        using var client = _httpFactory.CreateClient();

        var token = await HttpContext.GetTokenAsync("access_token");

        // Token expiry check
        if (!string.IsNullOrEmpty(token) && IsTokenExpired(token))
        {
            _logger.LogWarning("🕐 Token expired, attempting refresh...");
            var newToken = await RefreshTokenAsync();
            if (newToken != null) token = newToken;
            else return Ok(new { success = false, error = "Token refresh failed" });
        }

        if (string.IsNullOrEmpty(token))
        {
            _logger.LogError("❌ ACCESS_TOKEN fehlt!");
            return Ok(new { success = false, error = "Kein access_token verfügbar" });
        }

        _logger.LogInformation("✅ ACCESS_TOKEN gefunden: {Length} Zeichen", token.Length);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var payload = new { X = req.X, Y = req.Y, Team = req.Team, Red = 0, Green = 0, Blue = 0 };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var resp = await client.PostAsync($"{baseUrl}/api/color", content);
        var body = await resp.Content.ReadAsStringAsync();

        _logger.LogInformation("✅ Response: {Status} {Body}", (int)resp.StatusCode, body);

        return Ok(new { success = resp.IsSuccessStatusCode, message = body, httpStatus = (int)resp.StatusCode });
    }

    private bool IsTokenExpired(string token)
    {
        try
        {
            var payload = token.Split('.')[1];
            var jsonBytes = ParseBase64Url(payload);
            var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);
            if (keyValuePairs.TryGetValue("exp", out var expObj) && expObj is long exp)
            {
                return DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp;
            }
        }
        catch { }
        return true; // Assume expired if can't parse
    }

    private static byte[] ParseBase64Url(string input)
    {
        var padded = input.Length % 4 == 2 ? input + "==" : input.Length % 4 == 3 ? input + "=" : input;
        return Convert.FromBase64String(padded);
    }
    private async Task<string?> RefreshTokenAsync()
    {
        // Option 1: Silent refresh via refresh_token (if available)
        var refreshToken = await HttpContext.GetTokenAsync("refresh_token");
        if (!string.IsNullOrEmpty(refreshToken))
        {
            // Implement refresh logic hier
            return null; // Placeholder
        }

        // Option 2: Redirect to login
        await HttpContext.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme);
        return null;
    }
    private static PixelColor? TryParseColorResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            int r = root.TryGetProperty("red", out var rEl) ? rEl.GetInt32() :
                    root.TryGetProperty("Red", out rEl) ? rEl.GetInt32() : 0;

            int g = root.TryGetProperty("green", out var gEl) ? gEl.GetInt32() :
                    root.TryGetProperty("Green", out gEl) ? gEl.GetInt32() : 0;

            int b = root.TryGetProperty("blue", out var bEl) ? bEl.GetInt32() :
                    root.TryGetProperty("Blue", out bEl) ? bEl.GetInt32() : 0;

            return new PixelColor(r, g, b);
        }
        catch
        {
            return null;
        }
    }

    public record SetPixelRequest(
        int X,
        int Y,
        int Team,
        int? Red = null,
        int? Green = null,
        int? Blue = null 
);
}