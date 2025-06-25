using ImageSearchBot.Bot;
using ImageSearchBot.Models;
using ImageSearchBot.Services;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using System.Text.Json;

class Program
{
    public static async Task Main()
    {
        var logger = new ConsoleLogger();
        BotConfig config;

        try
        {
            var configJson = await File.ReadAllTextAsync("config.json");
            config = JsonSerializer.Deserialize<BotConfig>(configJson) ?? throw new InvalidOperationException("Не удалось десериализовать конфигурацию");
            
            if (string.IsNullOrWhiteSpace(config.TelegramToken))
                throw new InvalidOperationException("Токен Telegram не указан в конфигурации");
        }
        catch (Exception ex)
        {
            logger.LogError($"Ошибка загрузки конфигурации: {ex.Message}", ex);
            return;
        }

        var botClient = new TelegramBotClient(config.TelegramToken);
        var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        var tagDictionaryService = new TagDictionaryService(logger);
        var userSettingsService = new UserSettingsService(logger);
        var imageSearchService = new ImageSearchService(httpClient, config, logger, tagDictionaryService, userSettingsService);
        var botHandler = new BotHandler(botClient, imageSearchService, logger, userSettingsService);

        using var cts = new CancellationTokenSource();
        var options = new ReceiverOptions 
        { 
            AllowedUpdates = new[] { UpdateType.Message },
            ThrowPendingUpdates = true
        };

        try
        {
            botClient.StartReceiving(botHandler.HandleUpdateAsync, botHandler.HandleErrorAsync, options, cts.Token);
            var me = await botClient.GetMeAsync(cts.Token);
            logger.LogInfo($"Бот @{me.Username} успешно запущен");

            Console.WriteLine("Нажмите Enter для остановки бота...");
            Console.ReadLine();
        }
        catch (Exception ex)
        {
            logger.LogError("Критическая ошибка при запуске бота", ex);
        }
        finally
        {
            cts.Cancel();
            httpClient.Dispose();
            logger.LogInfo("Бот остановлен");
        }
    }
}