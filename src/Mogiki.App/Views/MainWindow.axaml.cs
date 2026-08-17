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
using Mogiki.App.Config;
using Mogiki.App.Emulation;
using Mogiki.App.Video;
using Mogiki.Core.Video;

namespace Mogiki.App.Views;

public partial class MainWindow : Window
{
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint uMilliseconds);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint uMilliseconds);

    private readonly AppConfig _config = new();
    private readonly EmulatorSession _session;
    private readonly FrameBufferPipeline _framePipeline;
    private readonly EmulationRunner _emulation;

    private readonly WriteableBitmap _screenBitmap;
    private int _renderPending;

    private GameWindow? _gameWindow;
    private Sdl3GpuRenderer? _sdlRenderer;
    private bool _closingGameWindow;

    private readonly List<string> _libraryRoms = [];
    private byte _controllerState;

    public MainWindow() : this(null) { }

    public MainWindow(string? initialRom)
    {
        if (OperatingSystem.IsWindows())
        {
            TimeBeginPeriod(1);
        }

        _config.Load("config.ini");
        _session = new EmulatorSession();
        _framePipeline = new FrameBufferPipeline();
        _emulation = new EmulationRunner(_session, _framePipeline);
        _emulation.SoundEnabled = _config.SoundEnabled;
        _emulation.Volume = _config.Volume;
        _emulation.FrameReady += OnFrameReady;
        _emulation.FpsUpdated += OnFpsUpdated;
        _emulation.Faulted += OnEmulationFaulted;

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
        UpdateMenuState();
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

        // Start the runtime after the launcher is ready. It remains idle until
        // a game is selected from the library.
        _emulation.Start();

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
        _sdlRenderer?.SetLogicalSize((int)GameScreen.Width, (int)GameScreen.Height);
    }

    private void ApplyScale(int scale)
    {
        _config.WindowScale = Math.Clamp(scale, 1, 6);
        double targetW = GameScreen.Width * _config.WindowScale;
        double targetH = GameScreen.Height * _config.WindowScale + 70; // Include menu and status bar

        Width = Math.Max(targetW, 512);
        Height = Math.Max(targetH, 480);

        _gameWindow?.ApplyScale(_config.WindowScale, GameScreen.Width, GameScreen.Height);
        _sdlRenderer?.ApplyScale(_config.WindowScale);
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

    private void UpdateMenuState()
    {
        MenuBilinearFilter.Header = _config.BilinearFilter
            ? "Smooth Bilinear Filter [On]"
            : "Smooth Bilinear Filter [Off]";
        MenuSoundEnabled.Header = _config.SoundEnabled
            ? "Enable Sound [On]"
            : "Enable Sound [Off]";
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
            if (!_emulation.LoadRom(path))
                return false;

            var cart = _session.Cartridge;
            if (cart == null)
                return false;

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
                string renderer = _sdlRenderer?.IsAvailable == true
                    ? $"SDL3 GPU ({_sdlRenderer.BackendName})"
                    : "Avalonia bitmap";
                TxtRomInfo.Text = $"{name} • Mapper {cart.MapperId} • PRG: {cart.PrgBanks * 16}KB • CHR: {cart.ChrBanks * 8}KB • {cart.Mirror} • {renderer}";
            });

            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading ROM: {ex.Message}");
        }

        return false;
    }

    private void UpdateLoadedRomInfo()
    {
        var cart = _session.Cartridge;
        if (cart == null || string.IsNullOrWhiteSpace(_session.RomPath))
            return;

        string name = Path.GetFileName(_session.RomPath);
        string renderer = _sdlRenderer?.IsAvailable == true
            ? $"SDL3 GPU ({_sdlRenderer.BackendName})"
            : "Avalonia bitmap";
        TxtRomInfo.Text =
            $"{name} | Mapper {cart.MapperId} | PRG: {cart.PrgBanks * 16}KB | " +
            $"CHR: {cart.ChrBanks * 8}KB | {cart.Mirror} | {renderer}";
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
        if (!_emulation.IsRomLoaded) return;
        _emulation.IsPaused = !_emulation.IsPaused;

        Dispatcher.UIThread.Post(() =>
        {
            TxtStatus.Text = _emulation.IsPaused ? "❚❚ PAUSED" : "● RUNNING";
            TxtStatus.Foreground = _emulation.IsPaused
                ? new SolidColorBrush(Color.Parse("#FACC15"))
                : new SolidColorBrush(Color.Parse("#4ADE80"));
        });
    }

    private void OnStopClick(object? sender, RoutedEventArgs e) => StopAndReturnToLibrary();

    private void StopAndReturnToLibrary()
    {
        if (!_emulation.IsRomLoaded) return;

        _emulation.StopGame();
        _controllerState = 0;
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
        if (!string.Equals(_config.RendererBackend, "avalonia", StringComparison.OrdinalIgnoreCase))
        {
            _sdlRenderer ??= CreateSdlRenderer();
            bool rendererWasAvailable = _sdlRenderer.IsAvailable;
            if (_sdlRenderer.TryStart(
                    $"Mogiki - {gameName}",
                    (int)GameScreen.Width,
                    (int)GameScreen.Height,
                    _config.WindowScale,
                    _config.BilinearFilter))
            {
                _sdlRenderer.SetBilinearFilter(_config.BilinearFilter);
                if (_config.StartFullscreen && !rendererWasAvailable)
                    _sdlRenderer.ToggleFullscreen();
                return;
            }
        }

        bool gameWindowWasVisible = _gameWindow?.IsVisible == true;
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
        if (_config.StartFullscreen && !gameWindowWasVisible)
            _gameWindow.ToggleFullscreen();
    }

    private void CloseGameWindow()
    {
        var sdlRenderer = _sdlRenderer;
        _sdlRenderer = null;

        if (sdlRenderer != null)
        {
            _closingGameWindow = true;
            sdlRenderer.Dispose();
            _closingGameWindow = false;
        }

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

        if (!_closingGameWindow && _emulation.IsRomLoaded)
        {
            StopAndReturnToLibrary();
        }
    }

    private void OnToggleFullscreenClick(object? sender, RoutedEventArgs e)
    {
        if (_sdlRenderer?.IsAvailable == true)
            _sdlRenderer.ToggleFullscreen();
        else
            _gameWindow?.ToggleFullscreen();
    }

    private Sdl3GpuRenderer CreateSdlRenderer()
    {
        var renderer = new Sdl3GpuRenderer(_config.RendererBackend);
        renderer.Closed += OnSdlRendererClosed;
        renderer.KeyChanged += OnSdlKeyChanged;
        return renderer;
    }

    private void OnSdlRendererClosed()
    {
        if (!_closingGameWindow && _emulation.IsRomLoaded)
            StopAndReturnToLibrary();
    }

    private void OnSdlKeyChanged(uint keyCode, bool isDown)
    {
        if (!TryMapSdlKey(keyCode, out Key key))
            return;

        if (!isDown)
        {
            ApplyControllerKey(key, false);
            return;
        }

        if (key == Key.F11)
        {
            OnToggleFullscreenClick(null, null!);
            return;
        }

        if (key == Key.Escape)
        {
            OnStopClick(null, null!);
            return;
        }

        if (key == Key.Tab)
        {
            _emulation.FastForward = !_emulation.FastForward;
            return;
        }

        if (key == Key.Space || key == Key.P)
        {
            OnTogglePauseClick(null, null!);
            return;
        }

        if (key == Key.R)
        {
            OnResetClick(null, null!);
            return;
        }

        ApplyControllerKey(key, true);
    }

    private void ApplyControllerKey(Key key, bool pressed)
    {
        if (pressed)
        {
            if (key == _config.Keys.A) _controllerState |= 0x80;
            else if (key == _config.Keys.B) _controllerState |= 0x40;
            else if (key == _config.Keys.Select) _controllerState |= 0x20;
            else if (key == _config.Keys.Start) _controllerState |= 0x10;
            else if (key == _config.Keys.Up) _controllerState |= 0x08;
            else if (key == _config.Keys.Down) _controllerState |= 0x04;
            else if (key == _config.Keys.Left) _controllerState |= 0x02;
            else if (key == _config.Keys.Right) _controllerState |= 0x01;
        }
        else
        {
            if (key == _config.Keys.A) _controllerState = (byte)(_controllerState & ~0x80);
            else if (key == _config.Keys.B) _controllerState = (byte)(_controllerState & ~0x40);
            else if (key == _config.Keys.Select) _controllerState = (byte)(_controllerState & ~0x20);
            else if (key == _config.Keys.Start) _controllerState = (byte)(_controllerState & ~0x10);
            else if (key == _config.Keys.Up) _controllerState = (byte)(_controllerState & ~0x08);
            else if (key == _config.Keys.Down) _controllerState = (byte)(_controllerState & ~0x04);
            else if (key == _config.Keys.Left) _controllerState = (byte)(_controllerState & ~0x02);
            else if (key == _config.Keys.Right) _controllerState = (byte)(_controllerState & ~0x01);
        }

        _emulation.SetControllerState(_controllerState);
    }

    private static bool TryMapSdlKey(uint keyCode, out Key key)
    {
        if (keyCode is >= (uint)'a' and <= (uint)'z')
        {
            key = (Key)Enum.Parse(typeof(Key), ((char)keyCode).ToString().ToUpperInvariant());
            return true;
        }

        key = keyCode switch
        {
            0x1B => Key.Escape,
            0x09 => Key.Tab,
            0x20 => Key.Space,
            0x4000003A => Key.F1,
            0x40000044 => Key.F11,
            0x4000004F => Key.Right,
            0x40000050 => Key.Left,
            0x40000051 => Key.Down,
            0x40000052 => Key.Up,
            _ => Key.None
        };

        return key != Key.None;
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
        if (!_emulation.IsRomLoaded) return;
        _emulation.Reset();
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
        _sdlRenderer?.SetBilinearFilter(_config.BilinearFilter);
        UpdateMenuState();
    }

    private void OnToggleSoundClick(object? sender, RoutedEventArgs e)
    {
        _config.SoundEnabled = !_config.SoundEnabled;
        _emulation.SoundEnabled = _config.SoundEnabled;
        UpdateMenuState();
    }

    private async void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        bool wasPaused = _emulation.IsPaused;
        _emulation.IsPaused = true;

        var dialog = new SettingsWindow(_config);
        dialog.SettingsApplied += ApplySettings;
        await dialog.ShowDialog(this);

        _emulation.IsPaused = wasPaused;
    }

    private void ApplySettings(AppConfig updated)
    {
        string previousBackend = _config.RendererBackend;
        string previousLibrary = _config.LibraryDirectory;

        _config.CopyFrom(updated);
        _emulation.SoundEnabled = _config.SoundEnabled;
        _emulation.Volume = _config.Volume;

        ApplyAspectRatio();
        ApplyScale(_config.WindowScale);
        RenderOptions.SetBitmapInterpolationMode(
            GameScreen,
            _config.BilinearFilter ? BitmapInterpolationMode.LowQuality : BitmapInterpolationMode.None);
        _gameWindow?.SetBilinearFilter(_config.BilinearFilter);
        _sdlRenderer?.SetBilinearFilter(_config.BilinearFilter);

        if (!string.Equals(previousLibrary, _config.LibraryDirectory, StringComparison.OrdinalIgnoreCase))
            ScanLibraryDirectory(_config.LibraryDirectory, persist: false);
        else
            UpdateLibraryPanel();

        if (_emulation.IsRomLoaded
            && !string.Equals(previousBackend, _config.RendererBackend, StringComparison.OrdinalIgnoreCase))
        {
            string gameName = Path.GetFileName(_session.RomPath ?? "game.nes");
            CloseGameWindow();
            ShowGameWindow(gameName);
        }

        UpdateRecentMenu();
        UpdateMenuState();
        UpdateLoadedRomInfo();
        _config.Save("config.ini");
    }

    private async void OnControllerConfigClick(object? sender, RoutedEventArgs e)
    {
        bool wasPaused = _emulation.IsPaused;
        _emulation.IsPaused = true;
        var dlg = new ControllerConfigWindow(_config.Keys);
        await dlg.ShowDialog(this);
        if (dlg.IsSaved)
        {
            _config.Save("config.ini");
        }
        _emulation.IsPaused = wasPaused;
    }

    private void OnPatternTableClick(object? sender, RoutedEventArgs e)
    {
        new PatternTableWindow(_session.Bus).Show(this);
    }

    private async void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        await new AboutWindow().ShowDialog(this);
    }

    private void OnTakeScreenshotClick(object? sender, RoutedEventArgs e)
    {
        if (!_emulation.IsRomLoaded) return;
        try
        {
            string fileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            _screenBitmap.Save(fileName);
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
            _emulation.FastForward = !_emulation.FastForward;
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

        _emulation.SetControllerState(_controllerState);
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

        _emulation.SetControllerState(_controllerState);
    }

    private void OnFrameReady()
    {
        if (Interlocked.Exchange(ref _renderPending, 1) != 0)
            return;

        Dispatcher.UIThread.Post(PresentLatestFrame, DispatcherPriority.Render);
    }

    private unsafe void PresentLatestFrame()
    {
        try
        {
            if (_framePipeline.TryAcquireLatest(out var frame))
            {
                using (frame)
                using (var locked = _screenBitmap.Lock())
                {
                    var destination = new Span<uint>((void*)locked.Address, FrameBufferPipeline.PixelCount);
                    frame.Buffer.AsSpan().CopyTo(destination);
                    _sdlRenderer?.Present(frame.Buffer);
                }

                GameScreen.InvalidateVisual();
                _gameWindow?.InvalidateGameScreen();
            }
        }
        finally
        {
            Volatile.Write(ref _renderPending, 0);
            if (_framePipeline.HasPublishedFrame)
                OnFrameReady();
        }
    }

    private void OnFpsUpdated(double fps)
    {
        Dispatcher.UIThread.Post(() => TxtFps.Text = $"{fps:F1} FPS");
    }

    private void OnEmulationFaulted(Exception exception)
    {
        Console.Error.WriteLine($"Emulation thread stopped: {exception}");
        Dispatcher.UIThread.Post(() => TxtStatus.Text = "EMULATION ERROR");
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        CloseGameWindow();
        _emulation.Dispose();
        _screenBitmap.Dispose();
        _config.Save("config.ini");

        if (OperatingSystem.IsWindows())
        {
            TimeEndPeriod(1);
        }
    }
}
