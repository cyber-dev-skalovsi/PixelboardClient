using PixelboardClient.Models;

namespace PixelboardClient.Services
{
    public interface IBoardStateService
    {
        PixelColor? Pixel(int x, int y);
        PixelColor[,] GetAllPixels();
        
        Task<(PixelColor[,] pixels, long elapsedMs)> LoadPixelsParallelAsync();
        Task<(PixelColor[,] pixels, long elapsedMs)> LoadPixelsSequentialAsync();
        Task<(PixelColor[,] pixels, long elapsedMs)> LoadPixelsCachedAsync();
    }
}
