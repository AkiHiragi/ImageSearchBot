using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Exceptions;
using ImageSearchBot.Services;

namespace ImageSearchBot.Bot;

public class BotHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly ImageSearchService _imageSearch;

    public BotHandler(ITelegramBotClient bot, ImageSearchService imageSearch)
    {
        _bot = bot;
        _imageSearch = imageSearch;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Message is not { Text: var text } message) return;

        if (text.StartsWith("/image "))
        {
            var tag = text[7..].Trim();
            var stream = await _imageSearch.GetImageByTagAsync(tag);

            if (stream != null)
                await _bot.SendPhotoAsync(message.Chat.Id, stream, cancellationToken: ct);
            else
                await _bot.SendTextMessageAsync(message.Chat.Id, "Картинка не найдена", cancellationToken: ct);
        }
        else if (text.StartsWith("/random"))
        {
            var parts = text.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            var count = parts.Length > 1 && int.TryParse(parts[1], out var c) ? c : 3;
            var tags = parts.Length == 3 ? parts[2] : "";

            var images = await _imageSearch.GetRandomImagesAsync(tags, count);
            if (images.Count == 0)
            {
                await _bot.SendTextMessageAsync(message.Chat.Id, "Картинки не найдены", cancellationToken: ct);
                return;
            }

            foreach (var img in images)
                await _bot.SendPhotoAsync(message.Chat.Id, img, cancellationToken: ct);
        }
        else
        {
            await _bot.SendTextMessageAsync(message.Chat.Id, "Напиши /image <тег> или /random <кол-во> <теги>", cancellationToken: ct);
        }
    }

    public Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken ct)
    {
        var msg = exception switch
        {
            ApiRequestException apiEx => $"Telegram API Error: [{apiEx.ErrorCode}] {apiEx.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine(msg);
        return Task.CompletedTask;
    }
}
