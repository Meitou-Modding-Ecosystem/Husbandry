using System.Text.Json;
using System.Text.RegularExpressions;
using Meitou.Localisation;

namespace Husbandry.Tests;

public sealed class LocalizationTests
{
    [Theory]
    [InlineData("fr_FR")]
    [InlineData("de_DE")]
    [InlineData("es_ES")]
    [InlineData("it_IT")]
    [InlineData("pt_BR")]
    [InlineData("ru_RU")]
    [InlineData("ja_JP")]
    [InlineData("ko_KR")]
    [InlineData("zh_CN")]
    public void Catalog_covers_the_same_messages_and_keeps_their_format_arguments(string locale)
    {
        var assembly = typeof(HusbandryMod).Assembly;
        Dictionary<string, string> Read(string code)
        {
            using var stream = assembly.GetManifestResourceStream($"Husbandry.Locale.{code}.json");
            Assert.NotNull(stream);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
        }

        var catalog = Read(locale);
        Assert.Equal(Read("fr_FR").Keys.Order(), catalog.Keys.Order());
        foreach (var (source, translation) in catalog)
        {
            Assert.False(string.IsNullOrWhiteSpace(translation));
            Assert.Equal(Arguments(source), Arguments(translation));
            Assert.Equal(translation, HusbandryText.Lookup(locale, source));
        }

        string[] required =
        [
            HusbandryData.ResearchText, HusbandryData.PostText, "Training Post", "Pet Station", "Bone Pile", "Capsule Bench",
            "Hitch here", "Fetch for here", "Guard here", "Hunt from here", "Train: {1}", "Training {1}",
            "Fetch", "Guard", "Hunt", "Scout", "Pack", "Tie up", "Show me your beasts.", "Show me your packs.",
        ];
        Assert.All(required, source => Assert.True(catalog.ContainsKey(source), source));
        Assert.NotEqual("Hitch here", HusbandryText.Lookup(locale, "Hitch here"));
        Assert.Contains("Milo", Loc.Format(HusbandryText.Lookup(locale, "Caught {1}!"), "Milo"));
        Assert.Equal("Hitch here", HusbandryText.Lookup("en_GB", "Hitch here"));
    }

    static string[] Arguments(string text) =>
        Regex.Matches(text, @"\{\d+\}").Select(match => match.Value).Order().ToArray();
}
