using System.Text.Json.Serialization;

namespace ImageSearchBot.Models;

public class BotConfig
{
    [JsonPropertyName("telegram_token")]
    public string TelegramToken { get; set; } = "";
    
    [JsonPropertyName("max_image_size")]
    public int MaxImageSize { get; set; } = 1280;
    
    [JsonPropertyName("max_attempts")]
    public int MaxAttempts { get; set; } = 3;
    
    [JsonPropertyName("default_page_limit")]
    public int DefaultPageLimit { get; set; } = 50;
    
    [JsonPropertyName("images_per_page")]
    public int ImagesPerPage { get; set; } = 20;
}