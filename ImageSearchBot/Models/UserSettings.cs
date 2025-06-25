using System.Text.Json.Serialization;

namespace ImageSearchBot.Models;

public class UserSettings
{
    [JsonPropertyName("user_id")]
    public long UserId { get; set; }
    
    [JsonPropertyName("default_filter")]
    public ContentFilter DefaultFilter { get; set; } = ContentFilter.All;
    
    [JsonPropertyName("favorite_tags")]
    public List<string> FavoriteTags { get; set; } = new();
    
    [JsonPropertyName("custom_aliases")]
    public Dictionary<string, string> CustomAliases { get; set; } = new();
}