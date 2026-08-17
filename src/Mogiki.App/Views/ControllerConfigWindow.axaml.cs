using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Mogiki.App.Config;

namespace Mogiki.App.Views;

public partial class ControllerConfigWindow : Window
{
    private readonly KeyBindings _targetBindings;
    private readonly KeyBindings _bindings;
    private Button? _activeButton;
    private string? _activeAction;

    public bool IsSaved { get; private set; }

    public ControllerConfigWindow() : this(new KeyBindings()) { }

    public ControllerConfigWindow(KeyBindings bindings)
    {
        _targetBindings = bindings;
        _bindings = bindings.Clone();
        InitializeComponent();

        RefreshButtonLabels();
    }

    private void RefreshButtonLabels()
    {
        BtnUp.Content = _bindings.Up.ToString();
        BtnDown.Content = _bindings.Down.ToString();
        BtnLeft.Content = _bindings.Left.ToString();
        BtnRight.Content = _bindings.Right.ToString();
        BtnA.Content = _bindings.A.ToString();
        BtnB.Content = _bindings.B.ToString();
        BtnStart.Content = _bindings.Start.ToString();
        BtnSelect.Content = _bindings.Select.ToString();

        BtnUp.Background = new SolidColorBrush(Color.Parse("#202329"));
        BtnDown.Background = new SolidColorBrush(Color.Parse("#202329"));
        BtnLeft.Background = new SolidColorBrush(Color.Parse("#202329"));
        BtnRight.Background = new SolidColorBrush(Color.Parse("#202329"));
        BtnA.Background = new SolidColorBrush(Color.Parse("#202329"));
        BtnB.Background = new SolidColorBrush(Color.Parse("#202329"));
        BtnStart.Background = new SolidColorBrush(Color.Parse("#202329"));
        BtnSelect.Background = new SolidColorBrush(Color.Parse("#202329"));
    }

    private void OnKeyButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string action)
        {
            RefreshButtonLabels();
            _activeButton = btn;
            _activeAction = action;
            btn.Content = "<Press Key>";
            btn.Background = new SolidColorBrush(Color.Parse("#EE7448"));
        }
    }

    private void OnKeyDownHandler(object? sender, KeyEventArgs e)
    {
        if (_activeButton != null && _activeAction != null)
        {
            var key = e.Key;
            if (key == Key.Escape)
            {
                _activeButton = null;
                _activeAction = null;
                RefreshButtonLabels();
                e.Handled = true;
                return;
            }

            switch (_activeAction)
            {
                case "Up": _bindings.Up = key; break;
                case "Down": _bindings.Down = key; break;
                case "Left": _bindings.Left = key; break;
                case "Right": _bindings.Right = key; break;
                case "A": _bindings.A = key; break;
                case "B": _bindings.B = key; break;
                case "Start": _bindings.Start = key; break;
                case "Select": _bindings.Select = key; break;
            }

            _activeButton = null;
            _activeAction = null;
            RefreshButtonLabels();
            e.Handled = true;
        }
    }

    private void OnResetDefaultsClick(object? sender, RoutedEventArgs e)
    {
        _bindings.Up = Key.Up;
        _bindings.Down = Key.Down;
        _bindings.Left = Key.Left;
        _bindings.Right = Key.Right;
        _bindings.A = Key.X;
        _bindings.B = Key.Z;
        _bindings.Start = Key.S;
        _bindings.Select = Key.A;
        RefreshButtonLabels();
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        _targetBindings.CopyFrom(_bindings);
        IsSaved = true;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        IsSaved = false;
        Close();
    }
}
