using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

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
        var token = await HttpContext.GetTokenAsync("access_token");
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
                    teams.Add(new { id = i, data = data });
                }
            }
            catch { }
        }
        return Ok(teams);
    }
}
