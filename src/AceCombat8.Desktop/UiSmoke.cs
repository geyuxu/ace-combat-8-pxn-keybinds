using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AceCombat8.Core;

namespace AceCombat8.Desktop;

internal static class UiSmoke
{
    public static void Run(string source, string output)
    {
        Directory.CreateDirectory(output);
        string fixture = Path.Combine(output, "FlightStick.ini");
        File.Copy(source, fixture, true);
        byte[] original = File.ReadAllBytes(fixture);
        var window = new MainWindow();
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -30000; window.Top = -30000;
        window.Show();
        window.LoadFile(fixture, true);
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        Check(window.Session!.Changes.Count == 0, "Opening must not change bindings");
        Check(!window.SaveButton.IsEnabled, "Save must be disabled for an unchanged file");
        var picker = window.Editors["Gun"];
        picker.ApplyTemplate();
        var toggle = FindChild<ToggleButton>(picker) ?? throw new InvalidOperationException("Missing dropdown toggle");
        toggle.IsChecked = true;
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        Check(picker.IsDropDownOpen, "Dropdown toggle must open the picker");
        picker.IsDropDownOpen = false;
        Capture(window, output, "01-combat.png");
        window.Editors["Gun"].SelectedItem = PxnDevice.ButtonChoices.First(c => c.Input == new FlightInput("Button", 2));
        window.Editors["Missile"].SelectedItem = PxnDevice.ButtonChoices.First(c => c.Input == new FlightInput("Button", 1));
        Check(window.Session.Changes.Count == 2, "Combat controls must update the edit session");
        Capture(window, output, "02-changes.png");
        var saved = window.SaveCurrent(false);
        Check(saved.Changed && File.ReadAllBytes(saved.BackupPath!).SequenceEqual(original), "UI save must create an exact backup");
        Check(window.Session.Changes.Count == 0, "A successful save must clear pending changes");
        window.SelectPage(1);
        window.Editors["FaceButtonBottom"].SelectedItem = PxnDevice.ButtonChoices.First(c => c.Input == new FlightInput("Button", 3));
        Check(window.Session.Changes.Single().Key == "FaceButtonBottom", "Menu editing must be independent from combat bindings");
        Capture(window, output, "03-menu.png");
        window.LoadFile(fixture, true);
        window.SelectPage(2);
        window.AxisReverseForTest!.IsChecked = false;
        Check(window.Session.Changes.Single().Key == "Pitch", "Axis reverse control must edit pitch only");
        Capture(window, output, "04-axes.png");
        window.LoadFile(fixture, true);
        window.SelectPage(3);
        window.HatEditor!.SelectedIndex = 1;
        Check(window.Session.HatLayout == "left-orders", "Hat swap must produce the native POV2 mapping");
        Capture(window, output, "05-hats.png");
        window.LoadFile(fixture, true);
        window.SelectPage(4);
        Capture(window, output, "06-backups.png");
        window.SelectPage(0);
        Capture(window, output, "07-compact.png", 1080, 720);
        File.WriteAllText(Path.Combine(output, "ui-results.txt"), "PASS: startup, real dropdown toggle, combat edit, exact backup, save/reload, independent menu binding, axis reversal, native POV2 swap, five pages, compact layout.\nNo live game configuration was modified.");
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static T? FindChild<T>(DependencyObject value) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(value); i++)
        {
            var child = VisualTreeHelper.GetChild(value, i);
            if (child is T match) return match;
            if (FindChild<T>(child) is { } nested) return nested;
        }
        return null;
    }
    internal static void Capture(MainWindow window, string output, string name, int width = 1280, int height = 900, bool documentation = false)
    {
        string path = window.PathDisplay.Text, heading = window.FileHeading.Text, steam = window.SteamDisplay.Text;
        if (documentation)
        {
            window.PathDisplay.Text = @"%LOCALAPPDATA%\BANDAI NAMCO Entertainment\ACE COMBAT 8\Saved\Config\Windows\FlightStick.ini";
            window.FileHeading.Text = "示例配置 · PXN 2119 Pro / PXN-F19";
            window.SteamDisplay.Text = @"Steam · ACE COMBAT 8: WINGS OF THEVE · D:\SteamLibrary";
        }
        var root = window.RootView;
        window.Content = null;
        var surface = new Border { Background = new SolidColorBrush(Color.FromRgb(11, 20, 34)), Child = root, Width = width, Height = height };
        TextElementSet(surface, window);
        surface.Measure(new Size(width, height));
        surface.Arrange(new Rect(0, 0, width, height));
        surface.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(surface);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(output, name))) png.Save(stream);
        surface.Child = null;
        window.Content = root;
        window.PathDisplay.Text = path; window.FileHeading.Text = heading; window.SteamDisplay.Text = steam;
    }
    private static void TextElementSet(Border surface, MainWindow window)
    {
        System.Windows.Documents.TextElement.SetFontFamily(surface, window.FontFamily);
        System.Windows.Documents.TextElement.SetFontSize(surface, window.FontSize);
        System.Windows.Documents.TextElement.SetForeground(surface, window.Foreground);
    }
}
