using ImageSearchBot.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Text.Json;

namespace ImageSearchBot.Services;

public class ImageSearchService : IImageSearchService
{
    private readonly HttpClient _http;
    private readonly BotConfig _config;
    private readonly ILogger _logger;
    private readonly ITagDictionaryService _tagDictionary;
    private const string BaseUrl = "https://yande.re/post.json";

    private readonly IUserSettingsService _userSettings;

    public ImageSearchService(HttpClient http, BotConfig config, ILogger logger, ITagDictionaryService tagDictionary, IUserSettingsService userSettings)
    {
        _http = http;
        _config = config;
        _logger = logger;
        _tagDictionary = tagDictionary;
        _userSettings = userSettings;
    }

    public async Task<List<Stream>> GetImagesByTagAsync(string tag, int count = 1, ContentFilter filter = ContentFilter.All, long userId = 0)
    {
        var result = new List<Stream>();
        
        if (string.IsNullOrWhiteSpace(tag))
        {
            _logger.LogWarning("Попытка поиска с пустым тегом");
            return result;
        }

        if (count <= 0 || count > 10)
        {
            _logger.LogWarning($"Некорректное количество изображений: {count}");
            return result;
        }

        var userSettings = userId > 0 ? await _userSettings.GetUserSettingsAsync(userId) : null;
        var processedTags = TagProcessor.ProcessAdvancedTags(tag);
        var expandedTags = _tagDictionary.ExpandTags(processedTags, userSettings?.CustomAliases);
        
        if (filter == ContentFilter.All && userSettings?.DefaultFilter != ContentFilter.All)
        {
            filter = userSettings.DefaultFilter;
            _logger.LogInfo($"Применен фильтр пользователя по умолчанию: {filter}");
        }
        _logger.LogInfo($"Поиск {count} изображений по тегу: '{expandedTags}' с фильтром: {filter}");
        
        for (int attempt = 0; attempt < _config.MaxAttempts; attempt++)
        {
            var searchTags = ApplyContentFilter(expandedTags, filter);
            int maxPage = attempt == 0 ? await GetMaxPageCount(searchTags) : _config.DefaultPageLimit;
            int randomPage = Random.Shared.Next(1, Math.Max(2, maxPage / 2));
            
            _logger.LogInfo($"Попытка {attempt + 1}/{_config.MaxAttempts}: страница {randomPage} из {maxPage}");
            
            var url = $"{BaseUrl}?tags={Uri.EscapeDataString(searchTags)}&page={randomPage}&limit={_config.ImagesPerPage}";
            
            try
            {
                var posts = await FetchPostsAsync(url);
                if (posts?.Length > 0)
                {
                    var selectedPosts = posts.OrderBy(_ => Guid.NewGuid()).Take(count);
                    var tasks = selectedPosts.Select(async post =>
                    {
                        var stream = await DownloadAndResizeImage(post.FileUrl);
                        return stream;
                    });
                    
                    var streams = await Task.WhenAll(tasks);
                    result.AddRange(streams.Where(s => s != null)!);
                    
                    if (result.Count > 0)
                    {
                        _logger.LogInfo($"Успешно получено {result.Count} изображений");
                        return result;
                    }
                }
                else
                {
                    _logger.LogWarning($"Посты не найдены на странице {randomPage}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка получения изображений на попытке {attempt + 1}", ex);
            }
        }
        
        _logger.LogWarning($"Не удалось найти изображения по тегу '{tag}' после {_config.MaxAttempts} попыток");
        return result;
    }

    public async Task<List<Stream>> GetRandomImagesAsync(int count, ContentFilter filter = ContentFilter.All, long userId = 0)
    {
        var result = new List<Stream>();
        
        if (count <= 0 || count > 10)
        {
            _logger.LogWarning($"Некорректное количество изображений: {count}");
            return result;
        }

        var userSettings = userId > 0 ? await _userSettings.GetUserSettingsAsync(userId) : null;
        
        if (filter == ContentFilter.All && userSettings?.DefaultFilter != ContentFilter.All)
        {
            filter = userSettings.DefaultFilter;
            _logger.LogInfo($"Применен фильтр пользователя по умолчанию: {filter}");
        }
        
        _logger.LogInfo($"Поиск {count} случайных изображений с фильтром: {filter}");
        
        for (int attempt = 0; attempt < _config.MaxAttempts; attempt++)
        {
            var searchTags = ApplyContentFilter("", filter);
            int maxPage = _config.DefaultPageLimit;
            int randomPage = Random.Shared.Next(1, Math.Max(2, maxPage / 2));
            
            _logger.LogInfo($"Попытка {attempt + 1}/{_config.MaxAttempts}: страница {randomPage} из {maxPage}");
            
            var url = $"{BaseUrl}?tags={Uri.EscapeDataString(searchTags)}&page={randomPage}&limit={_config.ImagesPerPage}";

            try
            {
                var posts = await FetchPostsAsync(url);
                if (posts?.Length > 0)
                {
                    var selectedPosts = posts.OrderBy(_ => Guid.NewGuid()).Take(count);
                    var tasks = selectedPosts.Select(async post =>
                    {
                        var stream = await DownloadAndResizeImage(post.FileUrl);
                        return stream;
                    });
                    
                    var streams = await Task.WhenAll(tasks);
                    result.AddRange(streams.Where(s => s != null)!);
                    
                    if (result.Count > 0)
                    {
                        _logger.LogInfo($"Успешно получено {result.Count} случайных изображений");
                        return result;
                    }
                    
                    _logger.LogWarning("Не удалось загрузить ни одного изображения");
                }
                else
                {
                    _logger.LogWarning($"Посты не найдены на странице {randomPage}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка получения случайных изображений на попытке {attempt + 1}", ex);
            }
        }

        _logger.LogWarning($"Не удалось получить случайные изображения после {_config.MaxAttempts} попыток");
        return result;
    }

    private async Task<Stream?> DownloadAndResizeImage(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.IsWellFormedUriString(url, UriKind.Absolute))
        {
            _logger.LogWarning($"Некорректный URL: {url}");
            return null;
        }

        try
        {
            await using var stream = await _http.GetStreamAsync(url);
            using var image = await Image.LoadAsync(stream);
            
            // Оптимизация: изменяем размер только если нужно
            if (image.Width > _config.MaxImageSize || image.Height > _config.MaxImageSize)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(_config.MaxImageSize, _config.MaxImageSize),
                    Mode = ResizeMode.Max
                }));
            }

            var ms = new MemoryStream();
            await image.SaveAsJpegAsync(ms);
            ms.Position = 0;
            return ms;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Ошибка обработки изображения: {url}", ex);
            return null;
        }
    }

    private async Task<Post[]?> FetchPostsAsync(string url)
    {
        try
        {
            var json = await _http.GetStringAsync(url);
            return JsonSerializer.Deserialize<Post[]>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Ошибка получения постов по URL: {url}", ex);
            return null;
        }
    }

    private async Task<int> GetMaxPageCount(string tag)
    {
        try
        {
            var checkUrl = $"{BaseUrl}?tags={Uri.EscapeDataString(tag)}&page=1&limit=1";
            var checkPosts = await FetchPostsAsync(checkUrl);
            
            if (checkPosts is not { Length: > 0 })
            {
                _logger.LogWarning($"Для тега '{tag}' не найдено постов");
                return 1;
            }
            
            _logger.LogInfo($"Для тега '{tag}' установлен лимит в {_config.DefaultPageLimit} страниц");
            return _config.DefaultPageLimit;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Ошибка определения страниц для '{tag}'", ex);
            return 10;
        }
    }

    private string ApplyContentFilter(string tags, ContentFilter filter)
    {
        return filter switch
        {
            ContentFilter.SfwOnly => string.IsNullOrWhiteSpace(tags) ? "rating:safe" : $"{tags} rating:safe",
            ContentFilter.NsfwOnly => string.IsNullOrWhiteSpace(tags) ? "rating:explicit" : $"{tags} rating:explicit",
            _ => tags
        };
    }
}