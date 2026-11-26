using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AceCombat8.Core;

if (args.Length > 0 && args[0] == "--discover")
{
    var result = ConfigDiscovery.Find([@"C:\Program Files (x86)\Steam"], Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
    Console.WriteLine(JsonSerializer.Serialize(result));
    return result.Games.Any(g => g.AppId == "2288340") && result.Candidates.Count == 1 ? 0 : 1;
}
var suite = new RegressionSuite();
suite.Run();
return suite.Failures == 0 ? 0 : 1;

internal sealed partial class RegressionSuite
{
    private readonly string fixture = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "FlightStick.ini"));
    private readonly string root = Path.Combine(Path.GetTempPath(), "ac8-keybind-tests-" + Guid.NewGuid().ToString("N"));
    private int passed;
    public int Failures { get; private set; }
    public void Run()
    {
        Directory.CreateDirectory(root);
        try
        {
            Test("untouched configuration is byte-identical", () => { var d = Document(); Check(d.Build(d.Bindings).SequenceEqual(d.OriginalBytes)); });
            Test("only the four combat lines change", FourCombatLines);
            Test("correct escaping and unknown JSON fields survive", UnknownFields);
            Test("atomic save creates an exact backup", SaveAndRestore);
            Test("unchanged save does not create a backup", UnchangedSave);
            Test("external changes refuse overwrite", ConcurrentChange);
            Test("a running game refuses write", RunningGame);
            Test("non-PXN device can be selected and saved independently", GenericDevice);
            Test("multiple inputs preserve sibling and unknown fields through add and remove", MultipleBindings);
            Test("renamed PXN section is selected by VID/PID", () => Check(From(fixture.Replace("[PXN-F19]", "[Flight device]")).SectionName == "Flight device"));
            Test("duplicate PXN sections are refused", () => Throws<FormatException>(() => From(fixture + fixture[fixture.IndexOf("[PXN-F19]", StringComparison.Ordinal)..])));
            Test("duplicate function keys are refused", DuplicateKey);
            Test("missing primary action is refused", () => Throws<FormatException>(() => From(RemoveTargetKey("Gun"))));
            Test("invalid nested JSON is refused", () => Throws<FormatException>(() => InputBinding.Parse("\"not json\"")));
            Test("invalid UTF8 is refused", () => Throws<FormatException>(() => FlightStickDocument.FromBytes("invalid.ini", [0xff, 0xff, 0xff])));
            Test("unknown keys and other devices survive", OtherSections);
            Test("native POV2 swap is correct", NativePov2);
            Test("hat swap roundtrip returns to original", HatRoundtrip);
            Test("camera reversal preserves input identity", CameraReverse);
            Test("flight/menu shared buttons are allowed", () => Check(new EditSession(Document()).Conflicts().Count == 0));
            Test("within-flight shared buttons are reported", Conflict);
            Test("menu conflicts do not alter flight controls", MenuConflict);
            Test("export uses game and hardware terms", Markdown);
            Test("story skip menu binding is visible and preserved", StorySkip);
            Test("cannot add nonexistent config keys", () => { var d = Document(); var c = d.Bindings.ToDictionary(p => p.Key, p => p.Value); c["Invented"] = c["Gun"]; Throws<FormatException>(() => d.Build(c)); });
            foreach (string ending in new[] { "\n", "\r\n", "\r" })
                foreach (var encoding in new Encoding[] { new UTF8Encoding(false, true), new UTF8Encoding(true, true), new UnicodeEncoding(false, true, true), new UnicodeEncoding(true, true, true) })
                    Test($"preserve encoding {encoding.WebName} BOM={encoding.GetPreamble().Length} EOL={ending.Length}", () => EncodingRoundtrip(encoding, ending));
            Test("Steam libraries and manifests find actual game", SteamDiscovery);
            Test("old-format libraryfolders is supported", LegacySteam);
            Test("ambiguous config candidates are preserved", MultipleCandidates);
            Test("Steam absence still permits local config", NoSteam);
            Test("invalid configs never become candidates", InvalidCandidate);
            Test("Steam title matching excludes AC7", () => { Check(ConfigDiscovery.IsAceCombat8("ACE COMBAT 8: WINGS OF THEVE")); Check(!ConfigDiscovery.IsAceCombat8("ACE COMBAT 7")); Check(!ConfigDiscovery.IsAceCombat8("ACE COMBAT 80")); });
            Test("VDF comments and escaped paths parse", VdfEscapes);
            Test("truncated VDF is refused", () => Throws<FormatException>(() => SteamVdf.Parse("\"a\" { \"b\" \"c\"")));
        }
        finally
        {
            Console.WriteLine($"RESULT: {passed} passed, {Failures} failed. Fixtures: {root}");
        }
    }
    private void Test(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { Failures++; Console.WriteLine("FAIL " + name + "\n" + ex); }
    }
    private static void Check(bool result, string message = "Assertion failed") { if (!result) throw new Exception(message); }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }
    private FlightStickDocument From(string text) => FlightStickDocument.FromBytes(Path.Combine(root, "memory.ini"), Encoding.UTF8.GetBytes(text));
    private FlightStickDocument Document() => From(fixture);
    private string NewFile()
    {
        string dir = Path.Combine(root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "FlightStick.ini"); File.WriteAllText(path, fixture, new UTF8Encoding(false)); return path;
    }
    private string RemoveTargetKey(string key)
    {
        int index = fixture.IndexOf("[PXN-F19]", StringComparison.Ordinal);
        return fixture[..index] + Regex.Replace(fixture[index..], @"(?m)^DeviceInputItemMap_" + key + @"=[^\r\n]*(?:\r?\n)?", "");
    }
    private void FourCombatLines()
    {
        var d = Document(); var s = new EditSession(d);
        foreach (var (key, n) in new[] { ("Gun", 2), ("Missile", 1), ("Target", 4), ("Weapon", 3) }) s.Set(key, new FlightInput("Button", n));
        byte[] output = d.Build(s.Current);
        var read = FlightStickDocument.FromBytes(d.Path, output);
        Check(s.Changes.Count == 4 && read.Bindings["Gun"].Inputs[0].Button == 2);
        string updated = Encoding.UTF8.GetString(output);
        var a = fixture.Split('\n'); var b = updated.Split('\n'); Check(a.Length == b.Length);
        Check(a.Zip(b).Count(p => p.First != p.Second) == 4);
        int first = fixture.IndexOf("[PXN-F19]", StringComparison.Ordinal);
        Check(fixture[..first] == updated[..first]);
    }
    private void UnknownFields()
    {
        string inner = "{\"future\":{\"number\":42},\"inputSettings\":[{\"inputType\":\"Button\",\"buttonIndex\":1,\"vendor\":\"keep\",\"axisInputOption\":\"Standard\"}]}";
        var binding = InputBinding.Parse(JsonSerializer.Serialize(inner)).WithInputs([new("Button", 5)]);
        string encoded = binding.Encode();
        Check(encoded.Contains("\\\"inputSettings\\\"", StringComparison.Ordinal), "Game INI must use native quote escaping");
        var rootNode = JsonNode.Parse(JsonSerializer.Deserialize<string>(encoded)!)!;
        Check(rootNode["future"]!["number"]!.GetValue<int>() == 42);
        Check(rootNode["inputSettings"]![0]!["vendor"]!.GetValue<string>() == "keep");
    }
    private void SaveAndRestore()
    {
        string path = NewFile(); var d = FlightStickDocument.Load(path); var s = new EditSession(d);
        s.Set("Gun", new FlightInput("Button", 7));
        var saved = ConfigStore.Save(s, () => false);
        Check(saved.Changed && File.ReadAllBytes(saved.BackupPath!).SequenceEqual(d.OriginalBytes));
        var restore = ConfigStore.PrepareRestore(saved.Document, saved.BackupPath!);
        Check(restore.Changes.Single().Key == "Gun");
        var restored = ConfigStore.Save(restore);
        Check(restored.Document.Bindings.All(p => p.Value.SameAs(d.Bindings[p.Key])));
        Check(ConfigStore.ListBackups(path).Count == 2);
        Check(!Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.tmp").Any());
    }
    private void UnchangedSave() { string p = NewFile(); var result = ConfigStore.Save(new(FlightStickDocument.Load(p))); Check(!result.Changed && result.BackupPath == null && ConfigStore.ListBackups(p).Count == 0); }
    private void ConcurrentChange()
    {
        string p = NewFile(); var s = new EditSession(FlightStickDocument.Load(p)); s.Set("Gun", new FlightInput("Button", 7));
        File.AppendAllText(p, "; external change\n"); byte[] external = File.ReadAllBytes(p);
        Throws<InvalidOperationException>(() => ConfigStore.Save(s)); Check(File.ReadAllBytes(p).SequenceEqual(external)); Check(ConfigStore.ListBackups(p).Count == 0);
    }
    private void RunningGame()
    {
        string p = NewFile(); var d = FlightStickDocument.Load(p); var s = new EditSession(d); s.Set("Gun", new FlightInput("Button", 7));
        Throws<InvalidOperationException>(() => ConfigStore.Save(s, () => true)); Check(File.ReadAllBytes(p).SequenceEqual(d.OriginalBytes)); Check(ConfigStore.ListBackups(p).Count == 0);
    }
    private void DuplicateKey()
    {
        var d = Document(); string line = "DeviceInputItemMap_Gun=" + d.Bindings["Gun"].Encode();
        Throws<FormatException>(() => From(fixture + "\n" + line + "\n"));
    }
    private void OtherSections()
    {
        var text = fixture + "\n[Future device]\nDeviceID=0x12345678\nSomeUnknown=keep exactly\n";
        var d = From(text); var s = new EditSession(d); s.Set("Gun", new FlightInput("Button", 7));
        Check(Encoding.UTF8.GetString(d.Build(s.Current)).EndsWith("[Future device]\nDeviceID=0x12345678\nSomeUnknown=keep exactly\n", StringComparison.Ordinal));
    }
    private void NativePov2()
    {
        var s = new EditSession(Document()); Check(s.HatLayout == "right-orders"); s.ApplyHatLayout(true);
        Check(s.Current["HatUp"].HasInputs(new FlightInput("Button", 17)) && s.Current["DPadRight"].HasInputs(new FlightInput("Button", 20)));
        Check(s.Current["CameraPitch"].HasInputs(new("POV_D"), new("POV_U", 0, "Reverse")));
        Check(s.Current["Gun"].SameAs(s.Document.Bindings["Gun"]));
    }
    private void HatRoundtrip() { var s = new EditSession(Document()); s.ApplyHatLayout(true); s.ApplyHatLayout(false); Check(s.Changes.Count == 0); }
    private void CameraReverse()
    {
        var s = new EditSession(Document()); var before = s.Current["CameraPitch"].Inputs; s.ReverseCamera("CameraPitch");
        Check(s.Current["CameraPitch"].Inputs.Zip(before).All(p => p.First.Type == p.Second.Type && p.First.Button == p.Second.Button && p.First.IsReverse != p.Second.IsReverse));
    }
    private void Conflict() { var s = new EditSession(Document()); s.Set("Gun", new FlightInput("Button", 2)); Check(s.Conflicts().Any(c => c.Contains("机炮") && c.Contains("导弹"))); }
    private void MenuConflict() { var s = new EditSession(Document()); s.Set("FaceButtonBottom", new FlightInput("Button", 2)); Check(s.Conflicts().Any(c => c.Contains("确认"))); Check(s.Current["Gun"].SameAs(s.Document.Bindings["Gun"])); }
    private void Markdown() { string md = new EditSession(Document()).ToMarkdown(); Check(md.Contains("机炮") && md.Contains("高 G 回转") && md.Contains("POV2") && !md.Contains("DeviceInputItemMap")); }
    private void StorySkip()
    {
        var action = PxnDevice.Menu.Single(a => a.Key == "SpecialRight");
        Check(action.Name.Contains("跳过剧情") && action.Hint.Contains("按住"));
        var session = new EditSession(Document());
        Check(session.Current["SpecialRight"].HasInputs(new FlightInput("Button", 8)));
        session.ApplyCombatPreset();
        Check(session.Current["SpecialRight"].HasInputs(new FlightInput("Button", 8)));
        Check(session.ToMarkdown().Contains("菜单键 ≡／跳过剧情"));
    }

    private void GenericDevice()
    {
        string path = NewFile();
        string head = fixture[..fixture.IndexOf("[PXN-F19]", StringComparison.Ordinal)];
        string section = fixture[head.Length..].Replace("PXN-F19", "Example HOTAS").Replace("0x84711ff", "0xB10A044F");
        File.WriteAllText(path, fixture + "\r\n" + section);
        var original = FlightStickDocument.Load(path);
        var device = original.Devices.Single(d => d.Name == "Example HOTAS");
        var session = new EditSession(original.Select(device));
        session.AddInput("Target", new FlightInput("Button", 27));
        Check(ControllerInputs.List(session, new(28, 6, 1)).Contains(new FlightInput("Button", 28)));
        var saved = ConfigStore.Save(session);
        Check(saved.Document.Device == device && saved.Document.Bindings["Target"].Inputs.Count == 2);
        Check(File.ReadAllText(path).StartsWith(fixture, StringComparison.Ordinal));
        var restore = ConfigStore.PrepareRestore(saved.Document, saved.BackupPath!);
        Check(restore.Current["Target"].Inputs.Count == 1);
        Check(FlightStickDocument.FromBytes(path, Encoding.UTF8.GetBytes(head + section), device).Device == device);
    }

    private void MultipleBindings()
    {
        InputBinding binding = InputBinding.Parse(JsonSerializer.Serialize(
            """{"inputSettings":[{"inputType":"Button","buttonIndex":2,"vendor":"first"},{"inputType":"Button","buttonIndex":3,"vendor":"second"}],"future":true}"""));
        var third = new FlightInput("Button", 7);
        var added = binding.AddInput(third);
        Check(added.Inputs.Count == 3 && ReferenceEquals(added.AddInput(third), added));
        var result = added.RemoveInput(0);
        var json = JsonNode.Parse(result.Fingerprint)!;
        Check(result.HasInputs(new FlightInput("Button", 3), third));
        Check(json["inputSettings"]![0]!["vendor"]!.GetValue<string>() == "second" && json["future"]!.GetValue<bool>());
        var session = new EditSession(Document());
        session.RemoveInput("Target", 0);
        Check(!session.IsBound("Target") && session.UnboundActions.Contains("Target"));
        Throws<ArgumentException>(() => session.AddInput("Throttle", third));
    }
    private void EncodingRoundtrip(Encoding encoding, string ending)
    {
        string text = fixture.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", ending);
        byte[] raw = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        var d = FlightStickDocument.FromBytes("fixture.ini", raw); Check(d.Build(d.Bindings).SequenceEqual(raw));
        var s = new EditSession(d); s.Set("Gun", new FlightInput("Button", 7));
        byte[] edited = d.Build(s.Current); Check(edited.AsSpan().StartsWith(encoding.GetPreamble()));
        Check(FlightStickDocument.FromBytes("fixture.ini", edited).Bindings["Gun"].Inputs[0].Button == 7);
    }
}
