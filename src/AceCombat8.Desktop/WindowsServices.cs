using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace AceCombat8.Desktop;

internal static class WindowsServices
{
    private static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AceCombat8Keybinds", "settings.json");
    public static IEnumerable<string> SteamRoots()
    {
        var paths = new List<string>();
        foreach (var pair in new[] { (Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath"), (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath") })
        {
            try
            {
                using var key = pair.Item1.OpenSubKey(pair.Item2);
                if (key?.GetValue(pair.Item3) is string value) paths.Add(value.Replace('/', Path.DirectorySeparatorChar));
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException) { }
        }
        paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        return paths.Distinct(StringComparer.OrdinalIgnoreCase);
    }
    public static string? LastPath()
    {
        try { return File.Exists(SettingsPath) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(SettingsPath))?.GetValueOrDefault("configPath") : null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }
    public static void Remember(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new { configPath = path }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* The configuration still works without a remembered path. */ }
    }
    public static bool GameRunning()
    {
        foreach (var process in Process.GetProcessesByName("AceCombat8"))
        {
            using (process) { if (!process.HasExited) return true; }
        }
        return false;
    }
    public static void OpenFolder(string path) => Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
}
