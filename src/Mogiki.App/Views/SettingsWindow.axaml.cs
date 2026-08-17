using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Mogiki.App.Config;

namespace Mogiki.App.Views;

public partial class SettingsWindow : Window
{
    private readonly AppConfig _workingConfig;

    public SettingsWindow() : this(new AppConfig()) { }

    public SettingsWindow(AppConfig currentConfig)
    {
        _workingConfig = currentConfig.Clone();
        InitializeComponent();
        LoadControls();
    }

    public bool IsSaved { get; private set; }
    public AppConfig? AppliedConfig { get; private set; }

    public event Action<AppConfig>? SettingsApplied;

    private void LoadControls()
    {
        SelectByTag(RendererBackendCombo, AppConfig.NormalizeRendererBackend(_workingConfig.RendererBackend));
        SelectByTag(ScaleCombo, _workingConfig.WindowScale.ToString());
        SelectByTag(AspectRatioCombo, _workingConfig.AspectRatio.ToString());

        BilinearCheckBox.IsChecked = _workingConfig.BilinearFilter;
        StartFullscreenCheckBox.IsChecked = _workingConfig.StartFullscreen;
        SoundEnabledCheckBox.IsChecked = _workingConfig.SoundEnabled;
        VolumeSlider.Value = _workingConfig.Volume;

        UpdateRendererHint();
        UpdateVolumeText();
        UpdateControllerSummary();
        UpdateLibrarySummary();
    }

    private static void SelectByTag(ComboBox combo, string tag)
    {
        combo.SelectedItem = combo.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase));
    }

    private static string SelectedTag(ComboBox combo, string fallback)
    {
        return combo.SelectedItem is ComboBoxItem item && item.Tag != null
            ? item.Tag.ToString() ?? fallback
            : fallback;
    }

    private void ReadControls()
    {
        _workingConfig.RendererBackend = AppConfig.NormalizeRendererBackend(
            SelectedTag(RendererBackendCombo, _workingConfig.RendererBackend));

        if (int.TryParse(SelectedTag(ScaleCombo, _workingConfig.WindowScale.ToString()), out int scale))
            _workingConfig.WindowScale = Math.Clamp(scale, 1, 6);

        if (Enum.TryParse<AspectRatioMode>(SelectedTag(AspectRatioCombo, _workingConfig.AspectRatio.ToString()), out var aspect))
            _workingConfig.AspectRatio = aspect;

        _workingConfig.BilinearFilter = BilinearCheckBox.IsChecked == true;
        _workingConfig.StartFullscreen = StartFullscreenCheckBox.IsChecked == true;
        _workingConfig.SoundEnabled = SoundEnabledCheckBox.IsChecked == true;
        _workingConfig.Volume = Math.Clamp((int)Math.Round(VolumeSlider.Value), 0, 100);
    }

    private bool ApplyChanges()
    {
        ReadControls();
        AppliedConfig = _workingConfig.Clone();
        SettingsApplied?.Invoke(AppliedConfig.Clone());
        SettingsStatus.Text = "Changes applied.";
        return true;
    }

    private void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        ApplyChanges();
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (!ApplyChanges())
            return;

        IsSaved = true;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        IsSaved = false;
        Close();
    }

    private void OnResetDefaultsClick(object? sender, RoutedEventArgs e)
    {
        var defaults = new AppConfig();
        _workingConfig.Keys.CopyFrom(defaults.Keys);
        _workingConfig.WindowScale = defaults.WindowScale;
        _workingConfig.AspectRatio = defaults.AspectRatio;
        _workingConfig.BilinearFilter = defaults.BilinearFilter;
        _workingConfig.RendererBackend = defaults.RendererBackend;
        _workingConfig.StartFullscreen = defaults.StartFullscreen;
        _workingConfig.SoundEnabled = defaults.SoundEnabled;
        _workingConfig.Volume = defaults.Volume;
        LoadControls();
        SettingsStatus.Text = "Defaults loaded. Press Apply or Save & Close to keep them.";
    }

    private void OnVolumeChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        UpdateVolumeText();
    }

    private void OnRendererBackendChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateRendererHint();
    }

    private void UpdateVolumeText()
    {
        VolumeText.Text = $"{(int)Math.Round(VolumeSlider.Value)}%";
    }

    private void UpdateRendererHint()
    {
        RendererBackendHint.Text = SelectedTag(RendererBackendCombo, "auto") switch
        {
            "auto" => "Automatic tries Vulkan, OpenGL, then Direct3D.",
            "vulkan" => "Requires a Vulkan-capable graphics driver and SDL3 video support.",
            "opengl" => "Uses SDL3's OpenGL accelerated renderer.",
            "direct3d11" => "Uses SDL3's Direct3D 11 accelerated renderer.",
            "direct3d12" => "Uses SDL3's Direct3D 12 accelerated renderer.",
            "avalonia" => "Uses the Avalonia bitmap window and does not create an SDL3 game renderer.",
            _ => "Mogiki will choose an available presentation backend."
        };
    }

    private void UpdateControllerSummary()
    {
        var keys = _workingConfig.Keys;
        ControllerSummary.Text =
            $"D-pad: {keys.Up} / {keys.Down} / {keys.Left} / {keys.Right}    " +
            $"A: {keys.A}    B: {keys.B}    Start: {keys.Start}    Select: {keys.Select}";
    }

    private void UpdateLibrarySummary()
    {
        LibraryPathText.Text = string.IsNullOrWhiteSpace(_workingConfig.LibraryDirectory)
            ? "No folder selected"
            : _workingConfig.LibraryDirectory;

        int existingRecent = _workingConfig.RecentRoms.Count(File.Exists);
        RecentGamesText.Text = existingRecent == 0
            ? "No recent ROMs are currently available."
            : $"{existingRecent} recent ROM(s) will appear in the launcher.";
    }

    private async void OnControllerSetupClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new ControllerConfigWindow(_workingConfig.Keys);
        await dialog.ShowDialog(this);
        UpdateControllerSummary();
    }

    private async void OnChooseLibraryClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null)
            return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select NES game directory",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            string? path = folders[0].TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(path))
            {
                _workingConfig.LibraryDirectory = path;
                UpdateLibrarySummary();
            }
        }
    }

    private void OnClearLibraryClick(object? sender, RoutedEventArgs e)
    {
        _workingConfig.LibraryDirectory = "";
        UpdateLibrarySummary();
    }
}
