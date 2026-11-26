using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using static AceCombat8.Core.Lang;

namespace AceCombat8.Core;

public sealed record DeviceSection(string Name, uint Id)
{
    public bool IsPxn => Id == PxnDevice.DeviceId;
    public string Label => IsPxn ? "PXN 2119 Pro · " + Name : $"{Name} · {Id & 0xffff:X4}:{Id >> 16:X4}";
}

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
    public DeviceSection Device { get; }
    public IReadOnlyList<DeviceSection> Devices { get; }
    public string SectionName => Device.Name;
    public IReadOnlyDictionary<string, InputBinding> Bindings { get; }

    private FlightStickDocument(string path, byte[] bytes, DeviceSection? selection)
    {
        Path = System.IO.Path.GetFullPath(path);
        OriginalBytes = bytes;
        Hash = Convert.ToHexString(SHA256.HashData(bytes));
        (encoding, preamble) = DetectEncoding(bytes);
        string text;
        try { text = encoding.GetString(bytes, preamble.Length, bytes.Length - preamble.Length); }
        catch (DecoderFallbackException ex) { throw new FormatException(T("请选择 UTF-8 或带 BOM 的 UTF-16 配置文件。", "Use a UTF-8 file or a UTF-16 file with a BOM.", "UTF-8 または BOM 付き UTF-16 の設定ファイルを選択してください。"), ex); }
        lines = Regex.Matches(text, @"([^\r\n]*)(\r\n|\n|\r|$)")
            .Where(m => m.Length > 0).Select(m => new Line(m.Groups[1].Value, m.Groups[2].Value)).ToList();
        var sections = FindSections().Where(s => s.Id == PxnDevice.DeviceId || HasFlightActions(s.Start, s.End)).ToList();
        Devices = sections.Select(s => new DeviceSection(s.Name, s.Id)).Distinct().ToList();
        Device = selection ?? Devices.FirstOrDefault(d => d.IsPxn) ?? Devices.FirstOrDefault()
            ?? throw new FormatException(Lang.T("没有可编辑的飞行控制器配置。", "No editable flight controller profile was found.", "編集可能なフライトコントローラー設定が見つかりません。"));
        var targets = sections.Where(s => s.Id == Device.Id && s.Name == Device.Name).ToList();
        if (targets.Count != 1 || sections.Count(s => s.Id == Device.Id) != 1)
            throw new FormatException(Lang.T("无法唯一匹配该设备的配置区段。", "The controller profile could not be matched uniquely.", "コントローラー設定を一意に特定できません。"));
        var target = targets[0];
        var bindings = new Dictionary<string, InputBinding>(StringComparer.Ordinal);
        for (int i = target.Start + 1; i < target.End; i++)
        {
            var m = Regex.Match(lines[i].Text, @"^(\s*DeviceInputItemMap_([A-Za-z0-9_]+)\s*=\s*)(.*?)(\s*)$");
            if (!m.Success) continue;
            string key = m.Groups[2].Value;
            if (!bindings.TryAdd(key, InputBinding.Parse(m.Groups[3].Value)))
                throw new FormatException(T("设备区段存在重复操作 ", "Duplicate action in the device profile ", "機器設定内で操作が重複しています ") + key);
            locations[key] = (i, m.Groups[1].Value, m.Groups[4].Value);
        }
        if (!new[] { "Gun", "Missile", "Target", "Weapon" }.All(bindings.ContainsKey))
            throw new FormatException(T("缺少主要作战操作，请由游戏生成完整配置。", "Required combat actions are missing. Let the game generate a complete profile.", "主な戦闘操作がありません。ゲームで完全な設定を生成してください。"));
        Bindings = bindings;
    }

    public static FlightStickDocument Load(string path, DeviceSection? device = null) => new(path, File.ReadAllBytes(path), device);
    public static FlightStickDocument FromBytes(string path, byte[] bytes, DeviceSection? device = null) => new(path, bytes.ToArray(), device);
    public FlightStickDocument Select(DeviceSection device) => FromBytes(Path, OriginalBytes, device);

    private bool HasFlightActions(int start, int end)
    {
        var keys = lines.Skip(start + 1).Take(end - start - 1)
            .Select(l => Regex.Match(l.Text, @"^\s*DeviceInputItemMap_([A-Za-z0-9_]+)\s*="))
            .Where(m => m.Success).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        return new[] { "Gun", "Missile", "Target", "Weapon" }.All(keys.Contains);
    }

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
                .Select(l => Regex.Match(l.Text, @"^\s*DeviceID\s*=\s*(0[xX][0-9a-fA-F]{1,8})\s*$"))
                .Where(m => m.Success).Select(m => uint.Parse(m.Groups[1].Value[2..], NumberStyles.HexNumber)).ToList();
            if (ids.Count > 1) throw new FormatException(T("同一个设备区段出现多个 DeviceID。", "A device profile has multiple DeviceID entries.", "機器設定に複数の DeviceID が含まれています。"));
            if (ids.Count == 1 && ids[0] != 0) result.Add((headers[n].Match.Groups[1].Value, ids[0], start, end));
        }
        return result;
    }

    public byte[] Build(IReadOnlyDictionary<string, InputBinding> desired)
    {
        var copy = lines.ToList();
        foreach (var (key, value) in desired)
        {
            if (!locations.TryGetValue(key, out var location))
                throw new FormatException(T("设备配置缺少操作 ", "The device profile is missing the action ", "機器設定に操作がありません ") + key);
            if (value.SameAs(Bindings[key])) continue;
            copy[location.Index] = new Line(location.Prefix + value.Encode() + location.Suffix, copy[location.Index].Ending);
        }
        byte[] body = encoding.GetBytes(string.Concat(copy.Select(l => l.Text + l.Ending)));
        return preamble.Concat(body).ToArray();
    }
}
