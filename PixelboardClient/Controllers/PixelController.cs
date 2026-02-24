using Microsoft.AspNetCore.Authentication;
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