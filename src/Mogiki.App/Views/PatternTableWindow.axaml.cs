using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Mogiki.Core.Bus;

namespace Mogiki.App.Views;

public partial class PatternTableWindow : Window
{
    private readonly Bus _bus;
    private readonly WriteableBitmap _patternBitmap;
    private readonly DispatcherTimer _timer;
    private byte _selectedPalette;

    public PatternTableWindow() : this(new Bus()) { }

    public PatternTableWindow(Bus bus)
    {
        _bus = bus;
        _patternBitmap = new WriteableBitmap(
            new PixelSize(256, 128),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);

        InitializeComponent();

        ComboPalette.SelectedIndex = 0;
        ImgPattern.Source = _patternBitmap;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => UpdatePatternTable();
        _timer.Start();

        Closed += (_, _) => _timer.Stop();

        UpdatePatternTable();
    }

    private void OnPaletteChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedPalette = (byte)Math.Clamp(ComboPalette.SelectedIndex, 0, 7);
        UpdatePatternTable();
    }

    private unsafe void UpdatePatternTable()
    {
        using var locked = _patternBitmap.Lock();
        uint* scan0 = (uint*)locked.Address;

        for (int table = 0; table < 2; table++)
        {
            for (int tileY = 0; tileY < 16; tileY++)
            {
                for (int tileX = 0; tileX < 16; tileX++)
                {
                    int tileOffset = tileY * 256 + tileX * 16;

                    for (int row = 0; row < 8; row++)
                    {
                        ushort addrLo = (ushort)(table * 0x1000 + tileOffset + row);
                        ushort addrHi = (ushort)(addrLo + 8);

                        byte tileLsb = _bus.Ppu.PpuRead(addrLo);
                        byte tileMsb = _bus.Ppu.PpuRead(addrHi);

                        for (int col = 0; col < 8; col++)
                        {
                            byte p0 = (byte)((tileLsb & (0x80 >> col)) != 0 ? 1 : 0);
                            byte p1 = (byte)((tileMsb & (0x80 >> col)) != 0 ? 1 : 0);
                            byte pixel = (byte)((p1 << 1) | p0);

                            var color = _bus.Ppu.GetColorFromPaletteRam(_selectedPalette, pixel);
                            uint bgra = (uint)((255 << 24) | (color.R << 16) | (color.G << 8) | color.B);

                            int x = table * 128 + tileX * 8 + col;
                            int y = tileY * 8 + row;
                            scan0[y * 256 + x] = bgra;
                        }
                    }
                }
            }
        }

        ImgPattern.InvalidateVisual();
    }
}
