using System.Windows;
using System.Windows.Controls;
using AceCombat8.Core;
using static AceCombat8.Core.Lang;

namespace AceCombat8.Desktop;

public partial class MainWindow
{
    private void ActionFilterChanged(object sender, RoutedEventArgs e)
    {
        if (Session == null) return;
        if (ReferenceEquals(sender, ShowOptional)) DrawEditor();
        RefreshChanges();
    }

    private void RefreshSelectedBinding()
    {
        if (page != 0 || Session == null || selectedInput == null)
        {
            SelectedBindingCard.Visibility = Visibility.Collapsed;
            return;
        }
        var matches = Session.BindingsFor(selectedInput);
        SelectedBindingCard.Visibility = Visibility.Visible;
        SelectedBindingTitle.Text = ControllerInputs.Short(selectedInput, Session.Document.Device.IsPxn);
        SelectedBindingActions.Text = matches.Count == 0 ? T("未绑定", "Unbound", "未割り当て") : BindingLabels(matches);
    }

    private static string BindingLabels(IEnumerable<PhysicalBinding> matches) => string.Join("\n", matches.Select(binding => binding.Key)
        .Distinct(StringComparer.Ordinal).Select(key => $"{ActionCatalog.Context(key)} · {ActionCatalog.Name(key)}"));

    private void DrawActionChips()
    {
        var session = Session!;
        bool onlyMissing = OnlyMissing.IsChecked == true;
        bool showOptional = ShowOptional.IsChecked == true;
        var entries = session.Current.Where(pair => !onlyMissing || !session.IsBound(pair.Key)).ToArray();
        var common = entries.Where(pair => ActionCatalog.IsCommon(pair.Key)).ToArray();
        var optional = entries.Where(pair => !ActionCatalog.IsCommon(pair.Key)
            && (showOptional || session.IsBound(pair.Key))).ToArray();
        if (common.Length != 0)
        {
            AddChipGroup(T("常用操作", "Common actions", "よく使う操作"), common);
        }
        if (optional.Length != 0)
        {
            AddChipGroup(T("可选项与手柄输入槽", "Optional and gamepad slots", "オプション・ゲームパッド入力"), optional);
        }
        if (common.Length == 0 && optional.Length == 0)
        {
            ChangesHost.Children.Add(Text(T("这个筛选下没有项目。", "No entries match this filter.", "この条件に該当する項目はありません。"), 11, "#A2B1C6"));
        }
    }

    private void AddChipGroup(string title, IEnumerable<KeyValuePair<string, InputBinding>> entries)
    {
        ChangesHost.Children.Add(Text(title, 12, "#A2B1C6", new Thickness(0, 2, 0, 7)));
        foreach (var (key, binding) in entries.OrderBy(pair => Session!.IsBound(pair.Key)))
        {
            bool bound = Session!.IsBound(key);
            bool missing = ActionCatalog.IsCommon(key) && !bound;
            var name = Text(ActionCatalog.Name(key), 12, missing ? "#FFD692" : "#E9F0FA");
            name.FontWeight = FontWeights.SemiBold;
            var chip = new StackPanel();
            chip.Children.Add(name);
            chip.Children.Add(Text(Session.Describe(binding), 11, bound ? "#9EE0DB" : missing ? "#F2BE71" : "#A2B1C6", new Thickness(0, 3, 0, 0)));
            ChangesHost.Children.Add(new Border
            {
                Child = chip, Tag = key, ToolTip = ActionCatalog.Hold(key) ?? ActionCatalog.Context(key),
                Background = Brush(missing ? "#33271A" : bound ? "#163040" : "#142236"),
                BorderBrush = Brush(missing ? "#6E482A" : "#2A485A"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9), Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 0, 6)
            });
        }
    }
}
