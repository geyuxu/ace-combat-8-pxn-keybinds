using System.Text;

namespace AceCombat8.Core;

public sealed record BindingChange(string Key, string Name, string Before, string After);

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
        if (!Current.TryGetValue(key, out var binding)) throw new FormatException($"当前配置中没有{PxnDevice.ActionName(key)}。");
        Current[key] = binding.WithInputs(inputs);
    }
    public IReadOnlyList<BindingChange> Changes => Current.Where(p => !p.Value.SameAs(Document.Bindings[p.Key]))
        .Select(p => new BindingChange(p.Key, PxnDevice.ActionName(p.Key), Document.Bindings[p.Key].Describe(), p.Value.Describe())).ToList();
    public void ApplyCombatPreset()
    {
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
        if (!HatKeys.All(Current.ContainsKey)) throw new FormatException("配置中缺少帽键项目，无法应用帽键布局。");
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
        Set(key, Current[key].Inputs.Select(i => i with { Direction = i.IsReverse ? "Standard" : "Reverse" }).ToArray());
    }

    public IReadOnlyList<string> Conflicts()
    {
        var warnings = new List<string>();
        foreach (var group in new[] { PxnDevice.Combat, PxnDevice.Menu, PxnDevice.Axes })
        {
            var pairs = group.Where(a => Current.ContainsKey(a.Key)).SelectMany(a => Current[a.Key].Inputs
                .Where(i => i.Type != "None").Select(i => (Action: a.Name, i.Type, i.Button, i.Enable, i.Disable)));
            foreach (var shared in pairs.GroupBy(p => (p.Type, p.Button, p.Enable, p.Disable)).Where(g => g.Select(p => p.Action).Distinct().Count() > 1))
                warnings.Add(string.Join("／", shared.Select(p => p.Action).Distinct()) + " 共用同一输入，可能同时触发。");
        }
        var ownedHats = new HashSet<(string, int)>();
        foreach (string key in HatKeys.Where(Current.ContainsKey))
            foreach (var i in Current[key].Inputs.Where(i => i.Type != "None")) ownedHats.Add((i.Type, i.Button));
        foreach (var action in PxnDevice.Combat.Where(a => Current.ContainsKey(a.Key)))
            if (Current[action.Key].Inputs.Any(i => ownedHats.Contains((i.Type, i.Button))))
                warnings.Add(action.Name + " 与帽键导航／环顾共用输入。");
        return warnings;
    }

    public string ToMarkdown()
    {
        var text = new StringBuilder($"---\ntitle: PXN 2119 Pro 皇牌空战8当前键位\nupdated: {DateTime.Now:yyyy-MM-dd}\n---\n\n# 皇牌空战8 · PXN 2119 Pro\n\n");
        foreach (var group in new[] { ("作战按键", PxnDevice.Combat), ("菜单导航", PxnDevice.Menu), ("飞行轴", PxnDevice.Axes) })
        {
            text.AppendLine($"## {group.Item1}\n\n| 游戏功能 | 实体输入 |\n| --- | --- |");
            foreach (var action in group.Item2.Where(a => Current.ContainsKey(a.Key)))
                text.AppendLine($"| {action.Name} | {Current[action.Key].Describe()} |");
            text.AppendLine();
        }
        text.AppendLine("## 帽键与环顾\n\n| 游戏功能 | 实体输入 |\n| --- | --- |");
        foreach (string key in HatKeys.Where(Current.ContainsKey)) text.AppendLine($"| {PxnDevice.ActionName(key)} | {Current[key].Describe()} |");
        text.AppendLine("\nPXN 使用 MODE 0，所有 T 连发开关保持 OFF。保存配置后重新启动游戏。");
        return text.ToString();
    }
}
