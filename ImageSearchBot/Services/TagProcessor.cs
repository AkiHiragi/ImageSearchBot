namespace ImageSearchBot.Services;

public static class TagProcessor
{
    public static string ProcessAdvancedTags(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var includeTags = new List<string>();
        var excludeTags = new List<string>();

        foreach (var part in parts)
        {
            if (part.StartsWith('-') && part.Length > 1)
            {
                excludeTags.Add(part[1..]);
            }
            else
            {
                includeTags.Add(part);
            }
        }

        var result = string.Join(" ", includeTags);
        
        if (excludeTags.Count > 0)
        {
            var excludeString = string.Join(" ", excludeTags.Select(tag => $"-{tag}"));
            result = string.IsNullOrWhiteSpace(result) ? excludeString : $"{result} {excludeString}";
        }

        return result;
    }
}