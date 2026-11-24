using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AceCombat8.Core;
using Microsoft.Win32;

namespace AceCombat8.Desktop;

public partial class MainWindow : Window
{
    internal EditSession? Session { get; private set; }
    private int page;
    private bool testing;
    internal readonly Dictionary<string, ComboBox> Editors = new();
    internal CheckBox? AxisReverseForTest;
    public MainWindow()
    {
        InitializeComponent();
        Height = Math.Max(MinHeight, Math.Min(900, SystemParameters.WorkArea.Height - 40));
        Closing += OnClosing;
        RenderPage();
    }
    internal void InitializeFromArguments(string[] args)
    {
        if (args.Length > 0 && File.Exists(args[0])) TryAction(() => LoadFile(args[0]));
        else LocateClick(this, new RoutedEventArgs());
    }
    internal void LoadFile(string path, bool test = false)
    {
        var document = FlightStickDocument.Load(path);
        testing = test;
        Session = new(document);
        if (!testing) WindowsServices.Remember(document.Path);
        PathDisplay.Text = document.Path;
        FileHeading.Text = $"已读取 PXN 2119 Pro  ·  [{document.SectionName}]";
        RefreshAll();
        SetStatus("已读取配置，选择要调整的游戏功能。", "保存时自动备份；生效需要重新启动游戏。");
    }
    private void NavigateClick(object sender, RoutedEventArgs e) => SelectPage(int.Parse((string)((Button)sender).Tag));
    internal void SelectPage(int value) { page = value; RenderPage(); PageScroll.ScrollToTop(); }
    private void RefreshAll() { RenderPage(); RefreshChanges(); }
    private void RenderPage()
    {
        Editors.Clear(); AxisReverseForTest = null;
        ContentHost.Children.Clear();
        foreach (var nav in Navigation.Children.OfType<Button>())
            nav.Background = Brush((string)nav.Tag == page.ToString() ? "#24434B" : "#142236");
        if (Session == null)
        {
            ContentHost.Children.Add(PageTitle("先连接你的飞行控制器", "自动查找会读取 Steam 库，并验证配置中包含 PXN-F19。你也可以手动选择 FlightStick.ini。"));
            ContentHost.Children.Add(Info("首次使用", "把摇杆与油门连接好，使用 MODE 0。若没有配置文件，请先启动一次游戏，然后退出再查找。"));
            return;
        }
        switch (page)
        {
            case 0: CombatPage(); break;
            case 1: MenuPage(); break;
            case 2: AxesPage(); break;
            case 3: HatsPage(); break;
            case 4: BackupsPage(); break;
        }
    }
    internal void RefreshChanges()
    {
        ChangesHost.Children.Clear();
        var changes = Session?.Changes ?? [];
        ChangesHeading.Text = changes.Count == 0 ? "待保存的更改" : $"待保存 · {changes.Count} 项";
        if (changes.Count == 0) ChangesHost.Children.Add(Text("当前没有更改。\n修改左侧的按键定义后，这里会显示前后对比。", 12, "#91A5BF"));
        foreach (var change in changes)
        {
            var block = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            block.Children.Add(Text(change.Name, 13));
            block.Children.Add(Text(change.Before, 11, "#8093AD", new Thickness(0, 5, 0, 0)));
            block.Children.Add(Text("→ " + change.After, 12, "#65DED1", new Thickness(0, 4, 0, 0)));
            ChangesHost.Children.Add(block);
        }
        var conflicts = Session?.Conflicts() ?? [];
        WarningsBox.Visibility = conflicts.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        WarningsText.Text = string.Join("\n", conflicts) + "\n确认有意共用后仍可保存。";
        SaveButton.IsEnabled = changes.Count > 0;
        ReloadButton.IsEnabled = ExportButton.IsEnabled = Session != null;
        Title = $"{(changes.Count > 0 ? "● " : "")}皇牌空战8 · PXN 键位工具";
    }
    private void SetStatus(string message, string detail = "") { StatusText.Text = message; StatusDetail.Text = detail; }
    private void TryAction(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or InvalidOperationException or ArgumentException)
        {
            SetStatus("操作未完成：" + ex.Message, "原配置或可用备份仍保留；可以重新读取后重试。");
            MessageBox.Show(this, ex.Message, "操作未完成", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
    private bool CanDiscard()
    {
        if (Session?.Changes.Count is not > 0) return true;
        return MessageBox.Show(this, "有尚未保存的键位更改。要放弃这些更改吗？", "未保存的更改", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }
    private void OnClosing(object? sender, CancelEventArgs e) { if (!testing && !CanDiscard()) e.Cancel = true; }
    private void ChooseClick(object sender, RoutedEventArgs e)
    {
        if (!CanDiscard()) return;
        var dialog = new OpenFileDialog { Title = "选择皇牌空战8的 FlightStick.ini", Filter = "摇杆配置 (FlightStick.ini)|FlightStick.ini|INI 配置 (*.ini)|*.ini", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) TryAction(() => LoadFile(dialog.FileName));
    }
    private async void LocateClick(object sender, RoutedEventArgs e)
    {
        if (!CanDiscard()) return;
        LocateButton.IsEnabled = false;
        SetStatus("正在读取 Steam 库并查找当前用户配置…");
        try
        {
            var roots = WindowsServices.SteamRoots().ToArray();
            var result = await Task.Run(() => ConfigDiscovery.Find(roots, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), WindowsServices.LastPath()));
            SteamDisplay.Text = result.Games.Count > 0 ? $"Steam 已识别：{result.Games[0].Name}  ·  {result.Games[0].Library}" : "未找到 Steam 安装记录；支持直接读取当前用户配置。";
            if (result.Candidates.Count == 1) LoadFile(result.Candidates[0].Path);
            else if (result.Candidates.Count > 1)
            {
                string? selected = ChooseCandidate(result.Candidates);
                if (selected != null) LoadFile(selected);
                else SetStatus("已取消选择，当前配置保持原样。");
            }
            else SetStatus("没有找到可用配置。", string.Join(" ", result.Notes));
        }
        catch (Exception ex) { SetStatus("自动查找未完成。", ex.Message + " 可使用“选择文件”。"); }
        finally { LocateButton.IsEnabled = true; }
    }
    private void ReloadClick(object sender, RoutedEventArgs e) { if (Session != null && CanDiscard()) TryAction(() => LoadFile(Session.Document.Path)); }
    private void SaveClick(object sender, RoutedEventArgs e) => TryAction(() => SaveCurrent());
    internal SaveResult SaveCurrent(bool? gameRunning = null)
    {
        if (Session == null) throw new InvalidOperationException("请先选择配置文件。");
        var result = ConfigStore.Save(Session, () => gameRunning ?? WindowsServices.GameRunning());
        Session = new(result.Document);
        RefreshAll();
        SetStatus(result.Changed ? "键位已保存。重新启动游戏后生效。" : "配置没有变化。", result.BackupPath == null ? "" : "备份：" + System.IO.Path.GetFileName(result.BackupPath));
        return result;
    }
    private void ExportClick(object sender, RoutedEventArgs e)
    {
        if (Session == null) return;
        var dialog = new SaveFileDialog { Title = "导出可放入 Obsidian 的键位表", FileName = "PXN 2119 Pro 皇牌空战8键位表.md", Filter = "Markdown 笔记 (*.md)|*.md" };
        if (dialog.ShowDialog(this) == true) TryAction(() =>
        {
            File.WriteAllText(dialog.FileName, Session.ToMarkdown(), new System.Text.UTF8Encoding(false));
            SetStatus("键位表已导出。", Session.Changes.Count > 0 ? "导出内容包含当前预览中的更改；这些更改仍需保存到游戏。" : dialog.FileName);
        });
    }
}
