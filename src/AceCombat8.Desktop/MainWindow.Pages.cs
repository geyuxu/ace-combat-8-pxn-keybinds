using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AceCombat8.Core;

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
        ContentHost.Children.Add(PageTitle("作战按键", "选择游戏功能对应的实体键。右手是 1～12 号键，左手是 13～16 号键。"));
        ContentHost.Children.Add(ActionButton("常用布局：1 机炮 · 2 导弹 · 3 锁定 · 4 副武器", () => { Session!.ApplyCombatPreset(); RefreshAll(); }));
        foreach (var group in PxnDevice.Combat.GroupBy(a => a.Group))
        {
            var card = Card(group.Key, out var body);
            foreach (var action in group) body.Children.Add(BindingEditor(action));
            ContentHost.Children.Add(card);
        }
    }
    private void MenuPage()
    {
        ContentHost.Children.Add(PageTitle("菜单导航", "菜单与飞行使用各自的绑定，同一个实体键可以在这两个场景中承担不同功能。"));
        var card = Card("确认、返回与界面操作", out var body);
        foreach (var action in PxnDevice.Menu) body.Children.Add(BindingEditor(action));
        ContentHost.Children.Add(card);
        if (Session!.Current.TryGetValue("SpecialRight", out var menuKey))
            ContentHost.Children.Add(Info("剧情跳过用哪个键？", "手柄三横线（≡）对应“菜单键 ≡／跳过剧情”。当前绑定：" + menuKey.Describe() + "。在支持跳过的剧情中按住使用；暂停键与菜单键分别绑定。"));
        ContentHost.Children.Add(Info("方向导航", "菜单上下左右跟随“帽键与视角”页面中的僚机指令帽键。键盘 Esc 保留游戏原有功能。"));
    }
    private void AxesPage()
    {
        ContentHost.Children.Add(PageTitle("飞行轴", "按实际操作方向选择控制轴；反向勾选只影响对应功能。"));
        var card = Card("摇杆与油门", out var body);
        foreach (var action in PxnDevice.Axes) body.Children.Add(BindingEditor(action, true));
        ContentHost.Children.Add(card);
        ContentHost.Children.Add(Info("试飞核对", "后拉抬头、前推低头；左右推杆滚转；扭转握把控制方向舵；油门推杆覆盖加减速行程。"));
    }
    private FrameworkElement BindingEditor(GameAction action, bool axis = false)
    {
        var row = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
        var labels = new StackPanel { Margin = new Thickness(0, 2, 18, 0) };
        labels.Children.Add(Text(action.Name, 14));
        labels.Children.Add(Text(action.Hint, 11, "#8DA2BE", new Thickness(0, 4, 0, 0)));
        header.Children.Add(labels);
        if (!Session!.Current.TryGetValue(action.Key, out var binding))
        {
            var missing = Text("此配置没有该项目", 12, "#8DA2BE"); Grid.SetColumn(missing, 1); header.Children.Add(missing); row.Children.Add(header); return row;
        }
        var options = (axis ? PxnDevice.AxisChoices : PxnDevice.ButtonChoices).ToList();
        FlightInput? single = binding.Inputs.Count == 1 ? binding.Inputs[0] : null;
        var selected = single?.Enable == 0 && single.Disable == 0
            ? options.FirstOrDefault(c => c.Input?.Type == single.Type && c.Input.Button == single.Button && (axis || c.Input.Direction == single.Direction)) : null;
        if (binding.Inputs.Count == 0) selected = options[0];
        if (selected == null) { selected = new("保留当前高级绑定", null); options.Insert(0, selected); }
        var combo = new ComboBox { ItemsSource = options, DisplayMemberPath = "Label", SelectedItem = selected, ToolTip = binding.Describe(), VerticalAlignment = VerticalAlignment.Top, Tag = action.Key };
        System.Windows.Automation.AutomationProperties.SetName(combo, action.Name);
        Editors[action.Key] = combo;
        var selector = new StackPanel(); selector.Children.Add(combo);
        var reverse = new CheckBox { Content = "反向", IsChecked = single?.IsReverse == true, IsEnabled = selected.Input?.Type is not null and not "None", Margin = new Thickness(0, 9, 0, 0) };
        if (axis) { selector.Children.Add(reverse); if (action.Key == "Pitch") AxisReverseForTest = reverse; }
        void Changed()
        {
            if (combo.SelectedItem is not InputChoice { Input: { } input }) return;
            reverse.IsEnabled = input.Type != "None";
            Session.Set(action.Key, input with { Direction = axis && input.Type != "None" && reverse.IsChecked == true ? "Reverse" : "Standard" });
            combo.ToolTip = Session.Current[action.Key].Describe();
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
        ContentHost.Children.Add(PageTitle("帽键与视角", "按住帽键环顾，松开停止；另一侧帽键用于僚机指令和菜单方向。"));
        var card = Card("两个帽键的分工", out var body);
        HatChoice[] options = [new("右手 POV1：僚机／菜单 · 左手 POV2：环顾", "right-orders"), new("右手 POV1：环顾 · 左手 POV2：僚机／菜单", "left-orders"), new("保留当前自定义布局", "custom")];
        var combo = new ComboBox { ItemsSource = options, DisplayMemberPath = "Label", SelectedItem = options.First(o => o.Value == Session!.HatLayout) };
        HatEditor = combo;
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is not HatChoice choice || choice.Value == "custom") return;
            TryAction(() => { Session!.ApplyHatLayout(choice.Value == "left-orders"); RefreshAll(); });
        };
        body.Children.Add(combo);
        body.Children.Add(Text("POV2 使用设备原生的 17～20 号输入。无需占用 9～12 号键，也无需修改驱动映射。", 11, "#8DA2BE", new Thickness(0, 12, 0, 0)));
        ContentHost.Children.Add(card);
        var direction = Card("环顾方向", out var directions);
        directions.Children.Add(ActionButton("反转环顾上下方向", () => { Session!.ReverseCamera("CameraPitch"); RefreshAll(); }));
        directions.Children.Add(ActionButton("反转环顾左右方向", () => { Session!.ReverseCamera("CameraYaw"); RefreshAll(); }));
        ContentHost.Children.Add(direction);
        var mapping = Card("当前分配", out var entries);
        foreach (string key in EditSession.HatKeys.Where(Session!.Current.ContainsKey))
        {
            entries.Children.Add(Text(PxnDevice.ActionName(key), 12));
            entries.Children.Add(Text(Session!.Current[key].Describe(), 11, "#8DA2BE", new Thickness(0, 3, 0, 12)));
        }
        ContentHost.Children.Add(mapping);
    }
}
