namespace ImageSearchBot.Services;

public interface ITagDictionaryService
{
    string ExpandTags(string tags, Dictionary<string, string>? customAliases = null);
}