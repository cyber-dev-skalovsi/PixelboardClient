using System.Diagnostics;
using System.Text.Json;
using PixelboardClient.Models;

namespace PixelboardClient.Services
{
    public class PixelboardService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PixelboardService> _logger;
        private readonly string _baseUrl;
        private readonly int _boardWidth;
        private readonly int _boardHeight;

        public PixelboardService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<PixelboardService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _baseUrl = _configuration["ApiSettings:BaseUrl"] ?? "https://edu.jakobmeier.ch";
            _boardWidth = _configuration.GetValue<int>("ApiSettings:BoardWidth", 16);
            _boardHeight = _configuration.GetValue<int>("ApiSettings:BoardHeight", 16);

            _logger.LogInformation($"PixelboardService initialisiert mit BaseUrl: {_baseUrl}");
        }

        /// <summary>
        /// Holt ein einzelnes Pixel von der API (GET)
        /// </summary>
        public async Task<PixelColor?> GetPixelAsync(int x, int y)
        {
            try
            {
                var url = $"{_baseUrl}/api/color/{x}/{y}";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    _logger.LogDebug($"Pixel ({x},{y}): {content}");

                    var pixel = ParsePixelColor(content, x, y);
                    return pixel;
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"Fehler bei ({x},{y}): Status {response.StatusCode}, Body: {errorBody}");

                    // Pink als Fehlerfarbe
                    return new PixelColor
                    {
                        X = x,
                        Y = y,
                        Red = 255,
                        Green = 0,
                        Blue = 255
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception beim Abrufen von Pixel ({x},{y})");
                return new PixelColor { X = x, Y = y, Red = 255, Green = 0, Blue = 255 };
            }
        }

        /// <summary>
        /// Parse verschiedene API-Antwort-Formate
        /// </summary>
        private PixelColor ParsePixelColor(string content, int x, int y)
        {
            var pixel = new PixelColor { X = x, Y = y };

            try
            {
                // Versuche JSON zu parsen: {"red":255,"green":0,"blue":0}
                if (content.TrimStart().StartsWith("{"))
                {
                    var jsonDoc = JsonDocument.Parse(content);
                    var root = jsonDoc.RootElement;

                    if (root.TryGetProperty("red", out var red))
                        pixel.Red = red.GetInt32();
                    if (root.TryGetProperty("green", out var green))
                        pixel.Green = green.GetInt32();
                    if (root.TryGetProperty("blue", out var blue))
                        pixel.Blue = blue.GetInt32();

                    return pixel;
                }

                // Fallback auf String-Parsing (rgb() oder #HEX)
                return PixelColor.Parse(content);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Fehler beim Parsen von '{content}': {ex.Message}");
                return pixel; // Gibt schwarzes Pixel zurück
            }
        }

        /// <summary>
        /// Lädt das ganze Board sequenziell (langsam, ein Pixel nach dem anderen)
        /// </summary>
        public async Task<PixelColor[,]> GetBoardSequentialAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            var pixels = new PixelColor[_boardWidth, _boardHeight];
            int successCount = 0;
            int errorCount = 0;

            for (int x = 0; x < _boardWidth; x++)
            {
                for (int y = 0; y < _boardHeight; y++)
                {
                    var pixel = await GetPixelAsync(x, y);
                    pixels[x, y] = pixel ?? new PixelColor { X = x, Y = y };

                    if (pixel != null && !pixel.IsErrorColor())
                        successCount++;
                    else
                        errorCount++;
                }
            }

            stopwatch.Stop();
            _logger.LogInformation(
                $"Sequenziell: {stopwatch.ElapsedMilliseconds}ms, " +
                $"{successCount} erfolgreich, {errorCount} Fehler");

            return pixels;
        }

        /// <summary>
        /// Lädt das ganze Board parallel (schnell, alle gleichzeitig)
        /// </summary>
        public async Task<PixelColor[,]> GetBoardParallelAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            var pixels = new PixelColor[_boardWidth, _boardHeight];
            var tasks = new List<Task>();
            int successCount = 0;
            int errorCount = 0;
            var lockObj = new object();

            for (int x = 0; x < _boardWidth; x++)
            {
                for (int y = 0; y < _boardHeight; y++)
                {
                    int localX = x;
                    int localY = y;

                    tasks.Add(Task.Run(async () =>
                    {
                        var pixel = await GetPixelAsync(localX, localY);
                        pixels[localX, localY] = pixel ?? new PixelColor { X = localX, Y = localY };

                        lock (lockObj)
                        {
                            if (pixel != null && !pixel.IsErrorColor())
                                successCount++;
                            else
                                errorCount++;
                        }
                    }));
                }
            }

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            _logger.LogInformation(
                $"Parallel: {stopwatch.ElapsedMilliseconds}ms, " +
                $"{successCount} erfolgreich, {errorCount} Fehler");

            return pixels;
        }

        /// <summary>
        /// Setzt ein Pixel auf eine bestimmte Farbe (POST) - BONUS für MS1
        /// </summary>
        public async Task<bool> SetPixelAsync(int x, int y, int red, int green, int blue)
        {
            try
            {
                var url = $"{_baseUrl}/api/color/{x}/{y}";

                // JSON Body erstellen
                var pixelData = new
                {
                    red = red,
                    green = green,
                    blue = blue
                };

                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(pixelData),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync(url, jsonContent);

                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation($"✓ Pixel ({x},{y}) gesetzt auf RGB({red},{green},{blue}) - Response: {responseBody}");
                    return true;
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"✗ Fehler beim Setzen: {response.StatusCode} - {errorBody}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception beim Setzen von Pixel ({x},{y})");
                return false;
            }
        }

        /// <summary>
        /// Setzt ein Pixel auf die Team-Farbe (POST) - BONUS für MS1
        /// </summary>
        public async Task<bool> SetPixelTeamColorAsync(int x, int y, string teamColor)
        {
            try
            {
                var url = $"{_baseUrl}/api/team/{teamColor}/color/{x}/{y}";

                var response = await _httpClient.PostAsync(url, null);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"✓ Pixel ({x},{y}) für Team {teamColor} gesetzt");
                    return true;
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"✗ Fehler: {response.StatusCode} - {errorBody}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception beim Team-Pixel setzen ({x},{y})");
                return false;
            }
        }
    }
}
