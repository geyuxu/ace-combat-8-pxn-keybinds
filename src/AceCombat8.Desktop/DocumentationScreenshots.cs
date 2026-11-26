using System.IO;
using System.Windows;
using System.Windows.Threading;
using AceCombat8.Core;

namespace AceCombat8.Desktop;

internal static class DocumentationScreenshots
{
    public static void Run(string source, string output)
    {
        Directory.CreateDirectory(output);
        string work = Path.Combine(Path.GetTempPath(), "ac8-documentation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        string fixture = Path.Combine(work, "FlightStick.ini");
        File.Copy(source, fixture);
        var window = new MainWindow
        {
            ShowActivated = false, ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -30000, Top = -30000
        };
        window.Show();
        window.LoadFile(fixture, true);
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        void Capture(string name) => UiSmoke.Capture(window, output, name, documentation: true);
        Capture("01-overview.png");
        window.VisualHotspots[new FlightInput("Button", 7)].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        window.AddActionPicker!.SelectedValue = "Target";
        window.AddMappingButton!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Capture("02-review-save.png");
        window.SaveCurrent(false);
        var session = window.Session!;
        session.RemoveInput("Target", session.Current["Target"].Inputs.ToList().FindIndex(i => i.Button == 7));
        window.SaveCurrent(false);
        window.SelectPage(1); Capture("03-menu-story-skip.png");
        window.SelectPage(2); Capture("04-flight-axes.png");
        window.SelectPage(3); Capture("05-hats-camera.png");
        window.SelectPage(4); Capture("06-backup-restore.png");
        window.LanguagePicker.SelectedIndex = 0;
        window.SelectPage(0); Capture("07-overview-en.png");
        window.LanguagePicker.SelectedIndex = 2;
        window.SelectPage(0); Capture("08-overview-ja.png");
        File.WriteAllText(Path.Combine(output, "screenshots.json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            mode = "application-rendered example configuration",
            liveConfigurationModified = false, privatePathsIncluded = false, screenshots = 8
        }));
    }
}
