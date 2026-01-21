using System.Diagnostics;
using PixelboardClient.Models;

namespace PixelboardClient.Services
{
    public class PixelboardService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly string _baseUrl;
        private readonly int _boardWidth;
        private readonly int _boardHeight;

        public PixelboardService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _baseUrl = _configuration["ApiSettings:BaseUrl"] ?? "https://edu.jakobmeier.ch";
            _boardWidth = _configuration.GetValue<int>("ApiSettings:BoardWidth", 16);
            _boardHeight = _configuration.GetValue<int>("ApiSettings:BoardHeight", 16);
        }

        public async Task<PixelColor?> GetPixelAsync(int x, int y)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/color/{x}/{y}");

                Console.WriteLine($"GET /api/color/{x}/{y} - Status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var colorString = await response.Content.ReadAsStringAsync();
                    var pixel = PixelColor.Parse(colorString);
                    pixel.X = x;
                    pixel.Y = y;
                    return pixel;
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Fehler: Status {response.StatusCode}, Body: {errorBody}");

                    return new PixelColor { X = x, Y = y, Red = 255, Green = 0, Blue = 255 };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception beim Abrufen von Pixel ({x},{y}): {ex.Message}");
                return new PixelColor { X = x, Y = y, Red = 255, Green = 0, Blue = 255 };
            }
        }
        public async Task<PixelColor[,]> GetBoardSequentialAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            var pixels = new PixelColor[_boardWidth, _boardHeight];

            for (int x = 0; x < _boardWidth; x++)
            {
                for (int y = 0; y < _boardHeight; y++)
                {
                    var pixel = await GetPixelAsync(x, y);
                    pixels[x, y] = pixel ?? new PixelColor { X = x, Y = y };
                }
            }

            stopwatch.Stop();
            Console.WriteLine($"Sequenziell: {stopwatch.ElapsedMilliseconds}ms für {_boardWidth * _boardHeight} Pixel");

            return pixels;
        }
        public async Task<PixelColor[,]> GetBoardParallelAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            var pixels = new PixelColor[_boardWidth, _boardHeight];
            var tasks = new List<Task>();

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
                    }));
                }
            }

            await Task.WhenAll(tasks);

            stopwatch.Stop();
            Console.WriteLine($"Parallel: {stopwatch.ElapsedMilliseconds}ms für {_boardWidth * _boardHeight} Pixel");

            return pixels;
        }
    }
}
