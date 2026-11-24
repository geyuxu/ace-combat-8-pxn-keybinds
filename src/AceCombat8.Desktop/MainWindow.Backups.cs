using System.Windows;
using System.Windows.Controls;
using AceCombat8.Core;

namespace AceCombat8.Desktop;

public partial class MainWindow
{
    private void BackupsPage()
    {
        ContentHost.Children.Add(PageTitle("备份与恢复", "每次保存之前，工具都会在配置文件旁保留一份原始备份。也能读取之前手动调整时留下的备份。"));
        ContentHost.Children.Add(ActionButton("打开配置文件所在文件夹", () => WindowsServices.OpenFolder(Session!.Document.Path)));
        var card = Card("历史备份", out var body);
        var backups = ConfigStore.ListBackups(Session!.Document.Path);
        if (backups.Count == 0) body.Children.Add(Text("还没有备份。首次保存更改时会自动创建。", 12, "#A2B1C6"));
        else
        {
            var list = new ListBox { MaxHeight = 280, Background = Brush("#0E1929"), Foreground = Brush("#E9F0FA"), BorderBrush = Brush("#334861"), Padding = new Thickness(5) };
            ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
            foreach (var item in backups)
                list.Items.Add(new ListBoxItem { Content = Text(item.Label, 11), Tag = item, ToolTip = item.Path, Padding = new Thickness(9, 8, 9, 8) });
            body.Children.Add(list);
            list.SelectedIndex = 0;
            var restore = ActionButton("载入选中备份，先预览", () =>
            {
                if (list.SelectedItem is not ListBoxItem { Tag: BackupEntry entry }) return;
                if (!CanDiscard()) return;
                var restored = ConfigStore.PrepareRestore(Session.Document, entry.Path);
                Session = restored;
                SelectPage(0); RefreshChanges();
                SetStatus("备份键位已载入预览，尚未写入游戏。", "核对右侧更改后，点击“保存到游戏”；只恢复 PXN 设备区段。");
            });
            restore.Margin = new Thickness(0, 14, 0, 0); body.Children.Add(restore);
        }
        ContentHost.Children.Add(card);
        ContentHost.Children.Add(Info("恢复的范围", "载入备份只准备 PXN 2119 Pro 的键位更改，不会立刻覆盖配置。保存后会再备份当前版本；其他设备区段保持原样。"));
    }
    private string? ChooseCandidate(IReadOnlyList<ConfigCandidate> candidates)
    {
        string? chosen = null;
        var dialog = new Window { Title = "选择要修改的游戏配置", Owner = this, Width = 880, Height = 420, MinWidth = 600, Background = Brush("#0B1422"), WindowStartupLocation = WindowStartupLocation.CenterOwner, FontFamily = FontFamily };
        var grid = new Grid { Margin = new Thickness(22) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.Children.Add(Text("发现多个有效的 PXN 配置。请选择你要修改的文件：", 15));
        var list = new ListBox { Background = Brush("#132032"), BorderBrush = Brush("#334861"), Margin = new Thickness(0, 15, 0, 15) };
        foreach (var item in candidates) list.Items.Add(new ListBoxItem { Content = Text(item.Label, 12), Tag = item, Padding = new Thickness(10) });
        Grid.SetRow(list, 1); grid.Children.Add(list);
        var button = new Button { Content = "读取选中配置", HorizontalAlignment = HorizontalAlignment.Right, Style = (Style)FindResource("PrimaryButton"), IsEnabled = false };
        list.SelectionChanged += (_, _) => button.IsEnabled = list.SelectedItem != null;
        button.Click += (_, _) => { chosen = ((ConfigCandidate)((ListBoxItem)list.SelectedItem).Tag).Path; dialog.DialogResult = true; };
        Grid.SetRow(button, 2); grid.Children.Add(button);
        dialog.Content = grid; dialog.ShowDialog();
        return chosen;
    }
}
