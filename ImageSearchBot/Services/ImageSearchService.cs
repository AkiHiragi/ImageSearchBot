using ImageSearchBot.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Text.Json;

namespace ImageSearchBot.Services;

public class ImageSearchService
{
    private readonly HttpClient _http;

    public ImageSearchService(HttpClient http)
    {
        _http = http;
    }

    public async Task<Stream?> GetImageByTagAsync(string tag)
    {
        Console.WriteLine($"Поиск изображения по тегу: '{tag}'");
        
        for (int attempt = 0; attempt < 3; attempt++)
        {
            int maxPage = attempt == 0 ? await GetMaxPageCount(tag) : 50;
            int randomPage = new Random().Next(1, Math.Max(2, maxPage / 2));
            
            Console.WriteLine($"Попытка {attempt+1}: Страница {randomPage} из {maxPage}");
            
            var url = $"https://yande.re/post.json?tags={Uri.EscapeDataString(tag)}&page={randomPage}&limit=20";
            
            try
            {
                var json = await _http.GetStringAsync(url);
                var posts = JsonSerializer.Deserialize<Post[]>(json);
                
                if (posts is { Length: > 0 })
                {
                    var randomPost = posts.OrderBy(_ => Guid.NewGuid()).First();
                    Console.WriteLine($"Выбран пост с URL: {randomPost.FileUrl}");
                    
                    var result = await DownloadAndResizeImage(randomPost.FileUrl);
                    Console.WriteLine($"Результат обработки изображения: {(result != null ? "успех" : "ошибка")}");
                    
                    return result;
                }
                else
                {
                    Console.WriteLine("Посты не найдены на выбранной странице, пробуем другую");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка получения изображения: {ex.Message}");
            }
        }
        
        Console.WriteLine("Не удалось найти изображение после нескольких попыток");
        return null;
    }

    public async Task<List<Stream>> GetRandomImagesAsync(string tags, int count)
    {
        var result = new List<Stream>();
        
        for (int attempt = 0; attempt < 3; attempt++)
        {
            int maxPage = attempt == 0 ? await GetMaxPageCount(tags) : 50;
            int randomPage = new Random().Next(1, Math.Max(2, maxPage / 2));
            
            Console.WriteLine($"Попытка {attempt+1} получения случайных изображений: Страница {randomPage} из {maxPage}");
            
            var url = $"https://yande.re/post.json?tags={Uri.EscapeDataString(tags)}&page={randomPage}&limit=20";

            try
            {
                var json = await _http.GetStringAsync(url);
                var posts = JsonSerializer.Deserialize<Post[]>(json);
                
                if (posts is { Length: > 0 })
                {
                    var random = posts.OrderBy(_ => Guid.NewGuid()).Take(count);
                    foreach (var post in random)
                    {
                        var stream = await DownloadAndResizeImage(post.FileUrl);
                        if (stream != null) result.Add(stream);
                    }
                    
                    if (result.Count > 0)
                    {
                        Console.WriteLine($"Успешно получено {result.Count} случайных изображений");
                        return result;
                    }
                    
                    Console.WriteLine("Не удалось загрузить ни одного изображения, пробуем другую страницу");
                }
                else
                {
                    Console.WriteLine("Посты не найдены на выбранной странице, пробуем другую");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка получения случайных изображений: {ex.Message}");
            }
        }

        return result;
    }

    private async Task<Stream?> DownloadAndResizeImage(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("http"))
            return null;

        try
        {
            await using var stream = await _http.GetStreamAsync(url);
            using var image = await Image.LoadAsync(stream);
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(1280, 1280),
                Mode = ResizeMode.Max
            }));

            var ms = new MemoryStream();
            await image.SaveAsJpegAsync(ms);
            ms.Position = 0;
            return ms;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка обработки изображения {url}: {ex.Message}");
            return null;
        }
    }

    private async Task<int> GetMaxPageCount(string tag)
    {
        try
        {
            var checkUrl = $"https://yande.re/post.json?tags={Uri.EscapeDataString(tag)}&page=1&limit=1";
            var checkJson = await _http.GetStringAsync(checkUrl);
            var checkPosts = JsonSerializer.Deserialize<Post[]>(checkJson);
            
            if (checkPosts is not { Length: > 0 })
            {
                Console.WriteLine($"Для тега '{tag}' не найдено постов");
                return 1;
            }
            
            Console.WriteLine($"Для тега '{tag}' установлен лимит в 50 страниц");
            return 50;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка определения страниц для '{tag}': {ex.Message}");
            return 10;
        }
    }
}