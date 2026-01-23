using PixelboardClient.Models;

namespace PixelboardClient.Services
{
    public interface IBoardStateService
    {
        PixelColor? Pixel(int x, int y);
        PixelColor[,] GetAllPixels();
    }
}
