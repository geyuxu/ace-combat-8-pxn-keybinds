using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AceCombat8.Core;

public sealed class FlightStickDocument
{
    private sealed record Line(string Text, string Ending);
    private readonly List<Line> lines;
    private readonly Dictionary<string, (int Index, string Prefix, string Suffix)> locations = new();
    private readonly Encoding encoding;
    private readonly byte[] preamble;
    public string Path { get; }
    public byte[] OriginalBytes { get; }
    public string Hash { get; }
    public string SectionName { get; }
    public IReadOnlyDictionary<string, InputBinding> Bindings { get; }

    private FlightStickDocument(string path, byte[] bytes)
    {
        Path = System.IO.Path.GetFullPath(path);
        OriginalBytes = bytes;
        Hash = Convert.ToHexString(SHA256.HashData(bytes));
        (encoding, preamble) = DetectEncoding(bytes);
        string text;
        try { text = encoding.GetString(bytes, preamble.Length, bytes.Length - preamble.Length); }
        catch (DecoderFallbackException ex) { throw new FormatException("配置编码无效；支持 UTF-8 和带 BOM 的 UTF-16。", ex); }
        lines = Regex.Matches(text, @"([^\r\n]*)(\r\n|\n|\r|$)")
            .Where(m => m.Length > 0).Select(m => new Line(m.Groups[1].Value, m.Groups[2].Value)).ToList();
        var sections = FindSections();
        var targets = sections.Where(s => s.Id == PxnDevice.DeviceId).ToList();
        if (targets.Count != 1)
            throw new FormatException(targets.Count == 0 ? "未找到 PXN 2119 Pro（PXN-F19）的设备区段，请先连接摇杆并启动一次游戏。" : "检测到重复的 PXN 设备区段，无法确定要修改哪一个。");
        var target = targets[0];
        SectionName = target.Name;
        var bindings = new Dictionary<string, InputBinding>(StringComparer.Ordinal);
        for (int i = target.Start + 1; i < target.End; i++)
        {
            var m = Regex.Match(lines[i].Text, @"^(\s*DeviceInputItemMap_([A-Za-z0-9_]+)\s*=\s*)(.*?)(\s*)$");
            if (!m.Success) continue;
            string key = m.Groups[2].Value;
            if (!bindings.TryAdd(key, InputBinding.Parse(m.Groups[3].Value)))
                throw new FormatException($"设备区段中存在重复项目：{key}。");
            locations[key] = (i, m.Groups[1].Value, m.Groups[4].Value);
        }
        if (!new[] { "Gun", "Missile", "Target", "Weapon" }.All(bindings.ContainsKey))
            throw new FormatException("配置缺少主要作战按键，请先让游戏生成完整的 FlightStick.ini。");
        Bindings = bindings;
    }

    public static FlightStickDocument Load(string path) => new(path, File.ReadAllBytes(path));
    public static FlightStickDocument FromBytes(string path, byte[] bytes) => new(path, bytes.ToArray());

    private static (Encoding, byte[]) DetectEncoding(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xfe })) return (new UnicodeEncoding(false, true, true), [0xff, 0xfe]);
        if (bytes.AsSpan().StartsWith(new byte[] { 0xfe, 0xff })) return (new UnicodeEncoding(true, true, true), [0xfe, 0xff]);
        if (bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) return (new UTF8Encoding(false, true), [0xef, 0xbb, 0xbf]);
        return (new UTF8Encoding(false, true), []);
    }

    private List<(string Name, uint Id, int Start, int End)> FindSections()
    {
        var result = new List<(string, uint, int, int)>();
        var headers = lines.Select((line, index) => (Match: Regex.Match(line.Text, @"^\s*\[([^\]]+)\]\s*$"), Index: index))
            .Where(x => x.Match.Success).ToList();
        for (int n = 0; n < headers.Count; n++)
        {
            int start = headers[n].Index, end = n + 1 < headers.Count ? headers[n + 1].Index : lines.Count;
            var ids = lines.Skip(start + 1).Take(end - start - 1)
                .Select(l => Regex.Match(l.Text, @"^\s*DeviceID\s*=\s*(0x[0-9a-fA-F]+)\s*$"))
                .Where(m => m.Success).Select(m => uint.Parse(m.Groups[1].Value[2..], NumberStyles.HexNumber)).ToList();
            if (ids.Count > 1) throw new FormatException("同一个设备区段出现多个 DeviceID。");
            result.Add((headers[n].Match.Groups[1].Value, ids.SingleOrDefault(), start, end));
        }
        return result;
    }

    public byte[] Build(IReadOnlyDictionary<string, InputBinding> desired)
    {
        var copy = lines.ToList();
        foreach (var (key, value) in desired)
        {
            if (!locations.TryGetValue(key, out var location)) throw new FormatException($"当前配置中没有 {key}，未写入未知项目。");
            if (value.SameAs(Bindings[key])) continue;
            copy[location.Index] = new Line(location.Prefix + value.Encode() + location.Suffix, copy[location.Index].Ending);
        }
        byte[] body = encoding.GetBytes(string.Concat(copy.Select(l => l.Text + l.Ending)));
        return preamble.Concat(body).ToArray();
    }
}
