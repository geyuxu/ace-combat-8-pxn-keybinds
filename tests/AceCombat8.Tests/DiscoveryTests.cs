using AceCombat8.Core;

internal sealed partial class RegressionSuite
{
    private string NewDirectory(string label)
    {
        string path = Path.Combine(root, label + "-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path;
    }
    private static string Escape(string path) => path.Replace("\\", "\\\\").Replace("\"", "\\\"");
    private void LocalConfig(string local, string directory = "Windows", string? body = null)
    {
        string parent = Path.Combine(local, "BANDAI NAMCO Entertainment", "ACE COMBAT 8", "Saved", "Config", directory);
        Directory.CreateDirectory(parent); File.WriteAllText(Path.Combine(parent, "FlightStick.ini"), body ?? fixture);
    }
    private static void Manifest(string library, string id, string name, string dir)
    {
        string apps = Path.Combine(library, "steamapps"); Directory.CreateDirectory(apps);
        File.WriteAllText(Path.Combine(apps, $"appmanifest_{id}.acf"), $"\"AppState\" {{ \"appid\" \"{id}\" \"name\" \"{name}\" \"installdir\" \"{dir}\" }}");
        Directory.CreateDirectory(Path.Combine(apps, "common", dir));
    }
    private void SteamDiscovery()
    {
        string steam = NewDirectory("Steam"), library = NewDirectory("SteamLibrary"), local = NewDirectory("Local");
        Directory.CreateDirectory(Path.Combine(steam, "steamapps"));
        File.WriteAllText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"), $"\"libraryfolders\" {{ \"0\" {{ \"path\" \"{Escape(library)}\" }} \"1\" {{ \"path\" \"{Escape(library)}\" }} }}");
        Manifest(library, "2288340", "ACE COMBAT 8: WINGS OF THEVE", "ACE COMBAT 8");
        Manifest(library, "502500", "ACE COMBAT 7", "ACE COMBAT 7"); LocalConfig(local);
        var result = ConfigDiscovery.Find([steam], local);
        Check(result.Games.Count == 1 && result.Games[0].AppId == "2288340" && result.Games[0].Library == library);
        Check(result.Candidates.Count == 1 && result.Candidates[0].Path.StartsWith(local, StringComparison.Ordinal));
    }
    private void LegacySteam()
    {
        string steam = NewDirectory("oldSteam"), library = NewDirectory("oldLibrary"), local = NewDirectory("oldLocal");
        Directory.CreateDirectory(Path.Combine(steam, "steamapps"));
        File.WriteAllText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"), $"\"libraryfolders\" {{ \"1\" \"{Escape(library)}\" }}");
        Manifest(library, "2288340", "ACE COMBAT 8", "ACE COMBAT 8");
        Check(ConfigDiscovery.Find([steam], local).Games.Single().AppId == "2288340");
    }
    private void MultipleCandidates()
    {
        string local = NewDirectory("multipleLocal"); LocalConfig(local); LocalConfig(local, "WindowsNoEditor");
        var first = ConfigDiscovery.Find([], local); Check(first.Candidates.Count == 2);
        Check(ConfigDiscovery.Find([], local, first.Candidates[0].Path).Candidates.Count == 2);
    }
    private void NoSteam()
    {
        string local = NewDirectory("noSteam"); LocalConfig(local);
        var result = ConfigDiscovery.Find([], local); Check(result.Games.Count == 0 && result.Candidates.Count == 1);
    }
    private void InvalidCandidate()
    {
        string local = NewDirectory("badLocal"); LocalConfig(local, body: "[Wrong]\nDeviceID=0x1234\n");
        var result = ConfigDiscovery.Find([], local); Check(result.Candidates.Count == 0 && result.Notes.Count > 0);
    }
    private static void VdfEscapes()
    {
        var parsed = SteamVdf.Parse("// comment\n\"root\" { \"path\" \"D:\\\\SteamLibrary\" \"text\" \"a\\\"b\" }").Child("root")!;
        Check(parsed.Get("path") == "D:\\SteamLibrary" && parsed.Get("text") == "a\"b");
    }
}
