using System.Windows;
using System.Windows.Controls;
using AceCombat8.Core;
using static AceCombat8.Core.Lang;

namespace AceCombat8.Desktop;

public partial class MainWindow
{
    private void DrawActionChips()
    {
        var session = Session!;
        var filter = new CheckBox
        {
            IsChecked = onlyMissing, Content = T("只看未绑定", "Show unbound only", "未割り当てのみ表示"),
            Margin = new Thickness(0, 2, 0, 12)
        };
        filter.Checked += (_, _) => { onlyMissing = true; RefreshChanges(); };
        filter.Unchecked += (_, _) => { onlyMissing = false; RefreshChanges(); };
        ChangesHost.Children.Add(filter);
        foreach (var (key, binding) in session.Current.OrderBy(p => !session.IsBound(p.Key)))
        {
            bool bound = session.IsBound(key);
            if (bound && onlyMissing) continue;
            var name = Text(ActionCatalog.Name(key), 12, bound ? "#E9F0FA" : "#FFD692");
            name.FontWeight = FontWeights.SemiBold;
            var chip = new StackPanel();
            chip.Children.Add(name);
            chip.Children.Add(Text(session.Describe(binding), 11, bound ? "#9EE0DB" : "#F2BE71", new Thickness(0, 3, 0, 0)));
            if (ActionCatalog.Hold(key) is { } hint)
                name.ToolTip = hint;
            var box = new Border
            {
                Child = chip, Background = Brush(bound ? "#163040" : "#33271A"),
                BorderBrush = Brush(bound ? "#2A485A" : "#6E482A"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9), Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 0, 6)
            };
            ChangesHost.Children.Add(box);
        }
    }
}
