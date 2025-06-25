namespace ImageSearchBot.Services;

public class TagDictionaryService : ITagDictionaryService
{
    private readonly Dictionary<string, string> _tagDictionary;
    private readonly ILogger _logger;

    public TagDictionaryService(ILogger logger)
    {
        _logger = logger;
        _tagDictionary = InitializeDictionary();
    }

    public string ExpandTags(string tags, Dictionary<string, string>? customAliases = null)
    {
        if (string.IsNullOrWhiteSpace(tags))
            return tags;

        var words = tags.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var expandedWords = new List<string>();
        var hasExpansions = false;

        foreach (var word in words)
        {
            var lowerWord = word.ToLowerInvariant();
            
            // Сначала проверяем пользовательские алиасы
            if (customAliases?.TryGetValue(lowerWord, out var customTag) == true)
            {
                expandedWords.Add(customTag);
                hasExpansions = true;
                _logger.LogInfo($"Расширен пользовательский тег: '{word}' -> '{customTag}'");
            }
            // Затем глобальные алиасы
            else if (_tagDictionary.TryGetValue(lowerWord, out var expandedTag))
            {
                expandedWords.Add(expandedTag);
                hasExpansions = true;
                _logger.LogInfo($"Расширен тег: '{word}' -> '{expandedTag}'");
            }
            else
            {
                expandedWords.Add(word);
            }
        }

        var result = string.Join(" ", expandedWords);
        if (hasExpansions)
        {
            _logger.LogInfo($"Исходные теги: '{tags}' -> Расширенные: '{result}'");
        }

        return result;
    }

    private Dictionary<string, string> InitializeDictionary()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Игры
            ["zzz"] = "zenless_zone_zero",
            ["genshin"] = "genshin_impact",
            ["hsr"] = "honkai:_star_rail",
            ["hi3"] = "honkai_impact_3rd",
            ["fgo"] = "fate/grand_order",
            ["azur"] = "azur_lane",
            ["arknights"] = "arknights",
            ["ba"] = "blue_archive",
            ["nikke"] = "goddess_of_victory:_nikke",
            ["ff14"] = "final_fantasy_xiv",
            ["lol"] = "league_of_legends",
            ["ow"] = "overwatch",
            ["valorant"] = "valorant",
            
            // Аниме/Манга
            ["jjk"] = "jujutsu_kaisen",
            ["aot"] = "shingeki_no_kyojin",
            ["mha"] = "boku_no_hero_academia",
            ["ds"] = "kimetsu_no_yaiba",
            ["op"] = "one_piece",
            ["naruto"] = "naruto_(series)",
            ["bleach"] = "bleach_(series)",
            ["dbz"] = "dragon_ball_z",
            ["eva"] = "neon_genesis_evangelion",
            ["fma"] = "fullmetal_alchemist",
            
            // Персонажи (популярные сокращения)
            ["miku"] = "hatsune_miku",
            ["rem"] = "rem_(re:zero)",
            ["zero_two"] = "zero_two_(darling_in_the_franxx)",
            ["asuka"] = "souryuu_asuka_langley",
            ["rei"] = "ayanami_rei",
            
            // Общие теги
            ["waifu"] = "1girl",
            ["husbando"] = "1boy",
            ["cute"] = "cute",
            ["sexy"] = "sexy",
            ["kawaii"] = "cute",
            
            // Художественные стили
            ["pixel"] = "pixel_art",
            ["chibi"] = "chibi",
            ["realistic"] = "realistic",
            
            // Цвета волос (популярные)
            ["pink_hair"] = "pink_hair",
            ["blue_hair"] = "blue_hair",
            ["white_hair"] = "white_hair",
            ["silver_hair"] = "silver_hair"
        };
    }
}