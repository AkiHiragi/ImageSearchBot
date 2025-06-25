using ImageSearchBot.Models;

namespace ImageSearchBot.Services;

public interface IImageSearchService
{
    Task<List<Stream>> GetImagesByTagAsync(string tag, int count = 1, ContentFilter filter = ContentFilter.All, long userId = 0);
    Task<List<Stream>> GetRandomImagesAsync(int count, ContentFilter filter = ContentFilter.All, long userId = 0);
}