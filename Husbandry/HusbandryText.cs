using System.Collections.Concurrent;
using System.Text.Json;
using Meitou.Localisation;

namespace Husbandry;

/// <summary>
/// Translates Husbandry-owned text using the language selected in Kenshi. Catalogs travel in the mod assembly.
/// </summary>
internal static class HusbandryText
{
    static readonly ConcurrentDictionary<string, Dictionary<string, string>> Catalogs = new(StringComparer.OrdinalIgnoreCase);

    public static string Get(string source) => Lookup(Loc.Language.Code, source);

    public static string Format(string source, params object?[] values) => Loc.Format(Get(source), values);

    internal static string Lookup(string language, string source)
    {
        var catalog = Catalogs.GetOrAdd(language.Replace('-', '_'), Read);
        return catalog.GetValueOrDefault(source, source);
    }

    static Dictionary<string, string> Read(string code)
    {
        using var stream = typeof(HusbandryText).Assembly.GetManifestResourceStream($"Husbandry.Locale.{code}.json");
        return stream is null ? [] : JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }
}
