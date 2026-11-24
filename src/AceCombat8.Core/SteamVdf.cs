using System.Text.RegularExpressions;

namespace AceCombat8.Core;

public sealed class SteamVdf
{
    public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, SteamVdf> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string? Get(string key) => Values.GetValueOrDefault(key);
    public SteamVdf? Child(string key) => Children.GetValueOrDefault(key);

    public static SteamVdf Parse(string text)
    {
        var tokens = Regex.Matches(text, "//[^\\r\\n]*|\"((?:\\\\.|[^\"\\\\])*)\"|[{}]")
            .Where(m => !m.Value.StartsWith("//", StringComparison.Ordinal))
            .Select(m => m.Value.StartsWith('"') ? Unescape(m.Groups[1].Value) : m.Value).ToArray();
        int position = 0;
        return Read(tokens, ref position, false);
    }
    private static SteamVdf Read(string[] tokens, ref int position, bool nested)
    {
        var node = new SteamVdf();
        while (position < tokens.Length)
        {
            string key = tokens[position++];
            if (key == "}") return node;
            if (key == "{" || position >= tokens.Length) throw new FormatException("Steam VDF 格式无效。");
            string value = tokens[position++];
            if (value == "{") node.Children[key] = Read(tokens, ref position, true);
            else if (value == "}") throw new FormatException("Steam VDF 值缺失。");
            else node.Values[key] = value;
        }
        if (nested) throw new FormatException("Steam VDF 缺少结束括号。");
        return node;
    }
    private static string Unescape(string value) => value.Replace("\\\"", "\"").Replace("\\\\", "\\");
}
