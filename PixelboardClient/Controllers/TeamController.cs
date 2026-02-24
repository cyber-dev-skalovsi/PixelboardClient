using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

[ApiController]
[Route("api/teams")]
public class TeamController : ControllerBase
{
    private readonly ILogger _logger;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;

    public TeamController(ILogger<TeamController> logger, IConfiguration config, IHttpClientFactory httpFactory)
    {
        _logger = logger; _config = config; _httpFactory = httpFactory;
    }

    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetLeaderboard()
    {
        var apiUrl = _config["ApiUrl"];
        var client = _httpFactory.CreateClient();
        var token = await HttpContext.GetTokenAsync("id_token");
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var teams = new List<object>();
        for (int i = 1; i <= 10; i++)
        {
            try
            {
                var resp = await client.GetAsync($"{apiUrl}/api/game/team/{i}");
                if (resp.IsSuccessStatusCode)
                {
                    var data = await resp.Content.ReadAsStringAsync();
                    // Parse JSON für Farbe + Punkte (wie im Onenote erwartet)
                    var json = JsonSerializer.Deserialize<JsonElement>(data);
                    teams.Add(new
                    {
                        id = i,
                        name = json.GetProperty("name").GetString() ?? $"Team {i}",
                        points = json.TryGetProperty("points", out var p) ? p.GetInt32() : 0,
                        color = new
                        {
                            r = json.TryGetProperty("color", out var c) && c.TryGetProperty("red", out var r) ? r.GetInt32() : 0,
                            g = json.TryGetProperty("color", out var c2) && c2.TryGetProperty("green", out var g) ? g.GetInt32() : 0,
                            b = json.TryGetProperty("color", out var c3) && c3.TryGetProperty("blue", out var b) ? b.GetInt32() : 0
                        }
                    });
                }
            }
            catch { }
        }
        return Ok(teams);
    }
}