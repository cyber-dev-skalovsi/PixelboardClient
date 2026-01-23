using PixelboardClient.Models;
using System.Diagnostics;

namespace PixelboardClient.Services
{
    public class BoardStateService : BackgroundService, IBoardStateService
    {
        private readonly ILogger<BoardStateService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private PixelColor[,] _pixels = new PixelColor[16, 16];
        private readonly object _lock = new object();
        private string? _apiUrl;

        public BoardStateService(
            ILogger<BoardStateService> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;

            // Initialisiere mit schwarzen Pixeln
            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    _pixels[x, y] = new PixelColor(0, 0, 0);
                }
            }
        }

        public PixelColor? Pixel(int x, int y)
        {
            if (x < 0 || x >= 16 || y < 0 || y >= 16)
                return null;

            lock (_lock)
            {
                return _pixels[x, y];
            }
        }

        public PixelColor[,] GetAllPixels()
        {
            lock (_lock)
            {
                return (PixelColor[,])_pixels.Clone();
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // WICHTIG: Delay am Anfang damit Startup nicht blockiert wird
            await Task.Delay(1000, stoppingToken);

            _apiUrl = _configuration["ApiUrl"];
            _logger.LogInformation("BoardStateService gestartet mit API URL: {url}", _apiUrl);

            // Initiales Laden
            await LoadPixelsAsync();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                    await LoadPixelsAsync();
                    _logger.LogInformation("Pixels aktualisiert um {time}", DateTimeOffset.Now);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fehler beim Laden der Pixels");
                }
            }
        }

        private async Task LoadPixelsAsync()
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            var stopwatch = Stopwatch.StartNew();

            var tasks = new List<Task<(int x, int y, PixelColor? color)>>();

            // Parallele Requests für alle Pixel
            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    int capturedX = x;
                    int capturedY = y;
                    tasks.Add(FetchPixelAsync(client, capturedX, capturedY));
                }
            }

            var results = await Task.WhenAll(tasks);

            lock (_lock)
            {
                foreach (var (x, y, color) in results)
                {
                    if (color != null)
                    {
                        _pixels[x, y] = color;
                    }
                }
            }

            stopwatch.Stop();
            _logger.LogInformation("Alle 256 Pixels geladen in {ms}ms", stopwatch.ElapsedMilliseconds);
        }

        private async Task<(int x, int y, PixelColor? color)> FetchPixelAsync(
            HttpClient client, int x, int y)
        {
            try
            {
                var url = $"{_apiUrl}/api/color/{x}/{y}";
                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();

                    // ✅ FIXED: Use FromRgbString which now handles JSON
                    var color = PixelColor.FromRgbString(content);

                    if (color != null)
                    {
                        return (x, y, color);
                    }
                    else
                    {
                        _logger.LogWarning("Konnte Farbe nicht parsen für Pixel ({x},{y}): {content}",
                            x, y, content);
                        return (x, y, new PixelColor(0, 0, 0));
                    }
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("HTTP {status} beim Laden von Pixel ({x},{y}): {body}",
                        response.StatusCode, x, y, errorBody);
                    // Bei Fehler: Pink als Markierung
                    return (x, y, new PixelColor(255, 0, 255));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception beim Laden von Pixel ({x},{y})", x, y);
                return (x, y, new PixelColor(255, 0, 255));
            }
        }


    }
}
