namespace AceCombat8.Core;

public sealed record InputChoice(string Label, FlightInput? Input)
{
    public string Token => Input?.Token ?? "custom";
}

public sealed record GameAction(string Key, string Name, string Hint, string Group);

public static class PxnDevice
{
    public const uint DeviceId = 0x084711ff;
    public static readonly GameAction[] Combat =
    [
        new("Gun", "机炮（机枪）", "按住持续射击", "武器与目标"),
        new("Missile", "发射导弹／特殊武器", "发射当前选中的导弹或特殊武器", "武器与目标"),
        new("Target", "切换锁定目标", "长按注视当前目标", "武器与目标"),
        new("Weapon", "切换副武器", "在标准导弹与特殊武器之间切换", "武器与目标"),
        new("AutoPilot", "自动驾驶", "按住使用，松开结束", "飞行与防御"),
        new("AccelDecel", "高 G 回转", "同时输入加速与减速", "飞行与防御"),
        new("Flare", "释放热焰弹", "规避来袭导弹", "飞行与防御"),
        new("View", "切换视角", "切换驾驶舱／机外等视角", "视角与系统"),
        new("Radar", "切换雷达地图", "切换雷达显示范围", "视角与系统"),
        new("Pause", "暂停／打开菜单", "飞行中的暂停操作", "视角与系统"),
        new("Gear", "起落架", "可选功能；是否可用以游戏场景为准", "视角与系统")
    ];
    public static readonly GameAction[] Menu =
    [
        new("FaceButtonBottom", "确认（A）", "菜单中的确认", "菜单操作"),
        new("FaceButtonRight", "返回／取消（B）", "菜单中的返回", "菜单操作"),
        new("FaceButtonLeft", "界面功能（X）", "执行当前界面 X 提示", "菜单操作"),
        new("FaceButtonTop", "界面功能（Y）", "执行当前界面 Y 提示", "菜单操作"),
        new("SpecialRight", "菜单键 ≡／跳过剧情", "对应手柄三横线键；可跳过的剧情中按住使用", "菜单操作")
    ];
    public static readonly GameAction[] Axes =
    [
        new("Pitch", "俯仰", "后拉抬头、前推低头；通常使用 Y 轴并反向", "飞行控制轴"),
        new("Roll", "滚转", "左右推杆控制机翼倾斜；通常使用 X 轴", "飞行控制轴"),
        new("Yaw", "方向舵", "扭转握把，左右转机头；通常使用 Rz 轴", "飞行控制轴"),
        new("Throttle", "油门", "左手油门控制加速／减速；通常使用 Z 轴", "飞行控制轴")
    ];
    public static IEnumerable<GameAction> All => Combat.Concat(Menu).Concat(Axes);
    public static string ActionName(string key) => All.FirstOrDefault(a => a.Key == key)?.Name ?? key switch
    {
        "HatUp" => "僚机指令·上", "HatDown" => "僚机指令·下",
        "HatLeft" => "僚机指令·左", "HatRight" => "僚机指令·右",
        "DPadUp" => "菜单导航·上", "DPadDown" => "菜单导航·下",
        "DPadLeft" => "菜单导航·左", "DPadRight" => "菜单导航·右",
        "CameraPitch" => "环顾·上下", "CameraYaw" => "环顾·左右", _ => key
    };

    public static IReadOnlyList<InputChoice> ButtonChoices { get; } = CreateButtons();
    public static IReadOnlyList<InputChoice> AxisChoices { get; } =
    [
        new("未绑定", FlightInput.None), new("右手·左右推杆（X）", new("X")),
        new("右手·前后推杆（Y）", new("Y")), new("左手·油门推杆（Z）", new("Z")),
        new("右手·扭转握把（Rz）", new("Rz")), new("左手·RX 旋钮", new("Rx")),
        new("左手·RY 旋钮", new("Ry")), new("右手·底座滑块（Slider）", new("Slider"))
    ];

    private static IReadOnlyList<InputChoice> CreateButtons()
    {
        var result = new List<InputChoice> { new("未绑定", FlightInput.None) };
        for (int i = 1; i <= 16; i++)
            result.Add(new($"{(i <= 12 ? "右手" : "左手")} · {i} 号键", new("Button", i)));
        var names = new[] { "上", "下", "左", "右" };
        var types = new[] { "POV_U", "POV_D", "POV_L", "POV_R" };
        for (int i = 0; i < 4; i++) result.Add(new($"右手 · POV1 {names[i]}", new(types[i])));
        for (int i = 0; i < 4; i++) result.Add(new($"左手 · POV2 {names[i]}", new("Button", 17 + i)));
        return result;
    }

    public static string Describe(FlightInput input)
    {
        string basic = input.Type == "None" ? "未绑定" : ButtonChoices.Concat(AxisChoices)
            .FirstOrDefault(c => c.Input?.Type == input.Type && c.Input.Button == input.Button)?.Label
            ?? (input.Type == "Button" ? $"按键 {input.Button}" : input.Type);
        string modifier = input.Enable != 0 || input.Disable != 0 ? $"（组合键 {input.Enable}/{input.Disable}）" : "";
        return basic + (input.IsReverse ? " · 反向" : "") + modifier;
    }
}
