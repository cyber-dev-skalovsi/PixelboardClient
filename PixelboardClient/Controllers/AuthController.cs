using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Text.Json;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IHttpContextAccessor _contextAccessor;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IHttpContextAccessor contextAccessor, ILogger<AuthController> logger)
    {
        _contextAccessor = contextAccessor;
        _logger = logger;
    }

    // MS3 - AuthService Interface bleibt unverändert
    public bool IsAuthenticated => _contextAccessor.HttpContext.User.Identity.IsAuthenticated;
    public string Username => _contextAccessor.HttpContext.User.Identity.Name ?? "Gast";

    // 🔥 MS4 A2.1 - Token Status Check (Frontend Polling)
    [HttpGet("status")]
    public IActionResult GetAuthStatus()
    {
        return Ok(new
        {
            authenticated = User.Identity?.IsAuthenticated ?? false,
            username = User.Identity?.Name ?? "Gast"
        });
    }

    // 🔥 MS4 A2.1 - Token Ablauf prüfen (JWT exp parsen)
    [HttpGet("token-status")]
    public async Task<IActionResult> GetTokenStatus()
    {
        try
        {
            var token = await HttpContext.GetTokenAsync("access_token");
            if (string.IsNullOrEmpty(token))
                return Ok(new { expired = true, expiresIn = 0 });

            // JWT Payload decodieren (Base64)
            var payload = token.Split('.')[1];
            var padded = payload.PadRight((payload.Length + 3) / 4 * 4, '=');
            var jsonBytes = Convert.FromBase64String(padded);
            var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(
                jsonBytes, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (keyValuePairs.TryGetValue("exp", out var expObj) && expObj is JsonElement expElement && expElement.ValueKind == JsonValueKind.Number)
            {
                var exp = expElement.GetInt64();
                var expiresIn = exp - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                return Ok(new
                {
                    expired = expiresIn <= 0,
                    expiresIn = Math.Max(0, (int)expiresIn)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token parsing failed");
        }

        return Ok(new { expired = true, expiresIn = 0 });
    }
}

// BEHALTEN: Deine ursprüngliche AuthService Klasse (separat)
public interface IAuthService
{
    bool IsAuthenticated { get; }
    string Username { get; }
}

public class AuthService : IAuthService
{
    private readonly IHttpContextAccessor _contextAccessor;

    public AuthService(IHttpContextAccessor contextAccessor)
    {
        _contextAccessor = contextAccessor;
    }

    public bool IsAuthenticated => _contextAccessor.HttpContext.User.Identity.IsAuthenticated;
    public string Username => _contextAccessor.HttpContext.User.Identity.Name ?? "Gast";
}
