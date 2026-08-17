using Mogiki.Core.Bus;
using Mogiki.Core.Cartridge;

namespace Mogiki.App.Emulation;

/// <summary>
/// Owns the lifetime of the emulated NES machine and its current cartridge.
/// UI code can inspect the bus for tools, but only the runner clocks it.
/// </summary>
public sealed class EmulatorSession
{
    private readonly object _sync = new();

    public Bus Bus { get; } = new();
    public object SyncRoot => _sync;

    public bool IsLoaded { get; private set; }
    public string? RomPath { get; private set; }
    public Cartridge? Cartridge { get; private set; }

    public bool LoadRom(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        lock (_sync)
        {
            var cartridge = new Cartridge(path);
            if (!cartridge.ImageValid)
                return false;

            Bus.InsertCartridge(cartridge);
            Bus.Reset();

            Cartridge = cartridge;
            RomPath = path;
            IsLoaded = true;
            return true;
        }
    }

    public void Unload()
    {
        lock (_sync)
        {
            IsLoaded = false;
            RomPath = null;
            Cartridge = null;
            Bus.RemoveCartridge();
            Bus.Reset();
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            if (IsLoaded)
                Bus.Reset();
        }
    }

    public void Clock()
    {
        Bus.Clock();
    }
}
