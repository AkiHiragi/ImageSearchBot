using ImageSearchBot.Models;
using System.Text.Json;

namespace ImageSearchBot.Services;

public class UserSettingsService : IUserSettingsService
{
    private readonly string _settingsPath = "user_settings.json";
    private readonly Dictionary<long, UserSettings> _cache = new();
    private readonly ILogger _logger;

    public UserSettingsService(ILogger logger)
    {
        _logger = logger;
        LoadSettings();
    }

    public async Task<UserSettings> GetUserSettingsAsync(long userId)
    {
        if (_cache.TryGetValue(userId, out var settings))
            return settings;

        settings = new UserSettings { UserId = userId };
        _cache[userId] = settings;
        return settings;
    }

    public async Task SaveUserSettingsAsync(UserSettings settings)
    {
        _cache[settings.UserId] = settings;
        await SaveToFile();
    }

    public async Task SetDefaultFilterAsync(long userId, ContentFilter filter)
    {
        var settings = await GetUserSettingsAsync(userId);
        settings.DefaultFilter = filter;
        await SaveUserSettingsAsync(settings);
        _logger.LogInfo($"Пользователь {userId} установил фильтр по умолчанию: {filter}");
    }

    public async Task AddFavoriteTagAsync(long userId, string tag)
    {
        var settings = await GetUserSettingsAsync(userId);
        if (!settings.FavoriteTags.Contains(tag))
        {
            settings.FavoriteTags.Add(tag);
            await SaveUserSettingsAsync(settings);
            _logger.LogInfo($"Пользователь {userId} добавил в избранное тег: {tag}");
        }
    }

    public async Task RemoveFavoriteTagAsync(long userId, string tag)
    {
        var settings = await GetUserSettingsAsync(userId);
        if (settings.FavoriteTags.Remove(tag))
        {
            await SaveUserSettingsAsync(settings);
            _logger.LogInfo($"Пользователь {userId} удалил из избранного тег: {tag}");
        }
    }

    public async Task AddCustomAliasAsync(long userId, string alias, string tag)
    {
        var settings = await GetUserSettingsAsync(userId);
        settings.CustomAliases[alias.ToLowerInvariant()] = tag;
        await SaveUserSettingsAsync(settings);
        _logger.LogInfo($"Пользователь {userId} добавил алиас: {alias} -> {tag}");
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var allSettings = JsonSerializer.Deserialize<UserSettings[]>(json) ?? Array.Empty<UserSettings>();
                foreach (var settings in allSettings)
                {
                    _cache[settings.UserId] = settings;
                }
                _logger.LogInfo($"Загружены настройки для {_cache.Count} пользователей");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Ошибка загрузки пользовательских настроек", ex);
        }
    }

    private async Task SaveToFile()
    {
        try
        {
            var allSettings = _cache.Values.ToArray();
            var json = JsonSerializer.Serialize(allSettings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError("Ошибка сохранения пользовательских настроек", ex);
        }
    }
}