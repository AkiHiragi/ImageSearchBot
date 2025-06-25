using ImageSearchBot.Models;

namespace ImageSearchBot.Services;

public interface IUserSettingsService
{
    Task<UserSettings> GetUserSettingsAsync(long userId);
    Task SaveUserSettingsAsync(UserSettings settings);
    Task SetDefaultFilterAsync(long userId, ContentFilter filter);
    Task AddFavoriteTagAsync(long userId, string tag);
    Task RemoveFavoriteTagAsync(long userId, string tag);
    Task AddCustomAliasAsync(long userId, string alias, string tag);
}