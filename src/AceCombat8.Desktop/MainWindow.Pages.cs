using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AceCombat8.Core;
using static AceCombat8.Core.Lang;

namespace AceCombat8.Desktop;

public partial class MainWindow
{
    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    private static TextBlock Text(string value, double size = 14, string color = "#E9F0FA", Thickness? margin = null) =>
        new() { Text = value, FontSize = size, Foreground = Brush(color), TextWrapping = TextWrapping.Wrap, Margin = margin ?? new Thickness(0) };
    private static FrameworkElement PageTitle(string name, string description)
    {
        var block = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
        var title = Text(name, 22); title.FontWeight = FontWeights.SemiBold;
        block.Children.Add(title);
        block.Children.Add(Text(description, 12, "#A2B1C6", new Thickness(0, 7, 0, 0)));
        return block;
    }
    private static Border Card(string heading, out StackPanel body)
    {
        body = new StackPanel();
        var title = Text(heading, 15); title.FontWeight = FontWeights.SemiBold;
        title.Margin = new Thickness(0, 0, 0, 12);
        body.Children.Add(title);
        return new Border { Background = Brush("#132032"), BorderBrush = Brush("#26384E"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(17), Margin = new Thickness(0, 0, 0, 14), Child = body };
    }
    private static Border Info(string heading, string content)
    {
        var card = Card(heading, out var body);
        body.Children.Add(Text(content, 12, "#AFC0D6"));
        return card;
    }
    private Button ActionButton(string text, Action clicked)
    {
        var button = new Button { Content = text, Margin = new Thickness(0, 0, 0, 14), HorizontalAlignment = HorizontalAlignment.Left };
        button.Click += (_, _) => TryAction(clicked);
        return button;
    }
    private void CombatPage()
    {
        ContentHost.Children.Add(PageTitle(T("作战按键", "Combat actions", "戦闘操作"), T("按操作查看和调整常用绑定。", "Review common combat inputs by action.", "戦闘操作ごとに割り当てを確認します。")));
        if (Session!.Document.Device.IsPxn)
            ContentHost.Children.Add(ActionButton(T("应用 PXN 布局 · 1 机炮 / 2 导弹 / 3 锁定 / 4 副武器",
                "Apply PXN preset · 1 Gun / 2 Missile / 3 Target / 4 Weapon", "PXN プリセット · 1 機銃 / 2 ミサイル / 3 ターゲット / 4 兵装"),
                () => { Session.ApplyCombatPreset(); RefreshAll(); }));
        foreach (var group in PxnDevice.Combat.GroupBy(a => a.Group))
        {
            string groupLabel = group.Key switch
            {
                "武器与目标" => T(group.Key, "Weapons & targets", "兵装・ターゲット"),
                "飞行与防御" => T(group.Key, "Flight & defense", "飛行・防御"),
                _ => T(group.Key, "View & systems", "視点・システム")
            };
            var card = Card(groupLabel, out var body);
            foreach (var action in group) body.Children.Add(BindingEditor(action));
            ContentHost.Children.Add(card);
        }
    }
    private void MenuPage()
    {
        ContentHost.Children.Add(PageTitle(T("菜单导航", "Menu controls", "メニュー操作"),
            T("菜单和飞行可以共用同一个实体键。", "Menu and flight actions can share a physical input.", "メニューと飛行の操作で同じ物理入力を使えます。")));
        var card = Card(T("确认、返回与界面操作", "Confirm, back & menu actions", "決定・戻る・メニュー操作"), out var body);
        foreach (var action in PxnDevice.Menu) body.Children.Add(BindingEditor(action));
        ContentHost.Children.Add(card);
        if (Session!.Current.TryGetValue("SpecialRight", out var menuKey))
            ContentHost.Children.Add(Info(ActionCatalog.Name("SpecialRight"),
                Session.Describe(menuKey) + "\n" + ActionCatalog.Hold("SpecialRight")));
    }
    private void AxesPage()
    {
        ContentHost.Children.Add(PageTitle(T("飞行轴", "Flight axes", "フライト軸"),
            T("选择实体轴，并按需要勾选反向。", "Choose a physical axis and reverse its direction as needed.", "物理軸を選択し、必要に応じて反転します。")));
        var card = Card(T("摇杆与油门", "Stick & throttle", "スティック・スロットル"), out var body);
        foreach (var action in PxnDevice.Axes) body.Children.Add(BindingEditor(action, true));
        ContentHost.Children.Add(card);
        ContentHost.Children.Add(Info(T("试飞核对", "Flight check", "飛行チェック"),
            T("在训练中检查俯仰、滚转、方向舵和油门方向。", "Verify pitch, roll, yaw and throttle direction in training.", "訓練でピッチ、ロール、ヨー、スロットルの向きを確認します。")));
    }
    private FrameworkElement BindingEditor(GameAction action, bool axis = false)
    {
        var row = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
        var labels = new StackPanel { Margin = new Thickness(0, 2, 18, 0) };
        string name = ActionCatalog.Name(action.Key);
        labels.Children.Add(Text(name, 14));
        if (ActionCatalog.Hold(action.Key) is { } hint)
            labels.Children.Add(Text(hint, 11, "#8DA2BE", new Thickness(0, 4, 0, 0)));
        header.Children.Add(labels);
        if (!Session!.Current.TryGetValue(action.Key, out var binding))
        {
            var missing = Text(T("配置中无此操作", "Unavailable in this profile", "この設定では使用できません"), 12, "#8DA2BE");
            Grid.SetColumn(missing, 1); header.Children.Add(missing); row.Children.Add(header); return row;
        }
        var existing = binding.Inputs.Where(i => i.Type != "None").ToArray();
        if (existing.Length > 1 || existing.Any(i => i.Enable != 0 || i.Disable != 0 || i.Direction is not ("Standard" or "Reverse")))
        {
            labels.Children.Add(Text(Session.Describe(binding), 11, "#8DA2BE"));
            var edit = ActionButton(T("按实体输入编辑", "Edit by physical input", "物理入力で編集"), () =>
            {
                SelectPage(0);
                SelectInput(new FlightInput(existing[0].Type, existing[0].Button));
            });
            Grid.SetColumn(edit, 1);
            header.Children.Add(edit);
            row.Children.Add(header);
            return row;
        }
        bool pxn = Session.Document.Device.IsPxn;
        var options = ControllerInputs.List(Session, capabilities)
            .Where(i => ActionCatalog.IsAxisInput(i) == axis)
            .Select(i => new InputChoice(ControllerInputs.Describe(i, pxn), i)).Prepend(
                new(T("未绑定", "Unbound", "未割り当て"), FlightInput.None)).ToList();
        FlightInput? single = binding.Inputs.Count == 1 ? binding.Inputs[0] : null;
        var selected = single?.Enable == 0 && single.Disable == 0
            ? options.FirstOrDefault(c => c.Input?.Type == single.Type && c.Input.Button == single.Button && (axis || c.Input.Direction == single.Direction)) : null;
        if (binding.Inputs.Count == 0) selected = options[0];
        if (selected == null) { selected = new(T("保留当前绑定", "Keep existing binding", "現在の割り当てを保持"), null); options.Insert(0, selected); }
        var combo = new ComboBox { ItemsSource = options, DisplayMemberPath = "Label", SelectedItem = selected, ToolTip = Session.Describe(binding), VerticalAlignment = VerticalAlignment.Top, Tag = action.Key };
        System.Windows.Automation.AutomationProperties.SetName(combo, name);
        Editors[action.Key] = combo;
        var selector = new StackPanel(); selector.Children.Add(combo);
        var reverse = new CheckBox { Content = T("反向", "Reverse", "反転"), IsChecked = single?.IsReverse == true, IsEnabled = selected.Input?.Type is not null and not "None", Margin = new Thickness(0, 9, 0, 0) };
        if (axis) { selector.Children.Add(reverse); if (action.Key == "Pitch") AxisReverseForTest = reverse; }
        void Changed()
        {
            if (combo.SelectedItem is not InputChoice { Input: { } input }) return;
            reverse.IsEnabled = input.Type != "None";
            Session.Set(action.Key, input with { Direction = axis && input.Type != "None" && reverse.IsChecked == true ? "Reverse" : "Standard" });
            combo.ToolTip = Session.Describe(Session.Current[action.Key]);
            RefreshChanges();
        }
        combo.SelectionChanged += (_, _) => Changed();
        reverse.Checked += (_, _) => Changed(); reverse.Unchecked += (_, _) => Changed();
        Grid.SetColumn(selector, 1); header.Children.Add(selector); row.Children.Add(header);
        return row;
    }
    private sealed record HatChoice(string Label, string Value);
    internal ComboBox? HatEditor;
    private void HatsPage()
    {
        ContentHost.Children.Add(PageTitle(T("帽键与视角", "Hats & camera", "ハット・視点"),
            T("检查僚机指令、菜单方向和环顾的实体输入。", "Review inputs for wingman commands, menu directions and camera look.", "僚機指示、メニュー方向、見回しの入力を確認します。")));
        if (Session!.Document.Device.IsPxn && EditSession.HatKeys.All(Session.Current.ContainsKey))
        {
            var card = Card(T("PXN 帽键布局", "PXN hat layout", "PXN ハット配置"), out var body);
            HatChoice[] options =
            [
                new(T("POV1 僚机／菜单 · POV2 环顾", "POV1 wingman/menu · POV2 look", "POV1 僚機・メニュー / POV2 見回し"), "right-orders"),
                new(T("POV1 环顾 · POV2 僚机／菜单", "POV1 look · POV2 wingman/menu", "POV1 見回し / POV2 僚機・メニュー"), "left-orders"),
                new(T("自定义布局", "Custom layout", "カスタム配置"), "custom")
            ];
            var combo = new ComboBox { ItemsSource = options, DisplayMemberPath = "Label", SelectedItem = options.First(o => o.Value == Session.HatLayout) };
            HatEditor = combo;
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is not HatChoice choice || choice.Value == "custom") return;
                TryAction(() => { Session.ApplyHatLayout(choice.Value == "left-orders"); RefreshAll(); });
            };
            body.Children.Add(combo);
            body.Children.Add(Text(T("此预设替换帽键和环顾绑定。PXN 的 POV2 使用 17～20 号输入。",
                "This preset replaces hat and camera bindings. PXN POV2 uses inputs 17–20.",
                "このプリセットはハットと見回しの割り当てを置き換えます。PXN の POV2 は入力 17～20 を使用します。"), 11, "#8DA2BE", new Thickness(0, 12, 0, 0)));
            ContentHost.Children.Add(card);
        }
        var direction = Card(T("环顾方向", "Camera directions", "見回しの向き"), out var directions);
        if (Session.Current.ContainsKey("CameraPitch"))
            directions.Children.Add(ActionButton(T("反转环顾上下", "Invert vertical look", "上下の見回しを反転"), () => { Session.ReverseCamera("CameraPitch"); RefreshAll(); }));
        if (Session.Current.ContainsKey("CameraYaw"))
            directions.Children.Add(ActionButton(T("反转环顾左右", "Invert horizontal look", "左右の見回しを反転"), () => { Session.ReverseCamera("CameraYaw"); RefreshAll(); }));
        if (directions.Children.Count > 1) ContentHost.Children.Add(direction);
        var mapping = Card(T("当前分配", "Current assignments", "現在の割り当て"), out var entries);
        foreach (string key in EditSession.HatKeys.Where(Session!.Current.ContainsKey))
        {
            entries.Children.Add(Text(ActionCatalog.Name(key), 12));
            entries.Children.Add(Text(Session.Describe(Session.Current[key]), 11, "#8DA2BE", new Thickness(0, 3, 0, 12)));
        }
        ContentHost.Children.Add(mapping);
    }
}
