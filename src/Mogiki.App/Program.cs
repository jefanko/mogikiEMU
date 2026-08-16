using Avalonia;

namespace Mogiki.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.InitialRomPath = args.Length > 0 ? args[0] : null;

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
