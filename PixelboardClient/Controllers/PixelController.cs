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
        if (req is null || req.X < 0 || req.X >= 16 || req.Y < 0 || req.Y >= 16 || req.Team < 1)
            return BadRequest("Invalid pixel coordinates or team number");

        try
        {
            var baseUrl = _config["ApiUrl"]?.TrimEnd('/')
                ?? throw new InvalidOperationException("ApiUrl missing");

            using var client = _httpFactory.CreateClient();

            var payload = new { x = req.X, y = req.Y, team = req.Team };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PostAsync($"{baseUrl}/api/color", content);

            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Pixel set failed: {status} – {body}", resp.StatusCode, body);
                return Ok(new { success = false, error = body });
            }

            // Try to get current color after set
            var colorResp = await client.GetAsync($"{baseUrl}/api/color/{req.X}/{req.Y}");
            if (!colorResp.IsSuccessStatusCode)
                return Ok(new { success = true, message = body });

            var colorJson = await colorResp.Content.ReadAsStringAsync();
            var color = TryParseColorResponse(colorJson);

            return Ok(new
            {
                success = true,
                message = body,
                red = color?.Red,
                green = color?.Green,
                blue = color?.Blue
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Set pixel ({x},{y}) failed", req.X, req.Y);
            return Ok(new { success = false, error = ex.Message });
        }
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

    public record SetPixelRequest(int X, int Y, int Team);
}