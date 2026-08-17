using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Mogiki.App.Audio;
using Mogiki.App.Config;
using Mogiki.Core.Bus;
using Mogiki.Core.Cartridge;

namespace Mogiki.App.Views;

public partial class MainWindow : Window
{
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint uMilliseconds);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint uMilliseconds);

    private readonly AppConfig _config = new();
    private readonly Bus _bus = new();
    private readonly AudioEngine _audio = new();

    private readonly WriteableBitmap _screenBitmap;
    private readonly Thread _emulationThread;
    private volatile bool _isRunning = true;
    private volatile bool _isPaused = false;
    private volatile bool _romLoaded = false;
    private volatile bool _fastForward = false;
    private volatile bool _renderPending = false;

    private GameWindow? _gameWindow;
    private bool _closingGameWindow;

    private readonly List<string> _libraryRoms = [];
    private byte _controllerState;
    private int _renderedFrames;
    private double _currentFps;
    private readonly Stopwatch _fpsStopwatch = Stopwatch.StartNew();
    private readonly object _renderLock = new();

    public MainWindow() : this(null) { }

    public MainWindow(string? initialRom)
    {
        if (OperatingSystem.IsWindows())
        {
            TimeBeginPeriod(1);
        }

        _config.Load("config.ini");

        _screenBitmap = new WriteableBitmap(
            new PixelSize(256, 240),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);

        InitializeComponent();

        GameScreen.Source = _screenBitmap;

        // Apply config
        ApplyAspectRatio();
        ApplyScale(_config.WindowScale);
        UpdateRecentMenu();
        if (Directory.Exists(_config.LibraryDirectory))
        {
            ScanLibraryDirectory(_config.LibraryDirectory, persist: false);
        }
        else
        {
            UpdateLibraryPanel();
        }

        // Drag & Drop
        AddHandler(DragDrop.DropEvent, OnDropHandler);
        AddHandler(DragDrop.DragOverEvent, OnDragOverHandler);

        // Audio initialization
        _audio.Init();

        // Background emulation thread
        _emulationThread = new Thread(EmulationLoop)
        {
            IsBackground = true,
            Name = "Mogiki Emulation Thread",
            Priority = ThreadPriority.Highest
        };
        _emulationThread.Start();

        Closed += OnWindowClosed;

        if (!string.IsNullOrEmpty(initialRom) && File.Exists(initialRom))
        {
            LoadRom(initialRom);
        }
        // Always open on the library screen. A previously played ROM should not
        // start until the user explicitly selects it from the launcher UI.
    }

    private void ApplyAspectRatio()
    {
        switch (_config.AspectRatio)
        {
            case AspectRatioMode.Native8_7:
                GameScreen.Width = 274;
                GameScreen.Height = 240;
                break;
            case AspectRatioMode.Standard4_3:
                GameScreen.Width = 320;
                GameScreen.Height = 240;
                break;
            case AspectRatioMode.PixelPerfect1_1:
                GameScreen.Width = 256;
                GameScreen.Height = 240;
                break;
        }

        _gameWindow?.SetLogicalSize(GameScreen.Width, GameScreen.Height);
    }

    private void ApplyScale(int scale)
    {
        _config.WindowScale = Math.Clamp(scale, 1, 6);
        double targetW = GameScreen.Width * _config.WindowScale;
        double targetH = GameScreen.Height * _config.WindowScale + 70; // Include menu and status bar

        Width = Math.Max(targetW, 512);
        Height = Math.Max(targetH, 480);

        _gameWindow?.ApplyScale(_config.WindowScale, GameScreen.Width, GameScreen.Height);
    }

    private void UpdateRecentMenu()
    {
        MenuRecentRoms.Items.Clear();

        var recentPaths = _config.RecentRoms
            .Where(File.Exists)
            .ToList();

        if (recentPaths.Count == 0)
        {
            MenuRecentRoms.Items.Add(new MenuItem { Header = "No Recent Files", IsEnabled = false });
            return;
        }

        foreach (var path in recentPaths)
        {
            string p = path;
            var item = new MenuItem { Header = Path.GetFileName(p) };
            item.Click += (_, _) => LoadRom(p);
            MenuRecentRoms.Items.Add(item);
        }
    }

    private void UpdateLibraryPanel()
    {
        LibraryGamesPanel.Children.Clear();

        bool hasDirectory = !string.IsNullOrWhiteSpace(_config.LibraryDirectory)
            && Directory.Exists(_config.LibraryDirectory);

        var paths = hasDirectory
            ? _libraryRoms
            : _config.RecentRoms.Where(File.Exists).Take(10).ToList();

        LibraryDirectoryText.Text = hasDirectory
            ? _config.LibraryDirectory
            : "No game folder selected";

        LibraryEmptyText.Text = hasDirectory
            ? "No .nes games found in this folder."
            : paths.Count == 0
                ? "Choose Add Games Folder to build your library."
                : "Recent games are shown here until you choose a folder.";

        LibraryEmptyText.IsVisible = paths.Count == 0;
        LibraryGamesPanel.IsVisible = paths.Count > 0;

        foreach (var path in paths)
        {
            var gameButton = new Button
            {
                Content = Path.GetFileNameWithoutExtension(path),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                Padding = new Thickness(10, 8),
                FontSize = 11
            };
            gameButton.Click += (_, _) => LoadRom(path);
            LibraryGamesPanel.Children.Add(gameButton);
        }
    }

    private void ScanLibraryDirectory(string directory, bool persist)
    {
        _libraryRoms.Clear();

        if (!Directory.Exists(directory))
        {
            _config.LibraryDirectory = "";
            UpdateLibraryPanel();
            return;
        }

        try
        {
            _libraryRoms.AddRange(
                Directory.EnumerateFiles(directory, "*.nes", SearchOption.AllDirectories)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase));

            _config.LibraryDirectory = directory;
            if (persist)
            {
                _config.Save("config.ini");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error scanning game directory: {ex.Message}");
        }

        UpdateLibraryPanel();
    }

    public bool LoadRom(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return false;

        try
        {
            lock (_renderLock)
            {
                var cart = new Cartridge(path);
                if (cart.ImageValid)
                {
                    _bus.InsertCartridge(cart);
                    _bus.Reset();
                    _audio.Reset();
                    _romLoaded = true;
                    _isPaused = false;
                    _config.LastRomPath = path;
                    _config.AddRecentRom(path);

                    Dispatcher.UIThread.Post(() =>
                    {
                        UpdateLibraryPanel();
                        WelcomeOverlay.IsVisible = false;
                        GameViewbox.IsVisible = false;
                        DetachedGameOverlay.IsVisible = true;
                        UpdateRecentMenu();
                        string name = Path.GetFileName(path);
                        Title = $"Mogiki NES - {name} (Mapper {cart.MapperId})";
                        ShowGameWindow(name);
                        TxtStatus.Text = "● RUNNING";
                        TxtStatus.Foreground = new SolidColorBrush(Color.Parse("#4ADE80"));
                        TxtRomInfo.Text = $"{name} • Mapper {cart.MapperId} • PRG: {cart.PrgBanks * 16}KB • CHR: {cart.ChrBanks * 8}KB • {cart.Mirror}";
                    });

                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading ROM: {ex.Message}");
        }

        return false;
    }

    private async void OnOpenRomClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select NES ROM",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("NES ROM Files (*.nes)") { Patterns = ["*.nes"] },
                new FilePickerFileType("All Files (*.*)") { Patterns = ["*.*"] }
            ]
        });

        if (files.Count > 0)
        {
            string? path = files[0].TryGetLocalPath();
            if (!string.IsNullOrEmpty(path))
            {
                LoadRom(path);
            }
        }
    }

    private async void OnChooseLibraryClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select NES game directory",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            string? path = folders[0].TryGetLocalPath();
            if (!string.IsNullOrEmpty(path))
            {
                ScanLibraryDirectory(path, persist: true);
            }
        }
    }

    private void OnTogglePauseClick(object? sender, RoutedEventArgs e)
    {
        if (!_romLoaded) return;
        _isPaused = !_isPaused;
        _audio.Pause(_isPaused || !_config.SoundEnabled);

        Dispatcher.UIThread.Post(() =>
        {
            TxtStatus.Text = _isPaused ? "❚❚ PAUSED" : "● RUNNING";
            TxtStatus.Foreground = _isPaused
                ? new SolidColorBrush(Color.Parse("#FACC15"))
                : new SolidColorBrush(Color.Parse("#4ADE80"));
        });
    }

    private void OnStopClick(object? sender, RoutedEventArgs e) => StopAndReturnToLibrary();

    private void StopAndReturnToLibrary()
    {
        if (!_romLoaded) return;

        // The emulation loop remains alive for the next game, but no longer
        // clocks the bus while the launcher/library is visible.
        _isPaused = true;
        _romLoaded = false;
        _controllerState = 0;
        _audio.Pause(true);
        CloseGameWindow();

        Dispatcher.UIThread.Post(() =>
        {
            UpdateLibraryPanel();
            GameViewbox.IsVisible = false;
            DetachedGameOverlay.IsVisible = false;
            WelcomeOverlay.IsVisible = true;
            Title = "Mogiki NES Emulator";
            TxtStatus.Text = "NO ROM LOADED";
            TxtStatus.Foreground = new SolidColorBrush(Color.Parse("#98A0AA"));
            TxtFps.Text = "0.0 FPS";
            TxtRomInfo.Text = "Select a game from the library";
        });
    }

    private void ShowGameWindow(string gameName)
    {
        if (_gameWindow == null)
        {
            var window = new GameWindow(_screenBitmap, GameScreen.Width, GameScreen.Height);
            window.GameKeyDown += OnGameWindowKeyDown;
            window.GameKeyUp += OnGameWindowKeyUp;
            window.Closed += OnGameWindowClosed;
            _gameWindow = window;
        }

        _gameWindow.SetLogicalSize(GameScreen.Width, GameScreen.Height);
        _gameWindow.ApplyScale(_config.WindowScale, GameScreen.Width, GameScreen.Height);
        _gameWindow.SetBilinearFilter(_config.BilinearFilter);
        _gameWindow.Title = $"Mogiki - {gameName}";

        if (!_gameWindow.IsVisible)
        {
            _gameWindow.Show();
        }

        _gameWindow.Activate();
    }

    private void CloseGameWindow()
    {
        var window = _gameWindow;
        _gameWindow = null;
        if (window == null)
            return;

        _closingGameWindow = true;
        window.Close();
        _closingGameWindow = false;
    }

    private void OnGameWindowClosed(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _gameWindow))
        {
            _gameWindow = null;
        }

        if (!_closingGameWindow && _romLoaded)
        {
            StopAndReturnToLibrary();
        }
    }

    private void OnToggleFullscreenClick(object? sender, RoutedEventArgs e)
    {
        _gameWindow?.ToggleFullscreen();
    }

    private void OnGameWindowKeyDown(object? sender, KeyEventArgs e)
    {
        OnKeyDownHandler(sender, e);
    }

    private void OnGameWindowKeyUp(object? sender, KeyEventArgs e)
    {
        OnKeyUpHandler(sender, e);
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        if (!_romLoaded) return;
        lock (_renderLock)
        {
            _bus.Reset();
            _audio.Reset();
        }
    }

    private void OnScale1xClick(object? sender, RoutedEventArgs e) => ApplyScale(1);
    private void OnScale2xClick(object? sender, RoutedEventArgs e) => ApplyScale(2);
    private void OnScale3xClick(object? sender, RoutedEventArgs e) => ApplyScale(3);
    private void OnScale4xClick(object? sender, RoutedEventArgs e) => ApplyScale(4);

    private void OnAspect4_3Click(object? sender, RoutedEventArgs e)
    {
        _config.AspectRatio = AspectRatioMode.Standard4_3;
        ApplyAspectRatio();
    }

    private void OnAspect8_7Click(object? sender, RoutedEventArgs e)
    {
        _config.AspectRatio = AspectRatioMode.Native8_7;
        ApplyAspectRatio();
    }

    private void OnAspect1_1Click(object? sender, RoutedEventArgs e)
    {
        _config.AspectRatio = AspectRatioMode.PixelPerfect1_1;
        ApplyAspectRatio();
    }

    private void OnToggleBilinearClick(object? sender, RoutedEventArgs e)
    {
        _config.BilinearFilter = !_config.BilinearFilter;
        RenderOptions.SetBitmapInterpolationMode(GameScreen,
            _config.BilinearFilter ? BitmapInterpolationMode.LowQuality : BitmapInterpolationMode.None);
        _gameWindow?.SetBilinearFilter(_config.BilinearFilter);
    }

    private void OnToggleSoundClick(object? sender, RoutedEventArgs e)
    {
        _config.SoundEnabled = !_config.SoundEnabled;
        _audio.Pause(!_config.SoundEnabled || _isPaused);
    }

    private async void OnControllerConfigClick(object? sender, RoutedEventArgs e)
    {
        bool wasPaused = _isPaused;
        _isPaused = true;
        var dlg = new ControllerConfigWindow(_config.Keys);
        await dlg.ShowDialog(this);
        if (dlg.IsSaved)
        {
            _config.Save("config.ini");
        }
        _isPaused = wasPaused;
    }

    private void OnPatternTableClick(object? sender, RoutedEventArgs e)
    {
        new PatternTableWindow(_bus).Show(this);
    }

    private async void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        await new AboutWindow().ShowDialog(this);
    }

    private void OnTakeScreenshotClick(object? sender, RoutedEventArgs e)
    {
        if (!_romLoaded) return;
        try
        {
            string fileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            lock (_renderLock)
            {
                _screenBitmap.Save(fileName);
            }
            Dispatcher.UIThread.Post(() => TxtRomInfo.Text = $"Saved screenshot: {fileName}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to save screenshot: {ex.Message}");
        }
    }

    private void OnExitClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnDragOverHandler(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
            e.DragEffects = DragDropEffects.Copy;
        else
            e.DragEffects = DragDropEffects.None;
    }

    private void OnDropHandler(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles();
            if (files != null)
            {
                foreach (var file in files)
                {
                    string? path = file.TryGetLocalPath();
                    if (!string.IsNullOrEmpty(path) && path.EndsWith(".nes", StringComparison.OrdinalIgnoreCase))
                    {
                        LoadRom(path);
                        break;
                    }
                }
            }
        }
    }

    private void OnKeyDownHandler(object? sender, KeyEventArgs e)
    {
        var k = e.Key;
        if (k == Key.F11)
        {
            OnToggleFullscreenClick(null, null!);
            e.Handled = true;
            return;
        }
        if (k == Key.F1 || (k == Key.O && e.KeyModifiers.HasFlag(KeyModifiers.Control)))
        {
            OnOpenRomClick(null, null!);
            e.Handled = true;
            return;
        }
        if (k == Key.Tab)
        {
            _fastForward = !_fastForward;
            e.Handled = true;
            return;
        }
        if (k == Key.Space || k == Key.P)
        {
            OnTogglePauseClick(null, null!);
            e.Handled = true;
            return;
        }
        if (k == Key.Escape)
        {
            OnStopClick(null, null!);
            e.Handled = true;
            return;
        }
        if (k == Key.R && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            OnResetClick(null, null!);
            e.Handled = true;
            return;
        }

        if (k == _config.Keys.A) _controllerState |= 0x80;
        else if (k == _config.Keys.B) _controllerState |= 0x40;
        else if (k == _config.Keys.Select) _controllerState |= 0x20;
        else if (k == _config.Keys.Start) _controllerState |= 0x10;
        else if (k == _config.Keys.Up) _controllerState |= 0x08;
        else if (k == _config.Keys.Down) _controllerState |= 0x04;
        else if (k == _config.Keys.Left) _controllerState |= 0x02;
        else if (k == _config.Keys.Right) _controllerState |= 0x01;
    }

    private void OnKeyUpHandler(object? sender, KeyEventArgs e)
    {
        var k = e.Key;
        if (k == _config.Keys.A) _controllerState = (byte)(_controllerState & ~0x80);
        else if (k == _config.Keys.B) _controllerState = (byte)(_controllerState & ~0x40);
        else if (k == _config.Keys.Select) _controllerState = (byte)(_controllerState & ~0x20);
        else if (k == _config.Keys.Start) _controllerState = (byte)(_controllerState & ~0x10);
        else if (k == _config.Keys.Up) _controllerState = (byte)(_controllerState & ~0x08);
        else if (k == _config.Keys.Down) _controllerState = (byte)(_controllerState & ~0x04);
        else if (k == _config.Keys.Left) _controllerState = (byte)(_controllerState & ~0x02);
        else if (k == _config.Keys.Right) _controllerState = (byte)(_controllerState & ~0x01);
    }

    private unsafe void EmulationLoop()
    {
        const double targetFrameTimeMs = 1000.0 / 60.0988; // NTSC 60.0988 Hz
        var frameStopwatch = Stopwatch.StartNew();

        const double cpuFreq = 1789773.0;
        double sampleFreq = _audio.SampleRate > 0 ? _audio.SampleRate : 44100;
        double cyclesPerSample = cpuFreq / sampleFreq;
        double audioSampleCounter = 0.0;
        double lastSample = 0.0;
        const double filterAlpha = 0.4;

        while (_isRunning)
        {
            if (_romLoaded && !_isPaused)
            {
                _bus.Controller[0] = _controllerState;

                lock (_renderLock)
                {
                    do
                    {
                        _bus.Clock();

                        audioSampleCounter += 1.0 / 3.0;
                        if (audioSampleCounter >= cyclesPerSample)
                        {
                            audioSampleCounter -= cyclesPerSample;
                            if (_config.SoundEnabled)
                            {
                                double rawSample = _bus.GetAudioSample();
                                double filtered = lastSample + filterAlpha * (rawSample - lastSample);
                                lastSample = filtered;

                                float vol = (_config.Volume / 100.0f) * 0.5f;
                                _audio.WriteSample((float)(filtered * vol));
                            }
                        }
                    } while (!_bus.Ppu.FrameComplete);

                    _bus.Ppu.FrameComplete = false;

                    // Copy Screen buffer directly to Avalonia WriteableBitmap memory
                    using (var locked = _screenBitmap.Lock())
                    {
                        fixed (uint* src = _bus.Ppu.ScreenArgb)
                        {
                            Buffer.MemoryCopy(src, (void*)locked.Address, 256 * 240 * sizeof(uint), 256 * 240 * sizeof(uint));
                        }
                    }
                }

                _renderedFrames++;
                if (_fpsStopwatch.ElapsedMilliseconds >= 1000)
                {
                    _currentFps = _renderedFrames * 1000.0 / _fpsStopwatch.ElapsedMilliseconds;
                    _renderedFrames = 0;
                    _fpsStopwatch.Restart();
                    Dispatcher.UIThread.Post(() => TxtFps.Text = $"{_currentFps:F1} FPS");
                }

                // Invalidate visual on UI thread
                if (!_renderPending)
                {
                    _renderPending = true;
                    Dispatcher.UIThread.Post(() =>
                    {
                        GameScreen.InvalidateVisual();
                        _gameWindow?.InvalidateGameScreen();
                        _renderPending = false;
                    }, DispatcherPriority.Render);
                }

                // High precision 1ms frame pacing
                if (!_fastForward)
                {
                    double elapsed = frameStopwatch.Elapsed.TotalMilliseconds;
                    double sleepTime = targetFrameTimeMs - elapsed;

                    if (sleepTime > 1.5)
                    {
                        Thread.Sleep((int)(sleepTime - 1.0));
                    }
                    while (frameStopwatch.Elapsed.TotalMilliseconds < targetFrameTimeMs)
                    {
                        Thread.SpinWait(10);
                    }
                    frameStopwatch.Restart();
                }
                else
                {
                    frameStopwatch.Restart();
                }
            }
            else
            {
                Thread.Sleep(16);
            }
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        CloseGameWindow();
        _isRunning = false;
        _emulationThread.Join(500);
        _audio.Dispose();
        _screenBitmap.Dispose();
        _config.Save("config.ini");

        if (OperatingSystem.IsWindows())
        {
            TimeEndPeriod(1);
        }
    }
}
