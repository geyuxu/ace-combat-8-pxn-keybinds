using System.Text;
using static AceCombat8.Core.Lang;

namespace AceCombat8.Core;

public sealed record BindingChange(string Key, string Name, string Before, string After);
public sealed record PhysicalBinding(string Key, FlightInput Input, int Index);

public sealed class EditSession
{
    public FlightStickDocument Document { get; }
    public Dictionary<string, InputBinding> Current { get; }
    public EditSession(FlightStickDocument document)
    {
        Document = document;
        Current = document.Bindings.ToDictionary(p => p.Key, p => p.Value);
    }
    public void Set(string key, params FlightInput[] inputs)
    {
        if (!Current.TryGetValue(key, out var binding))
            throw new FormatException(T("当前配置中没有此操作。", "This action is missing from the profile.", "この設定に操作がありません。") + $" ({key})");
        Current[key] = binding.WithInputs(inputs);
    }
    public bool IsBound(string key) => Current.TryGetValue(key, out var binding)
        && binding.Inputs.Any(input => input.Type != "None");

    public IReadOnlyList<string> UnboundActions => Current.Keys.Where(key => ActionCatalog.IsCommon(key) && !IsBound(key)).ToList();

    public IReadOnlyList<PhysicalBinding> BindingsFor(FlightInput control) => Current
        .SelectMany(pair => pair.Value.Inputs.Select((input, index) => new PhysicalBinding(pair.Key, input, index)))
        .Where(binding => binding.Input.Type != "None" && binding.Input.Type == control.Type && binding.Input.Button == control.Button)
        .ToList();

    public void AddInput(string key, FlightInput input)
    {
        if (!ActionCatalog.CanAssign(key, input) || input.Type == "None")
            throw new ArgumentException(Lang.T("这个输入不能用于该操作。", "This input cannot be assigned to that action.", "この入力はその操作に割り当てられません。"));
        Current[key] = Current[key].AddInput(input);
    }

    public void RemoveInput(string key, int index) => Current[key] = Current[key].RemoveInput(index);

    public string Describe(InputBinding binding) => binding.Describe(Document.Device);
    public IReadOnlyList<BindingChange> Changes => Current.Where(p => !p.Value.SameAs(Document.Bindings[p.Key]))
        .Select(p => new BindingChange(p.Key, ActionCatalog.Name(p.Key), Describe(Document.Bindings[p.Key]), Describe(p.Value))).ToList();
    public void ApplyCombatPreset()
    {
        if (!Document.Device.IsPxn) throw new InvalidOperationException(T("此预设适用于 PXN 2119 Pro。", "This preset is for the PXN 2119 Pro.", "このプリセットは PXN 2119 Pro 用です。"));
        Set("Gun", new FlightInput("Button", 1)); Set("Missile", new FlightInput("Button", 2));
        Set("Target", new FlightInput("Button", 3)); Set("Weapon", new FlightInput("Button", 4));
    }
    public string HatLayout => MatchesHatLayout(false) ? "right-orders" : MatchesHatLayout(true) ? "left-orders" : "custom";

    private bool MatchesHatLayout(bool swapped)
    {
        var expected = new EditSession(Document);
        if (!HatKeys.All(Current.ContainsKey)) return false;
        expected.ApplyHatLayout(swapped);
        return HatKeys.All(k => Current[k].Inputs.SequenceEqual(expected.Current[k].Inputs));
    }
    public static readonly string[] HatKeys = ["HatUp", "HatDown", "HatLeft", "HatRight", "DPadUp", "DPadDown", "DPadLeft", "DPadRight", "CameraPitch", "CameraYaw"];
    public void ApplyHatLayout(bool swapped)
    {
        if (!Document.Device.IsPxn || !HatKeys.All(Current.ContainsKey))
            throw new FormatException(T("此设备配置不支持 PXN 帽键预设。", "This profile does not support the PXN hat preset.", "この設定では PXN ハット用プリセットを使用できません。"));
        string[] directions = ["Up", "Down", "Left", "Right"];
        string[] types = ["POV_U", "POV_D", "POV_L", "POV_R"];
        for (int i = 0; i < directions.Length; i++)
        {
            FlightInput input = swapped ? new("Button", 17 + i) : new(types[i]);
            Set("Hat" + directions[i], input);
            Set("DPad" + directions[i], input);
        }
        FlightInput Camera(int i, bool reverse) => swapped
            ? new(types[i], 0, reverse ? "Reverse" : "Standard")
            : new("Button", 17 + i, reverse ? "Reverse" : "Standard");
        Set("CameraPitch", Camera(1, false), Camera(0, true));
        Set("CameraYaw", Camera(3, false), Camera(2, true));
    }
    public void ReverseCamera(string key)
    {
        if (key != "CameraPitch" && key != "CameraYaw") throw new ArgumentException("无效的视角轴。");
        Set(key, Current[key].Inputs.Select(i => i with { Direction = i.Direction switch
        {
            "Standard" when i.Type != "None" => "Reverse",
            "Reverse" => "Standard",
            _ => i.Direction
        } }).ToArray());
    }

    public IReadOnlyList<string> Conflicts()
    {
        var warnings = new List<string>();
        // Matching POV/wingman and menu directions are shared across different contexts.
        foreach (var group in Current.Keys.GroupBy(ActionCatalog.IsMenu))
        {
            var pairs = group.SelectMany(key => Current[key].Inputs.Where(i => i.Type != "None")
                .Select(i => (Action: key, i.Type, i.Button, i.Enable, i.Disable)));
            foreach (var shared in pairs.GroupBy(p => (p.Type, p.Button, p.Enable, p.Disable)).Where(g => g.Select(p => p.Action).Distinct().Count() > 1))
                warnings.Add(string.Join(" / ", shared.Select(p => p.Action).Distinct().Select(ActionCatalog.Name)) +
                    T(" 共用同一输入。", " share an input.", " は同じ入力を共有しています。"));
        }
        return warnings;
    }

    public string ToMarkdown()
    {
        var text = new StringBuilder($"# ACE COMBAT 8 · {MarkdownCell(Document.Device.Label)}\n\n");
        text.AppendLine(T("| 游戏功能 | 实体输入 |", "| Action | Physical inputs |", "| 操作 | 物理入力 |"));
        text.AppendLine("| --- | --- |");
        foreach (var (key, binding) in Current)
            text.AppendLine($"| {MarkdownCell(ActionCatalog.Name(key))} | {MarkdownCell(Describe(binding))} |");
        text.AppendLine(T("\n保存配置后重新启动游戏。", "\nRestart the game after saving.", "\n保存後にゲームを再起動します。"));
        return text.ToString();
    }
    private static string MarkdownCell(string value) => value.Replace("|", "\\|").Replace("\r", "").Replace("\n", " ");
}
