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
        window.Editors["Gun"].SelectedItem = PxnDevice.ButtonChoices.First(c => c.Input == new FlightInput("Button", 2));
        window.Editors["Missile"].SelectedItem = PxnDevice.ButtonChoices.First(c => c.Input == new FlightInput("Button", 1));
        Capture("02-review-save.png");
        window.SaveCurrent(false);
        window.Session!.ApplyCombatPreset();
        window.SaveCurrent(false);
        window.SelectPage(1); Capture("03-menu-story-skip.png");
        window.SelectPage(2); Capture("04-flight-axes.png");
        window.SelectPage(3); Capture("05-hats-camera.png");
        window.SelectPage(4); Capture("06-backup-restore.png");
        File.WriteAllText(Path.Combine(output, "screenshots.json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            mode = "application-rendered example configuration",
            liveConfigurationModified = false, privatePathsIncluded = false, screenshots = 6
        }));
    }
}
