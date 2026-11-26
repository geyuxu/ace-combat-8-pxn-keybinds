using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AceCombat8.Core;
using Microsoft.Win32;
using static AceCombat8.Core.Lang;

namespace AceCombat8.Desktop;

public partial class MainWindow : Window
{
    internal EditSession? Session { get; private set; }
    private int page;
    private bool testing;
    private bool settingDevice;
    private string? steamGame;
    internal readonly Dictionary<string, ComboBox> Editors = new();
    internal CheckBox? AxisReverseForTest;
    public MainWindow()
    {
        InitializeComponent();
        InitializeLanguage();
        Height = Math.Max(MinHeight, Math.Min(900, SystemParameters.WorkArea.Height - 40));
        Closing += OnClosing;
        RenderPage();
    }
    internal void InitializeFromArguments(string[] args)
    {
        if (args.Length > 0 && File.Exists(args[0])) TryAction(() => LoadFile(args[0]));
        else LocateClick(this, new RoutedEventArgs());
    }
    internal void LoadFile(string path, bool test = false, DeviceSection? device = null)
    {
        var document = FlightStickDocument.Load(path, device);
        testing = test;
        Session = new(document);
        ResetInputSelection();
        if (!testing) WindowsServices.Remember(document.Path);
        PathDisplay.Text = document.Path;
        UpdateDevicePicker();
        RefreshAll();
        SetLoadedStatus();
    }
    private void ResetInputSelection()
    {
        selectedInput = null;
        selectedAction = null;
        placePoint = false;
        if (Session != null)
            capabilities = DirectInputDevices.Read().GetValueOrDefault(Session.Document.Device.Id);
    }
    private void UpdateDevicePicker()
    {
        var document = Session!.Document;
        settingDevice = true;
        DevicePicker.ItemsSource = document.Devices;
        DevicePicker.SelectedItem = document.Device;
        DevicePicker.Visibility = document.Devices.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        settingDevice = false;
        FileHeading.Text = document.Device.Label;
        HardwareNote.Text = document.Device.IsPxn
            ? T("PXN 使用 MODE 0，T 连发保持 OFF。", "PXN: use MODE 0 with turbo OFF.", "PXN は MODE 0、連射 OFF で使用してください。")
            : capabilities is { } caps
                ? T($"DirectInput · {caps.Buttons} 键 / {caps.Axes} 轴", $"DirectInput · {caps.Buttons} buttons / {caps.Axes} axes", $"DirectInput · {caps.Buttons} ボタン / {caps.Axes} 軸")
                : T("使用文件中的设备配置。", "Using the device profile in the file.", "ファイル内の機器設定を使用しています。");
    }
    private void DeviceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (settingDevice || Session == null || DevicePicker.SelectedItem is not DeviceSection device || device == Session.Document.Device) return;
        if (CanDiscard()) TryAction(() => LoadFile(Session.Document.Path, testing, device));
        UpdateDevicePicker();
    }
    private void NavigateClick(object sender, RoutedEventArgs e) => SelectPage(int.Parse((string)((Button)sender).Tag));
    internal void SelectPage(int value) { page = value; RefreshAll(); PageScroll.ScrollToTop(); }
    private void RefreshAll() { RenderPage(); RefreshChanges(); }
    private void RenderPage()
    {
        Editors.Clear(); AxisReverseForTest = null;
        inputEditor = null; inputPicker = null; AddActionPicker = null; AddMappingButton = null;
        ContentHost.Children.Clear();
        foreach (var nav in Navigation.Children.OfType<Button>())
            nav.Background = Brush((string)nav.Tag == page.ToString() ? "#24434B" : "#142236");
        if (Session == null)
        {
            ContentHost.Children.Add(PageTitle(T("连接飞行控制器", "Connect a flight controller", "フライトコントローラーを接続"),
                T("自动查找 Steam 游戏配置，也可手动选择 FlightStick.ini。", "Find your Steam game configuration or choose FlightStick.ini.", "Steam の設定を検索するか、FlightStick.ini を選択します。")));
            ContentHost.Children.Add(Info(T("首次使用", "First setup", "初回設定"),
                T("连接设备并启动一次游戏，退出后再查找配置。", "Connect the controller, launch the game once, then exit and search for its configuration.", "機器を接続し、ゲームを一度起動・終了してから設定を検索します。")));
            return;
        }
        switch (page)
        {
            case 0: PhysicalPage(); break;
            case 5: CombatPage(); break;
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
        ChangesHeading.Text = T("操作绑定", "Action bindings", "操作の割り当て");
        int missing = Session?.UnboundActions.Count ?? 0;
        ChangesDetail.Text = Session == null ? "" : missing > 0
            ? T($"⚠ {missing} 项常用操作未绑定，仍可保存。", $"⚠ {missing} common actions are unbound. You can still save.", $"⚠ よく使う操作の未割り当てが {missing} 件あります。保存できます。")
            : T("常用操作均已绑定。", "All common actions are bound.", "よく使う操作はすべて割り当て済みです。");
        ActionFilters.Visibility = Session == null ? Visibility.Collapsed : Visibility.Visible;
        OnlyMissing.Content = T("只看未绑定", "Show unbound only", "未割り当てのみ表示");
        ShowOptional.Content = T("显示可选项与手柄输入槽", "Show optional and gamepad slots", "オプションとゲームパッド入力を表示");
        RefreshSelectedBinding();
        ChangesDetail.Foreground = Brush(missing > 0 ? "#F4C986" : "#A2B1C6");
        if (changes.Count > 0)
            ChangesHost.Children.Add(Text(T($"待保存 · {changes.Count} 项", $"Pending · {changes.Count} changes", $"未保存 · {changes.Count} 件"), 13, "#65DED1", new Thickness(0, 0, 0, 12)));
        foreach (var change in changes)
        {
            var block = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            block.Children.Add(Text(change.Name, 13));
            block.Children.Add(Text(change.Before, 11, "#8093AD", new Thickness(0, 5, 0, 0)));
            block.Children.Add(Text("→ " + change.After, 12, "#65DED1", new Thickness(0, 4, 0, 0)));
            ChangesHost.Children.Add(block);
        }
        if (Session != null) DrawActionChips();
        var conflicts = Session?.Conflicts() ?? [];
        WarningsBox.Visibility = conflicts.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        WarningsText.Text = string.Join("\n", conflicts) + "\n" + T("仍可保存。", "Saving is available.", "保存できます。");
        SaveButton.IsEnabled = changes.Count > 0;
        ReloadButton.IsEnabled = ExportButton.IsEnabled = Session != null;
        Title = (changes.Count > 0 ? "● " : "") + T("皇牌空战8 · 键位工具", "ACE COMBAT 8 · Flight controls", "ACE COMBAT 8 · フライト操作設定");
    }
    private void SetStatus(string message, string detail = "") { StatusText.Text = message; StatusDetail.Text = detail; }
    private void TryAction(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or InvalidOperationException or ArgumentException)
        {
            SetStatus(T("操作未完成。", "Operation did not complete.", "操作を完了できませんでした。"), ex.Message);
            MessageBox.Show(this, ex.Message, T("操作未完成", "Operation did not complete", "操作を完了できませんでした"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
    private bool CanDiscard()
    {
        if (Session?.Changes.Count is not > 0) return true;
        return MessageBox.Show(this,
            T("要放弃尚未保存的更改吗？", "Discard unsaved changes?", "未保存の変更を破棄しますか？"),
            T("未保存的更改", "Unsaved changes", "未保存の変更"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }
    private void OnClosing(object? sender, CancelEventArgs e) { if (!testing && !CanDiscard()) e.Cancel = true; }
    private void ChooseClick(object sender, RoutedEventArgs e)
    {
        if (!CanDiscard()) return;
        var dialog = new OpenFileDialog { Title = T("选择 FlightStick.ini", "Open FlightStick.ini", "FlightStick.ini を開く"), Filter = "FlightStick.ini|FlightStick.ini|INI (*.ini)|*.ini", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) TryAction(() => LoadFile(dialog.FileName));
    }
    private async void LocateClick(object sender, RoutedEventArgs e)
    {
        if (!CanDiscard()) return;
        LocateButton.IsEnabled = false;
        SetStatus(T("正在查找游戏配置…", "Searching for game configuration…", "ゲーム設定を検索中…"));
        try
        {
            var roots = WindowsServices.SteamRoots().ToArray();
            var result = await Task.Run(() => ConfigDiscovery.Find(roots, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), WindowsServices.LastPath()));
            steamGame = result.Games.Count > 0 ? $"{result.Games[0].Name} · {result.Games[0].Library}" : null;
            UpdateSteamDisplay();
            if (result.Candidates.Count == 1) LoadFile(result.Candidates[0].Path);
            else if (result.Candidates.Count > 1)
            {
                string? selected = ChooseCandidate(result.Candidates);
                if (selected != null) LoadFile(selected);
                else SetStatus(T("已取消选择。", "Selection canceled.", "選択をキャンセルしました。"));
            }
            else SetStatus(T("没有找到可用配置。", "No editable configuration found.", "編集可能な設定が見つかりません。"), string.Join(" ", result.Notes));
        }
        catch (Exception ex) { SetStatus(T("自动查找未完成。", "Automatic search failed.", "自動検索に失敗しました。"), ex.Message); }
        finally { LocateButton.IsEnabled = true; }
    }
    private void ReloadClick(object sender, RoutedEventArgs e) { if (Session != null && CanDiscard()) TryAction(() => LoadFile(Session.Document.Path, testing, Session.Document.Device)); }
    private void SaveClick(object sender, RoutedEventArgs e) => TryAction(() => SaveCurrent());
    internal SaveResult SaveCurrent(bool? gameRunning = null)
    {
        if (Session == null) throw new InvalidOperationException(T("请先选择配置文件。", "Choose a configuration first.", "先に設定ファイルを選択してください。"));
        var result = ConfigStore.Save(Session, () => gameRunning ?? WindowsServices.GameRunning());
        Session = new(result.Document);
        RefreshAll();
        SetStatus(result.Changed
            ? T("键位已保存，重启游戏后生效。", "Bindings saved. Restart the game to apply them.", "割り当てを保存しました。ゲームを再起動してください。")
            : T("配置没有变化。", "No changes to save.", "保存する変更はありません。"),
            result.BackupPath == null ? "" : T("备份 · ", "Backup · ", "バックアップ · ") + System.IO.Path.GetFileName(result.BackupPath));
        return result;
    }
    private void ExportClick(object sender, RoutedEventArgs e)
    {
        if (Session == null) return;
        var dialog = new SaveFileDialog { Title = T("导出键位表", "Export bindings", "割り当てを出力"), FileName = "Ace Combat 8 bindings.md", Filter = "Markdown (*.md)|*.md" };
        if (dialog.ShowDialog(this) == true) TryAction(() =>
        {
            File.WriteAllText(dialog.FileName, Session.ToMarkdown(), new System.Text.UTF8Encoding(false));
            SetStatus(T("键位表已导出。", "Bindings exported.", "割り当てを出力しました。"), dialog.FileName);
        });
    }
}
