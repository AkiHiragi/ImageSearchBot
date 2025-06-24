using System.Text.Json.Serialization;

namespace ImageSearchBot.Models;

public class Post
{
    [JsonPropertyName("file_url")]
    public string FileUrl { get; set; } = "";
        
    [JsonPropertyName("id")]
    public int    Id      { get; set; }
        
    [JsonPropertyName("tags")]
    public string Tags    { get; set; } = "";
}