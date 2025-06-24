using System.Text.Json;
using System.Text.Json.Serialization;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using File = System.IO.File;

namespace ImageSearchBot;

class Program
{
    static async Task Main()
    {
        var telegramToken = "";

        try
        {
            var       json = File.ReadAllText("config.json");
            using var doc  = JsonDocument.Parse(json);
            telegramToken = doc.RootElement.GetProperty("telegram_token").GetString()!;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Не удалось загрузить токен из config.json: " + ex.Message);
            return;
        }

        var       botClient = new TelegramBotClient(telegramToken);
        using var cts       = new CancellationTokenSource();

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            cancellationToken: cts.Token
        );

        var me = await botClient.GetMeAsync();
        Console.WriteLine($"Бот @{me.Username} запущен");

        Console.ReadLine();
        cts.Cancel();
    }

    static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Message is not { Text: var messageText } message)
            return;

        Console.WriteLine($"Получено сообщение: {messageText}");

        if (messageText.StartsWith("/image "))
        {
            var tag = messageText[7..].Trim();
            Console.WriteLine($"Запрос картинки по тегу: {tag}");

            var imageUrl = await FetchImageUrl(tag);
            Console.WriteLine($"URL картинки: {imageUrl ?? "не найден"}");

            if (imageUrl != null)
            {
                try
                {
                    using var httpClient = new HttpClient();
                    var       stream     = await httpClient.GetStreamAsync(imageUrl);

                    await bot.SendPhotoAsync(
                        chatId: message.Chat.Id,
                        photo: new InputOnlineFile(stream, "image.jpg"),
                        cancellationToken: ct);

                    Console.WriteLine("Картинка отправлена ✅");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при отправке картинки: {ex.Message}");
                    await bot.SendTextMessageAsync(message.Chat.Id, "Произошла ошибка при отправке изображения 😢",
                                                   cancellationToken: ct);
                }
            }
            else
            {
                await bot.SendTextMessageAsync(message.Chat.Id, "Картинка не найдена 🙁", cancellationToken: ct);
            }
        }
        else if (messageText.StartsWith("/random"))
        {
            var tagPart   = messageText[7..].Trim();
            var tags      = string.Join("+", tagPart.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            var imageUrls = await FetchRandomImages(tags, 3);

            if (imageUrls.Count > 0)
            {
                foreach (var url in imageUrls)
                {
                    try
                    {
                        using var httpClient = new HttpClient();
                        var       stream     = await httpClient.GetStreamAsync(url);

                        await bot.SendPhotoAsync(
                            chatId: message.Chat.Id,
                            photo: new InputOnlineFile(stream, "image.jpg"),
                            cancellationToken: ct);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка при отправке картинки: {ex.Message}");
                    }
                }
            }
            else
            {
                await bot.SendTextMessageAsync(message.Chat.Id, "Картинки не найдены 🙁", cancellationToken: ct);
            }
        }
        else
        {
            await bot.SendTextMessageAsync(message.Chat.Id,
                                           "Напиши /image <тег> или /random <тег?>, чтобы найти картинки",
                                           cancellationToken: ct);
        }
    }

    static Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken ct)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiEx => $"Telegram API Error:\n[{apiEx.ErrorCode}] {apiEx.Message}",
            _                         => exception.ToString()
        };
        Console.WriteLine(errorMessage);
        return Task.CompletedTask;
    }

    static async Task<string?> FetchImageUrl(string tag)
    {
        var url = $"https://yande.re/post.json?tags={Uri.EscapeDataString(tag)}&limit=1";

        using var client = new HttpClient();
        try
        {
            var json = await client.GetStringAsync(url);
            var data = JsonSerializer.Deserialize<List<Post>>(json);

            if (data != null && data.Count > 0)
            {
                var result = data[0].FileUrl;
                return !string.IsNullOrWhiteSpace(result) && result.StartsWith("http") ? result : null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка загрузки картинки: {ex.Message}");
        }

        return null;
    }

    private static async Task<List<string>> FetchRandomImages(string tags, int count)
    {
        var tagQuery = string.IsNullOrWhiteSpace(tags) ? "" : $"&tags={Uri.EscapeDataString(tags)}";
        var url      = $"https://yande.re/post.json?limit=50{tagQuery}";

        using var client = new HttpClient();
        var       urls   = new List<string>();

        try
        {
            var json = await client.GetStringAsync(url);
            var data = JsonSerializer.Deserialize<Post[]>(json);

            if (data is { Length: > 0 })
            {
                var rnd      = new Random();
                var shuffled = data.OrderBy(_ => rnd.Next()).Take(count);
                urls.AddRange(shuffled
                             .Where(p => !string.IsNullOrWhiteSpace(p.FileUrl) && p.FileUrl.StartsWith("http"))
                             .Select(p => p.FileUrl));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка загрузки: {ex.Message}");
        }

        return urls;
    }

    class Post
    {
        [JsonPropertyName("file_url")]
        public string FileUrl { get; set; } = "";
        
        [JsonPropertyName("id")]
        public int    Id      { get; set; }
        
        [JsonPropertyName("tags")]
        public string Tags    { get; set; } = "";
    }
}