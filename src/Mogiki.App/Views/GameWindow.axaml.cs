using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Media;

namespace Mogiki.App.Views;

public partial class GameWindow : Window
{
    private WindowState _windowedState = WindowState.Normal;

    public event EventHandler<KeyEventArgs>? GameKeyDown;
    public event EventHandler<KeyEventArgs>? GameKeyUp;

    public GameWindow()
    {
        InitializeComponent();
        SetLogicalSize(320, 240);
    }

    public GameWindow(WriteableBitmap screenBitmap, double gameWidth, double gameHeight)
    {
        InitializeComponent();
        GameScreen.Source = screenBitmap;
        SetLogicalSize(gameWidth, gameHeight);
    }

    public void SetLogicalSize(double width, double height)
    {
        GameScreen.Width = width;
        GameScreen.Height = height;
    }

    public void ApplyScale(int scale, double gameWidth, double gameHeight)
    {
        if (WindowState == WindowState.FullScreen)
            return;

        Width = Math.Max(gameWidth * scale + 24, 640);
        Height = Math.Max(gameHeight * scale + 48, 480);
    }

    public void SetBilinearFilter(bool enabled)
    {
        RenderOptions.SetBitmapInterpolationMode(
            GameScreen,
            enabled ? BitmapInterpolationMode.LowQuality : BitmapInterpolationMode.None);
    }

    public void InvalidateGameScreen()
    {
        GameScreen.InvalidateVisual();
    }

    public void ToggleFullscreen()
    {
        if (WindowState == WindowState.FullScreen)
        {
            WindowState = _windowedState == WindowState.FullScreen
                ? WindowState.Normal
                : _windowedState;
        }
        else
        {
            _windowedState = WindowState;
            WindowState = WindowState.FullScreen;
        }
    }

    private void OnKeyDownHandler(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
            return;
        }

        GameKeyDown?.Invoke(this, e);
    }

    private void OnKeyUpHandler(object? sender, KeyEventArgs e)
    {
        GameKeyUp?.Invoke(this, e);
    }
}
