using System.Windows;
using System.Windows.Controls;
using AceCombat8.Core;
using static AceCombat8.Core.Lang;

namespace AceCombat8.Desktop;

public partial class MainWindow
{
    private void BackupsPage()
    {
        ContentHost.Children.Add(PageTitle(T("备份与恢复", "Backups", "バックアップ"), T("保存前会在配置文件旁创建备份。", "Saving creates a backup beside the configuration file.", "保存前に設定ファイルと同じ場所へバックアップを作成します。")));
        ContentHost.Children.Add(ActionButton(T("打开配置目录", "Open configuration folder", "設定フォルダーを開く"), () => WindowsServices.OpenFolder(Session!.Document.Path)));
        var card = Card(T("历史备份", "Backup history", "バックアップ履歴"), out var body);
        var backups = ConfigStore.ListBackups(Session!.Document.Path);
        if (backups.Count == 0) body.Children.Add(Text(T("还没有备份。", "No backups yet.", "バックアップはまだありません。"), 12, "#A2B1C6"));
        else
        {
            var list = new ListBox { MaxHeight = 280, Background = Brush("#0E1929"), Foreground = Brush("#E9F0FA"), BorderBrush = Brush("#334861"), Padding = new Thickness(5) };
            ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
            foreach (var item in backups)
                list.Items.Add(new ListBoxItem { Content = Text(item.Label, 11), Tag = item, ToolTip = item.Path, Padding = new Thickness(9, 8, 9, 8) });
            body.Children.Add(list);
            list.SelectedIndex = 0;
            var restore = ActionButton(T("预览所选备份", "Preview selected backup", "選択したバックアップをプレビュー"), () =>
            {
                if (list.SelectedItem is not ListBoxItem { Tag: BackupEntry entry }) return;
                if (!CanDiscard()) return;
                var restored = ConfigStore.PrepareRestore(Session.Document, entry.Path);
                Session = restored;
                SelectPage(0); RefreshChanges();
                SetStatus(T("已载入备份预览。", "Backup loaded for preview.", "バックアップをプレビューしました。"),
                    T("核对后保存当前设备的绑定。", "Review and save the selected device’s bindings.", "内容を確認し、選択中の機器の割り当てを保存してください。"));
            });
            restore.Margin = new Thickness(0, 14, 0, 0); body.Children.Add(restore);
        }
        ContentHost.Children.Add(card);
        ContentHost.Children.Add(Info(T("恢复范围", "Restore scope", "復元範囲"),
            T("预览和保存只针对所选设备。", "Preview and save apply to the selected device.", "プレビューと保存は選択中の機器に適用されます。")));
    }
    private string? ChooseCandidate(IReadOnlyList<ConfigCandidate> candidates)
    {
        string? chosen = null;
        var dialog = new Window { Title = T("选择游戏配置", "Choose a configuration", "ゲーム設定を選択"), Owner = this, Width = 880, Height = 420, MinWidth = 600, Background = Brush("#0B1422"), WindowStartupLocation = WindowStartupLocation.CenterOwner, FontFamily = FontFamily };
        var grid = new Grid { Margin = new Thickness(22) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.Children.Add(Text(T("找到多个配置，请选择要编辑的文件。", "Several configurations were found. Choose a file to edit.", "設定が複数見つかりました。編集するファイルを選択してください。"), 15));
        var list = new ListBox { Background = Brush("#132032"), BorderBrush = Brush("#334861"), Margin = new Thickness(0, 15, 0, 15) };
        foreach (var item in candidates) list.Items.Add(new ListBoxItem { Content = Text(item.Label, 12), Tag = item, Padding = new Thickness(10) });
        Grid.SetRow(list, 1); grid.Children.Add(list);
        var button = new Button { Content = T("打开所选文件", "Open selected file", "選択したファイルを開く"), HorizontalAlignment = HorizontalAlignment.Right, Style = (Style)FindResource("PrimaryButton"), IsEnabled = false };
        list.SelectionChanged += (_, _) => button.IsEnabled = list.SelectedItem != null;
        button.Click += (_, _) => { chosen = ((ConfigCandidate)((ListBoxItem)list.SelectedItem).Tag).Path; dialog.DialogResult = true; };
        Grid.SetRow(button, 2); grid.Children.Add(button);
        dialog.Content = grid; dialog.ShowDialog();
        return chosen;
    }
}
