using PixelboardClient.Models;
using System.Diagnostics;
using System.Threading.Channels;

namespace PixelboardClient.Services
{
    public class BoardStateService : BackgroundService, IBoardStateService
    {
        private readonly ILogger<BoardStateService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IGraphQLPixelService _graphQLService;
        private PixelColor[,] _pixels = new PixelColor[16, 16];
        private readonly object _lock = new object();
        private string? _apiUrl;
        private bool _useGraphQL = true;

        public BoardStateService(
            ILogger<BoardStateService> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            IGraphQLPixelService graphQLService)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _graphQLService = graphQLService;

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    _pixels[x, y] = new PixelColor(0, 0, 0);
                }
            }
        }
        private readonly Channel<PixelUpdate> _pixelUpdateChannel = Channel.CreateUnbounded<PixelUpdate>();

        public record PixelUpdate(int X, int Y, int Red, int Green, int Blue);

   
        private async Task NotifyPixelUpdate(int x, int y, PixelColor color)
        {
            await _pixelUpdateChannel.Writer.WriteAsync(new PixelUpdate(x, y, color.Red, color.Green, color.Blue));
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

        public async Task<(PixelColor[,] pixels, long elapsedMs)> LoadPixelsParallelAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            var tasks = new List<Task<(int x, int y, PixelColor? color)>>();

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
            var pixels = new PixelColor[16, 16];

            foreach (var (x, y, color) in results)
            {
                pixels[x, y] = color ?? new PixelColor(0, 0, 0);
            }

            stopwatch.Stop();
            return (pixels, stopwatch.ElapsedMilliseconds);
        }

        public async Task<(PixelColor[,] pixels, long elapsedMs)> LoadPixelsSequentialAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            var pixels = new PixelColor[16, 16];

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    var (_, _, color) = await FetchPixelAsync(client, x, y);
                    pixels[x, y] = color ?? new PixelColor(0, 0, 0);
                }
            }

            stopwatch.Stop();
            return (pixels, stopwatch.ElapsedMilliseconds);
        }

        public async Task<(PixelColor[,] pixels, long elapsedMs)> LoadPixelsCachedAsync()
        {
            var stopwatch = Stopwatch.StartNew();

            lock (_lock)
            {
                var cachedPixels = (PixelColor[,])_pixels.Clone();
                stopwatch.Stop();
                return (cachedPixels, stopwatch.ElapsedMilliseconds);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(1000, stoppingToken);

            _apiUrl = _configuration["ApiUrl"];
            _logger.LogInformation("BoardStateService gestartet mit API URL: {url} (GraphQL: {gql})",
                _apiUrl, _useGraphQL);

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
            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (_useGraphQL)
                {
                    var newPixels = await _graphQLService.LoadAllPixelsAsync();

                    lock (_lock)
                    {
                        _pixels = newPixels;
                    }
                }
                else
                {
                    await LoadPixelsRestAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Laden der Pixels");

                if (_useGraphQL)
                {
                    _logger.LogWarning("GraphQL fehlgeschlagen, versuche REST API...");
                    _useGraphQL = false;
                    await LoadPixelsRestAsync();
                }
            }

            stopwatch.Stop();
            _logger.LogInformation("Alle 256 Pixels geladen in {ms}ms (Methode: {method})",
                stopwatch.ElapsedMilliseconds,
                _useGraphQL ? "GraphQL" : "REST");
        }

        private async Task LoadPixelsRestAsync()
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            var tasks = new List<Task<(int x, int y, PixelColor? color)>>();

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
                    var color = PixelColor.FromRgbString(content);

                    if (color != null)
                    {
                        return (x, y, color);
                    }
                    else
                    {
                        return (x, y, new PixelColor(0, 0, 0));
                    }
                }
                else
                {
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
