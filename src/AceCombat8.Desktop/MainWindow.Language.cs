using System.Globalization;
using System.IO;
using System.Windows.Controls;
using AceCombat8.Core;
using static AceCombat8.Core.Lang;

namespace AceCombat8.Desktop;

public partial class MainWindow
{
    private sealed record LanguageChoice(UiLanguage Language, string Label);

    private static string LanguageFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AceCombat8Keybinds", "language.txt");

    private void InitializeLanguage()
    {
        bool headless = Environment.GetCommandLineArgs().Any(arg => arg is "--smoke-test" or "--render-docs");
        Current = headless ? UiLanguage.Chinese : ReadLanguage();
        LanguageChoice[] options = [new(UiLanguage.English, "English"), new(UiLanguage.Chinese, "简体中文"), new(UiLanguage.Japanese, "日本語")];
        LanguagePicker.ItemsSource = options;
        LanguagePicker.DisplayMemberPath = "Label";
        LanguagePicker.SelectedItem = options.Single(choice => choice.Language == Current);
        System.Windows.Automation.AutomationProperties.SetName(LanguagePicker, "Language / 语言 / 言語");
        LanguagePicker.SelectionChanged += (_, _) =>
        {
            if (LanguagePicker.SelectedItem is not LanguageChoice choice) return;
            Current = choice.Language;
            if (!headless) StoreLanguage(choice.Language);
            TranslateChrome();
            if (Session != null) UpdateDevicePicker();
            RefreshAll();
            if (Session != null) SetLoadedStatus();
        };
        TranslateChrome();
    }

    private static UiLanguage ReadLanguage()
    {
        try
        {
            if (File.Exists(LanguageFile) && Enum.TryParse(File.ReadAllText(LanguageFile), out UiLanguage value)
                && Enum.IsDefined(value)) return value;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "zh" => UiLanguage.Chinese, "ja" => UiLanguage.Japanese, _ => UiLanguage.English
        };
    }

    private static void StoreLanguage(UiLanguage language)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LanguageFile)!);
            File.WriteAllText(LanguageFile, language.ToString());
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void TranslateChrome()
    {
        AppHeading.Text = T("飞行控制配置", "Flight controls", "フライト操作設定");
        LocateButton.Content = T("自动查找", "Find config", "自動検索");
        ChooseButton.Content = T("选择文件…", "Open file…", "ファイルを開く…");
        ReloadButton.Content = T("重新读取", "Reload", "再読み込み");
        ExportButton.Content = T("导出键位表", "Export bindings", "割り当てを出力");
        SaveButton.Content = T("保存到游戏", "Save to game", "ゲーム設定に保存");
        string[] pages =
        [
            T("实体按键", "Physical controls", "物理コントロール"),
            T("菜单导航", "Menu controls", "メニュー操作"),
            T("飞行轴", "Flight axes", "フライト軸"),
            T("帽键与视角", "Hats & camera", "ハット・視点"),
            T("备份与恢复", "Backups", "バックアップ"),
            T("作战按键", "Combat actions", "戦闘操作")
        ];
        foreach (var button in Navigation.Children.OfType<Button>())
            button.Content = pages[int.Parse((string)button.Tag)];
        if (Session == null)
        {
            FileHeading.Text = T("选择游戏配置", "Choose game configuration", "ゲーム設定を選択");
            PathDisplay.Text = T("自动查找，或选择 FlightStick.ini", "Find or open FlightStick.ini", "FlightStick.ini を検索または選択");
            SetStatus(T("打开配置后即可编辑。", "Open a configuration to start editing.", "設定を開くと編集できます。"));
        }
        UpdateSteamDisplay();
    }

    private void UpdateSteamDisplay() => SteamDisplay.Text = steamGame == null
        ? T("可从 Steam 安装路径或当前 Windows 用户配置自动查找。", "Search Steam libraries and the current Windows user profile.", "Steam ライブラリと現在の Windows ユーザー設定を検索します。")
        : "Steam · " + steamGame;

    private void SetLoadedStatus() => SetStatus(
        T("已读取设备配置。", "Controller profile loaded.", "コントローラー設定を読み込みました。"),
        T("保存时会备份。重新启动游戏后生效。", "Saving creates a backup. Restart the game to apply changes.", "保存時にバックアップします。ゲームの再起動後に反映されます。"));
}
