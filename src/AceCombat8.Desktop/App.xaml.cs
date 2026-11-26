using System.IO;
using System.Windows;
using AceCombat8.Core;

namespace AceCombat8.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, Lang.T("操作未完成", "Operation did not complete", "操作を完了できませんでした"), MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        if (e.Args.Length >= 3 && e.Args[0] is "--smoke-test" or "--render-docs")
        {
            try
            {
                Directory.CreateDirectory(e.Args[2]);
                if (e.Args[0] == "--render-docs") DocumentationScreenshots.Run(e.Args[1], e.Args[2]);
                else UiSmoke.Run(e.Args[1], e.Args[2]);
                Shutdown(0);
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(e.Args[2], "ui-error.txt"), ex.ToString()); Shutdown(1); }
            return;
        }
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
        window.InitializeFromArguments(e.Args);
    }
}
