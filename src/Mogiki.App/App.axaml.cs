using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Mogiki.App.Views;

namespace Mogiki.App;

public partial class App : Application
{
    public static string? InitialRomPath { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(InitialRomPath);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
