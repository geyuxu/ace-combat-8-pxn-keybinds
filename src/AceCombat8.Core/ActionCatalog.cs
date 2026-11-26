namespace AceCombat8.Core;

public static class ActionCatalog
{
    private static readonly Dictionary<string, (string English, string Japanese)> Labels = new()
    {
        ["Gun"] = ("Gun", "機銃"), ["Missile"] = ("Fire missile / special weapon", "ミサイル・特殊兵装を発射"),
        ["Weapon"] = ("Switch weapon", "兵装切り替え"), ["Target"] = ("Switch target", "ターゲット切り替え"),
        ["Radar"] = ("Radar map", "レーダーマップ切り替え"), ["AccelDecel"] = ("High-G turn", "ハイGターン"),
        ["AutoPilot"] = ("Autopilot", "オートパイロット"), ["Flare"] = ("Flares", "フレア"),
        ["View"] = ("Change view", "視点切り替え"), ["Pause"] = ("Pause", "ポーズ"), ["Gear"] = ("Landing gear", "ランディングギア"),
        ["FaceButtonBottom"] = ("Confirm (A)", "決定（A）"), ["FaceButtonRight"] = ("Back (B)", "戻る（B）"),
        ["FaceButtonLeft"] = ("Menu action (X)", "メニュー操作（X）"), ["FaceButtonTop"] = ("Menu action (Y)", "メニュー操作（Y）"),
        ["SpecialRight"] = ("Menu ≡ / Skip cutscene", "メニュー ≡・ムービースキップ"),
        ["SpecialLeft"] = ("View button", "ビュー ボタン"), ["TouchPad"] = ("Touchpad", "タッチパッド"), ["Platform"] = ("Platform button", "プラットフォーム ボタン"),
        ["LeftShoulder"] = ("Left bumper", "左バンパー"), ["RightShoulder"] = ("Right bumper", "右バンパー"),
        ["LeftTrigger"] = ("Left trigger", "左トリガー"), ["RightTrigger"] = ("Right trigger", "右トリガー"),
        ["LeftThumbstick"] = ("Left stick click", "左スティック押し込み"), ["RightThumbstick"] = ("Right stick click", "右スティック押し込み"),
        ["Pitch"] = ("Pitch", "ピッチ"), ["Roll"] = ("Roll", "ロール"), ["Yaw"] = ("Yaw", "ヨー"), ["Throttle"] = ("Throttle", "スロットル"),
        ["HatUp"] = ("Wingman · Up", "僚機指示・上"), ["HatDown"] = ("Wingman · Down", "僚機指示・下"),
        ["HatLeft"] = ("Wingman · Left", "僚機指示・左"), ["HatRight"] = ("Wingman · Right", "僚機指示・右"),
        ["DPadUp"] = ("Menu · Up", "メニュー・上"), ["DPadDown"] = ("Menu · Down", "メニュー・下"),
        ["DPadLeft"] = ("Menu · Left", "メニュー・左"), ["DPadRight"] = ("Menu · Right", "メニュー・右"),
        ["CameraPitch"] = ("Look up / down", "見回し・上下"), ["CameraYaw"] = ("Look left / right", "見回し・左右"),
        ["OnlineLobbyAvatarDash"] = ("Lobby avatar dash", "ロビーでダッシュ"),
        ["OnlineLobbyAvatarJump"] = ("Lobby avatar jump", "ロビーでジャンプ")
    };

    private static readonly Dictionary<string, string> ExtraChinese = new()
    {
        ["SpecialLeft"] = "查看键", ["TouchPad"] = "触摸板", ["Platform"] = "平台键",
        ["LeftShoulder"] = "左肩键", ["RightShoulder"] = "右肩键",
        ["LeftTrigger"] = "左扳机", ["RightTrigger"] = "右扳机",
        ["LeftThumbstick"] = "左摇杆按下", ["RightThumbstick"] = "右摇杆按下",
        ["OnlineLobbyAvatarDash"] = "大厅角色冲刺", ["OnlineLobbyAvatarJump"] = "大厅角色跳跃"
    };

    public static string Name(string key)
    {
        if (Labels.TryGetValue(key, out var label))
            return Lang.T(ExtraChinese.GetValueOrDefault(key) ?? PxnDevice.ActionName(key), label.English, label.Japanese);
        if (key.StartsWith("ExtraButton_", StringComparison.Ordinal))
            return Lang.T("附加键 ", "Extra button ", "追加ボタン ") + key["ExtraButton_".Length..];
        return key;
    }

    public static string? Hold(string key) => key switch
    {
        "Target" => Lang.T("轻按切换锁定目标，长按注视目标。两个行为共用同一原生操作。", "Tap to switch targets. Hold to look at the target. Both behaviors share one native action.", "短押しでターゲット切り替え、長押しでターゲット注視。同じゲーム内操作に属します。"),
        "SpecialRight" => Lang.T("在可跳过的剧情中长按。剧情跳过与手柄菜单键共用绑定。", "Hold during a skippable cutscene. Skip and the controller menu button share a binding.", "スキップ可能なムービー中に長押し。メニューボタンと割り当てを共有します。"),
        "Gun" => Lang.T("按住持续射击。", "Hold for sustained fire.", "押している間は射撃を続けます。"),
        "AutoPilot" => Lang.T("按住使用自动驾驶。", "Hold to use autopilot.", "押している間はオートパイロットが作動します。"),
        _ => null
    };

    public static bool IsAxis(string key) => key is "Pitch" or "Roll" or "Yaw" or "Throttle";
    public static bool IsCamera(string key) => key is "CameraPitch" or "CameraYaw";
    public static bool IsAxisInput(FlightInput input) => input.Type is "X" or "Y" or "Z" or "Rx" or "Ry" or "Rz" or "Slider";
    public static bool CanAssign(string key, FlightInput input)
    {
        if (IsAxisInput(input)) return IsAxis(key) || IsCamera(key);
        bool digital = (input.Type == "Button" && input.Button is >= 1 and <= 128)
            || ControllerInputs.HatTypes.Contains(input.Type, StringComparer.Ordinal) && input.Button == 0;
        return digital && !IsAxis(key);
    }
    public static bool IsMenu(string key) => key.StartsWith("FaceButton", StringComparison.Ordinal)
        || key.StartsWith("DPad", StringComparison.Ordinal) || key is "SpecialRight" or "SpecialLeft" or "TouchPad" or "Platform";
}
