using ImageSearchBot.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Exceptions;
using ImageSearchBot.Services;

namespace ImageSearchBot.Bot;

public class BotHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly IImageSearchService _imageSearch;
    private readonly ILogger _logger;
    private readonly IUserSettingsService _userSettings;
    public BotHandler(ITelegramBotClient bot, IImageSearchService imageSearch, ILogger logger, IUserSettingsService userSettings)
    {
        _bot = bot;
        _imageSearch = imageSearch;
        _logger = logger;
        _userSettings = userSettings;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Message is not { Text: var text } message) return;
        
        _logger.LogInfo($"Получена команда от {message.From?.Username ?? message.From?.FirstName ?? "Unknown"}: {text}");

        try
        {
            if (text.StartsWith("/start"))
            {
                await HandleStartCommand(message, ct);
            }
            else if (text.StartsWith("/help"))
            {
                await HandleHelpCommand(message, ct);
            }
            else if (text.StartsWith("/image "))
            {
                await HandleImageCommand(message, text, ct);
            }
            else if (text.StartsWith("/random"))
            {
                await HandleRandomCommand(message, text, ct);
            }
            else if (text.StartsWith("/tags"))
            {
                await HandleTagsCommand(message, ct);
            }
            else if (text.StartsWith("/settings"))
            {
                await HandleSettingsCommand(message, text, ct);
            }
            else if (text.StartsWith("/fav"))
            {
                await HandleFavoriteCommand(message, text, ct);
            }
            else if (text.StartsWith("/alias"))
            {
                await HandleAliasCommand(message, text, ct);
            }
            else
            {
                await HandleUnknownCommand(message, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Ошибка при обработке команды: {text}", ex);
            await _bot.SendTextMessageAsync(message.Chat.Id, "Произошла ошибка при обработке команды. Попробуйте позже.", cancellationToken: ct);
        }
    }

    private async Task HandleStartCommand(Message message, CancellationToken ct)
    {
        const string welcomeText = "👋 Привет! Я бот для поиска изображений.\n\n" +
                               "📝 Доступные команды:\n" +
                               "/image [кол-во] <тег> [sfw|nsfw] - найти изображения по тегу\n" +
                               "/random [кол-во] [sfw|nsfw] - случайные изображения\n" +
                               "/tags - показать алиасы тегов\n" +
                               "/help - показать справку";
        
        await _bot.SendTextMessageAsync(message.Chat.Id, welcomeText, cancellationToken: ct);
    }

    private async Task HandleHelpCommand(Message message, CancellationToken ct)
    {
        const string helpText = "📚 Справка по командам:\n\n" +
                               "🖼️ /image [кол-во] <тег> [sfw|nsfw] - найти изображения по тегу\n" +
                               "Примеры:\n" +
                               "/image cat - 1 изображение\n" +
                               "/image 3 cat - 3 изображения\n" +
                               "/image cat sfw - только SFW\n" +
                               "/image 2 cat nsfw - 2 NSFW изображения\n\n" +
                               "🎲 /random [кол-во] [sfw|nsfw] - случайные изображения\n" +
                               "Примеры:\n" +
                               "/random - 3 случайных\n" +
                               "/random 5 - 5 случайных\n" +
                               "/random sfw - 3 SFW случайных\n" +
                               "/random 2 nsfw - 2 NSFW случайных\n\n" +
                               "🔍 Фильтры:\n" +
                               "sfw - только безопасный контент\n" +
                               "nsfw - только взрослый контент\n\n" +
                               "🔥 Новое: расширенный поиск!\n" +
                               "/image cat dog - несколько тегов\n" +
                               "/image cat -dog - исключение тегов\n" +
                               "/settings - персональные настройки";
        
        await _bot.SendTextMessageAsync(message.Chat.Id, helpText, cancellationToken: ct);
    }

    private async Task HandleImageCommand(Message message, string text, CancellationToken ct)
    {
        var parts = text[7..].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length == 0)
        {
            await _bot.SendTextMessageAsync(message.Chat.Id, "⚠️ Укажите тег для поиска.\nПримеры:\n/image cat\n/image 3 cat\n/image cat sfw\n/image 2 cat nsfw", cancellationToken: ct);
            return;
        }

        var (count, tags, filter) = ParseImageCommand(parts);
        
        if (count <= 0 || count > 10)
        {
            await _bot.SendTextMessageAsync(message.Chat.Id, "⚠️ Количество должно быть от 1 до 10", cancellationToken: ct);
            return;
        }

        var countText = count == 1 ? "изображение" : $"{count} изображений";
        await _bot.SendTextMessageAsync(message.Chat.Id, $"🔍 Ищу {countText}...", cancellationToken: ct);
        
        var userId = message.From?.Id ?? 0;
        var images = await _imageSearch.GetImagesByTagAsync(tags, count, filter, userId);
        if (images.Count == 0)
        {
            var filterText = GetFilterText(filter);
            await _bot.SendTextMessageAsync(message.Chat.Id, $"😔 Картинки по тегу '{tags}'{filterText} не найдены", cancellationToken: ct);
            return;
        }

        for (int i = 0; i < images.Count; i++)
        {
            var filterText = GetFilterText(filter);
            var caption = images.Count == 1 
                ? $"🏷️ Тег: {tags}{filterText}" 
                : $"🏷️ Тег: {tags}{filterText} ({i + 1}/{images.Count})";
            
            await _bot.SendPhotoAsync(message.Chat.Id, images[i], caption: caption, cancellationToken: ct);
        }
    }

    private async Task HandleRandomCommand(Message message, string text, CancellationToken ct)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
        var (count, filter) = ParseRandomCommand(parts);

        if (count <= 0 || count > 10)
        {
            await _bot.SendTextMessageAsync(message.Chat.Id, "⚠️ Количество должно быть от 1 до 10", cancellationToken: ct);
            return;
        }

        await _bot.SendTextMessageAsync(message.Chat.Id, $"🎲 Ищу {count} случайных изображений...", cancellationToken: ct);

        var userId = message.From?.Id ?? 0;
        var images = await _imageSearch.GetRandomImagesAsync(count, filter, userId);
        if (images.Count == 0)
        {
            var filterText = GetFilterText(filter);
            await _bot.SendTextMessageAsync(message.Chat.Id, $"😔 Случайные картинки{filterText} не найдены", cancellationToken: ct);
            return;
        }

        for (int i = 0; i < images.Count; i++)
        {
            var filterText = GetFilterText(filter);
            var caption = $"🎲 Случайное изображение{filterText} {i + 1}/{images.Count}";
            
            await _bot.SendPhotoAsync(message.Chat.Id, images[i], caption: caption, cancellationToken: ct);
        }
    }

    private async Task HandleTagsCommand(Message message, CancellationToken ct)
    {
        const string tagsText = "🏷️ Популярные алиасы тегов:\n\n" +
                               "🎮 Игры:\n" +
                               "zzz → zenless_zone_zero\n" +
                               "genshin → genshin_impact\n" +
                               "hsr → honkai:_star_rail\n" +
                               "fgo → fate/grand_order\n" +
                               "ba → blue_archive\n\n" +
                               "📺 Аниме:\n" +
                               "jjk → jujutsu_kaisen\n" +
                               "aot → shingeki_no_kyojin\n" +
                               "mha → boku_no_hero_academia\n" +
                               "ds → kimetsu_no_yaiba\n" +
                               "op → one_piece\n\n" +
                               "👩 Персонажи:\n" +
                               "miku → hatsune_miku\n" +
                               "rem → rem_(re:zero)\n" +
                               "asuka → souryuu_asuka_langley\n\n" +
                               "✨ Общие:\n" +
                               "waifu → 1girl\n" +
                               "husbando → 1boy\n" +
                               "kawaii → cute";
        
        await _bot.SendTextMessageAsync(message.Chat.Id, tagsText, cancellationToken: ct);
    }

    private async Task HandleUnknownCommand(Message message, CancellationToken ct)
    {
        const string helpText = "❓ Неизвестная команда.\n\n" +
                               "📝 Доступные команды:\n" +
                               "/start - начать работу\n" +
                               "/image [кол-во] <тег> [sfw|nsfw] - найти изображения\n" +
                               "/random [кол-во] [sfw|nsfw] - случайные изображения\n" +
                               "/tags - показать алиасы тегов\n" +
                               "/help - показать справку";
        
        await _bot.SendTextMessageAsync(message.Chat.Id, helpText, cancellationToken: ct);
    }

    private (int count, string tags, ContentFilter filter) ParseImageCommand(string[] parts)
    {
        var count = 1;
        var tags = "";
        var filter = ContentFilter.All;
        var tagParts = new List<string>();

        foreach (var part in parts)
        {
            if (int.TryParse(part, out var num) && count == 1)
            {
                count = Math.Min(num, 10);
            }
            else if (part.Equals("sfw", StringComparison.OrdinalIgnoreCase) || part.Equals("swf", StringComparison.OrdinalIgnoreCase))
            {
                filter = ContentFilter.SfwOnly;
            }
            else if (part.Equals("nsfw", StringComparison.OrdinalIgnoreCase))
            {
                filter = ContentFilter.NsfwOnly;
            }
            else
            {
                tagParts.Add(part);
            }
        }

        tags = string.Join(" ", tagParts);
        return (count, tags, filter);
    }

    private (int count, ContentFilter filter) ParseRandomCommand(string[] parts)
    {
        var count = 3;
        var filter = ContentFilter.All;

        foreach (var part in parts)
        {
            if (int.TryParse(part, out var num) && count == 3)
            {
                count = Math.Min(num, 10);
            }
            else if (part.Equals("sfw", StringComparison.OrdinalIgnoreCase) || part.Equals("swf", StringComparison.OrdinalIgnoreCase))
            {
                filter = ContentFilter.SfwOnly;
            }
            else if (part.Equals("nsfw", StringComparison.OrdinalIgnoreCase))
            {
                filter = ContentFilter.NsfwOnly;
            }
        }

        return (count, filter);
    }

    private async Task HandleSettingsCommand(Message message, string text, CancellationToken ct)
    {
        var userId = message.From?.Id ?? 0;
        if (userId == 0) return;

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            var settings = await _userSettings.GetUserSettingsAsync(userId);
            var settingsText = $"⚙️ Ваши настройки:\n\n" +
                              $"🔍 Фильтр по умолчанию: {GetFilterName(settings.DefaultFilter)}\n" +
                              $"⭐ Избранных тегов: {settings.FavoriteTags.Count}\n" +
                              $"🏷️ Пользовательских алиасов: {settings.CustomAliases.Count}\n\n" +
                              $"Команды:\n" +
                              $"/settings filter <sfw|nsfw|all> - установить фильтр\n" +
                              $"/fav add <тег> - добавить в избранное\n" +
                              $"/fav list - показать избранное\n" +
                              $"/alias add <алиас> <тег> - добавить алиас";
            
            await _bot.SendTextMessageAsync(message.Chat.Id, settingsText, cancellationToken: ct);
            return;
        }

        if (parts[1] == "filter" && parts.Length == 3)
        {
            var filter = parts[2].ToLowerInvariant() switch
            {
                "sfw" => ContentFilter.SfwOnly,
                "nsfw" => ContentFilter.NsfwOnly,
                "all" => ContentFilter.All,
                _ => (ContentFilter?)null
            };

            if (filter.HasValue)
            {
                await _userSettings.SetDefaultFilterAsync(userId, filter.Value);
                await _bot.SendTextMessageAsync(message.Chat.Id, $"✅ Фильтр по умолчанию установлен: {GetFilterName(filter.Value)}", cancellationToken: ct);
            }
            else
            {
                await _bot.SendTextMessageAsync(message.Chat.Id, "❌ Неверный фильтр. Используйте: sfw, nsfw или all", cancellationToken: ct);
            }
        }
    }

    private async Task HandleFavoriteCommand(Message message, string text, CancellationToken ct)
    {
        var userId = message.From?.Id ?? 0;
        if (userId == 0) return;

        var parts = text.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            await _bot.SendTextMessageAsync(message.Chat.Id, "⭐ Команды избранного:\n/fav add <тег> - добавить\n/fav remove <тег> - удалить\n/fav list - показать список", cancellationToken: ct);
            return;
        }

        switch (parts[1])
        {
            case "add" when parts.Length == 3:
                await _userSettings.AddFavoriteTagAsync(userId, parts[2]);
                await _bot.SendTextMessageAsync(message.Chat.Id, $"⭐ Тег '{parts[2]}' добавлен в избранное", cancellationToken: ct);
                break;
                
            case "remove" when parts.Length == 3:
                await _userSettings.RemoveFavoriteTagAsync(userId, parts[2]);
                await _bot.SendTextMessageAsync(message.Chat.Id, $"❌ Тег '{parts[2]}' удален из избранного", cancellationToken: ct);
                break;
                
            case "list":
                var settings = await _userSettings.GetUserSettingsAsync(userId);
                if (settings.FavoriteTags.Count == 0)
                {
                    await _bot.SendTextMessageAsync(message.Chat.Id, "📝 У вас нет избранных тегов", cancellationToken: ct);
                }
                else
                {
                    var favText = "⭐ Ваши избранные теги:\n" + string.Join("\n", settings.FavoriteTags.Select(tag => $"• {tag}"));
                    await _bot.SendTextMessageAsync(message.Chat.Id, favText, cancellationToken: ct);
                }
                break;
        }
    }

    private async Task HandleAliasCommand(Message message, string text, CancellationToken ct)
    {
        var userId = message.From?.Id ?? 0;
        if (userId == 0) return;

        var parts = text.Split(' ', 4, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            await _bot.SendTextMessageAsync(message.Chat.Id, "🏷️ Команды алиасов:\n/alias add <алиас> <тег> - добавить алиас\n/alias list - показать список", cancellationToken: ct);
            return;
        }

        switch (parts[1])
        {
            case "add" when parts.Length == 4:
                await _userSettings.AddCustomAliasAsync(userId, parts[2], parts[3]);
                await _bot.SendTextMessageAsync(message.Chat.Id, $"🏷️ Алиас '{parts[2]}' → '{parts[3]}' добавлен", cancellationToken: ct);
                break;
                
            case "list":
                var settings = await _userSettings.GetUserSettingsAsync(userId);
                if (settings.CustomAliases.Count == 0)
                {
                    await _bot.SendTextMessageAsync(message.Chat.Id, "📝 У вас нет пользовательских алиасов", cancellationToken: ct);
                }
                else
                {
                    var aliasText = "🏷️ Ваши алиасы:\n" + string.Join("\n", settings.CustomAliases.Select(kvp => $"• {kvp.Key} → {kvp.Value}"));
                    await _bot.SendTextMessageAsync(message.Chat.Id, aliasText, cancellationToken: ct);
                }
                break;
        }
    }

    private string GetFilterText(ContentFilter filter)
    {
        return filter switch
        {
            ContentFilter.SfwOnly => " (SFW)",
            ContentFilter.NsfwOnly => " (NSFW)",
            _ => ""
        };
    }



    private string GetFilterName(ContentFilter filter)
    {
        return filter switch
        {
            ContentFilter.SfwOnly => "SFW только",
            ContentFilter.NsfwOnly => "NSFW только",
            _ => "Все"
        };
    }

    public Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken ct)
    {
        var msg = exception switch
        {
            ApiRequestException apiEx => $"Telegram API Error: [{apiEx.ErrorCode}] {apiEx.Message}",
            _ => exception.ToString()
        };

        _logger.LogError($"Ошибка Telegram Bot API: {msg}", exception);
        return Task.CompletedTask;
    }
}
