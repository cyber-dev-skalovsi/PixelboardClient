using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace PixelboardClient.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PixelController : ControllerBase
    {
        private readonly ILogger<PixelController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public PixelController(
            ILogger<PixelController> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost("set")]
        public async Task<IActionResult> SetPixel([FromBody] SetPixelRequest request)
        {
            try
            {
                var apiUrl = _configuration["ApiUrl"];
                var client = _httpClientFactory.CreateClient();

                var payload = new
                {
                    x = request.X,
                    y = request.Y,
                    team = request.Team
                };

                var jsonContent = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger.LogInformation("Sende POST zu {url} mit Payload: {payload}",
                    $"{apiUrl}/api/color", jsonContent);

                var response = await client.PostAsync($"{apiUrl}/api/color", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Response Status: {status}, Body: {body}",
                    response.StatusCode, responseBody);

                if (response.IsSuccessStatusCode)
                {
                    return Ok(new { success = true, message = responseBody });
                }
                else
                {
                    return Ok(new
                    {
                        success = false,
                        error = $"Status: {response.StatusCode}, Message: {responseBody}"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception beim Setzen von Pixel ({x},{y})",
                    request.X, request.Y);
                return Ok(new { success = false, error = ex.Message });
            }
        }

        public class SetPixelRequest
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Team { get; set; }
        }
    }
}
