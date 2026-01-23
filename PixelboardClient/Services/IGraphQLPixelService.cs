using PixelboardClient.Models;

namespace PixelboardClient.Services
{
    public interface IGraphQLPixelService
    {
        Task<PixelColor[,]> LoadAllPixelsAsync();
    }
}
