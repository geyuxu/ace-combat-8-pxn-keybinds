namespace AceCombat8.Core;

public sealed record SteamGame(string AppId, string Name, string InstallPath, string Library);
public sealed record ConfigCandidate(string Path, string Source, DateTime Modified)
{
    public string Label => $"{Source}  ·  {Path}";
}
public sealed record DiscoveryResult(IReadOnlyList<SteamGame> Games, IReadOnlyList<ConfigCandidate> Candidates, IReadOnlyList<string> Notes);

public static class ConfigDiscovery
{
    public static DiscoveryResult Find(IEnumerable<string> steamRoots, string localAppData, string? remembered = null)
    {
        var notes = new List<string>();
        var roots = steamRoots.Where(Directory.Exists).Select(System.IO.Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var libraries = new HashSet<string>(roots, StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            string vdfPath = System.IO.Path.Combine(root, "steamapps", "libraryfolders.vdf");
            TryRead(() =>
            {
                if (!File.Exists(vdfPath)) return;
                var folders = SteamVdf.Parse(File.ReadAllText(vdfPath)).Child("libraryfolders");
                if (folders == null) return;
                foreach (var library in folders.Children.Values)
                    if (library.Get("path") is { } path && Directory.Exists(path)) libraries.Add(System.IO.Path.GetFullPath(path));
                foreach (var (key, path) in folders.Values)
                    if (int.TryParse(key, out _) && Directory.Exists(path)) libraries.Add(System.IO.Path.GetFullPath(path));
            }, notes, "有一个 Steam 库信息无法读取，仍可手动选择配置。");
        }
        var games = new List<SteamGame>();
        foreach (var library in libraries)
        {
            string apps = System.IO.Path.Combine(library, "steamapps");
            if (!Directory.Exists(apps)) continue;
            TryRead(() =>
            {
                foreach (string manifest in Directory.EnumerateFiles(apps, "appmanifest_*.acf"))
                {
                    TryRead(() =>
                    {
                        var state = SteamVdf.Parse(File.ReadAllText(manifest)).Child("AppState");
                        if (state?.Get("name") is not { } name || !IsAceCombat8(name)) return;
                        if (state.Get("installdir") is not { } folder || state.Get("appid") is not { } appId) return;
                        games.Add(new(appId, name, System.IO.Path.Combine(apps, "common", folder), library));
                    }, notes, "一个 Steam 游戏清单无法读取。");
                }
            }, notes, "有一个 Steam 库无法访问。");
        }
        var candidates = new Dictionary<string, ConfigCandidate>(StringComparer.OrdinalIgnoreCase);
        void Add(string path, string source)
        {
            TryRead(() =>
            {
                if (!File.Exists(path)) return;
                _ = FlightStickDocument.Load(path); // A candidate must actually contain this user's PXN device.
                string full = System.IO.Path.GetFullPath(path);
                candidates.TryAdd(full, new(full, source, File.GetLastWriteTime(full)));
            }, notes, $"未采用无效或不可读的配置：{System.IO.Path.GetFileName(path)}。");
        }
        if (!string.IsNullOrWhiteSpace(remembered)) Add(remembered, "上次使用");
        string config = System.IO.Path.Combine(localAppData, "BANDAI NAMCO Entertainment", "ACE COMBAT 8", "Saved", "Config");
        Add(System.IO.Path.Combine(config, "Windows", "FlightStick.ini"), "Windows 当前用户配置");
        Add(System.IO.Path.Combine(config, "WindowsNoEditor", "FlightStick.ini"), "Windows 备用配置目录");
        foreach (var game in games)
        {
            Add(System.IO.Path.Combine(game.InstallPath, "Game", "Saved", "Config", "Windows", "FlightStick.ini"), "Steam 游戏内本地配置");
            Add(System.IO.Path.Combine(game.InstallPath, "Saved", "Config", "Windows", "FlightStick.ini"), "Steam 游戏内本地配置");
        }
        if (games.Count == 0) notes.Add("Steam 中未找到《皇牌空战8》安装记录；仍会查找当前用户配置，也可手动选择。");
        if (candidates.Count == 0) notes.Add("尚未找到含 PXN-F19 的 FlightStick.ini。请连接摇杆，启动游戏后退出，再重试或手动选择。");
        // If several real files exist, the UI asks which one to open instead of guessing from a backup's timestamp.
        return new(games.DistinctBy(g => g.InstallPath, StringComparer.OrdinalIgnoreCase).ToList(), candidates.Values.ToList(), notes.Distinct().ToList());
    }
    public static bool IsAceCombat8(string name) => System.Text.RegularExpressions.Regex.IsMatch(name, @"\bACE\s+COMBAT\s*8\b|皇牌空战\s*8", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    private static void TryRead(Action action, List<string> notes, string message)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or System.Text.Json.JsonException or ArgumentException)
        { notes.Add(message); }
    }
}
