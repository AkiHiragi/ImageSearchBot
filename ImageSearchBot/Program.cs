using ImageSearchBot.Bot;
using ImageSearchBot.Services;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using System.Text.Json;

class Program
{
    public static async Task Main()
    {
        string token;

        try
        {
            var config = JsonDocument.Parse(File.ReadAllText("config.json"));
            token = config.RootElement.GetProperty("telegram_token").GetString()!;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка загрузки токена: {ex.Message}");
            return;
        }

        var botClient          = new TelegramBotClient(token);
        var httpClient         = new HttpClient();
        var imageSearchService = new ImageSearchService(httpClient);
        var botHandler         = new BotHandler(botClient, imageSearchService);

        using var cts     = new CancellationTokenSource();
        var       options = new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() };

        botClient.StartReceiving(botHandler.HandleUpdateAsync, botHandler.HandleErrorAsync, options, cts.Token);
        var me = await botClient.GetMeAsync();
        Console.WriteLine($"Бот @{me.Username} запущен");

        Console.ReadLine();
        cts.Cancel();
    }
}