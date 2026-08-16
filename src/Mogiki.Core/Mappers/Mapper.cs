using Mogiki.Core.Common;

namespace Mogiki.Core.Mappers;

/// <summary>
/// Base class for all NES cartridge mappers.
/// </summary>
public abstract class Mapper
{
    public const uint PrgRamSentinel = 0xFFFFFFFF;

    protected readonly byte nPRGBanks;
    protected readonly byte nCHRBanks;

    protected Mapper(byte prgBanks, byte chrBanks)
    {
        nPRGBanks = prgBanks;
        nCHRBanks = chrBanks;
    }

    /// <summary>
    /// Transform CPU bus address into PRG ROM/RAM offset.
    /// Returns true if the mapper handles this address.
    /// </summary>
    public abstract bool CpuMapRead(ushort addr, out uint mappedAddr);

    /// <summary>
    /// Transform CPU bus address write.
    /// </summary>
    public virtual bool CpuMapWrite(ushort addr, out uint mappedAddr, byte data)
    {
        return CpuMapWrite(addr, out mappedAddr);
    }

    public abstract bool CpuMapWrite(ushort addr, out uint mappedAddr);

    /// <summary>
    /// Transform PPU bus address into CHR ROM/RAM offset.
    /// </summary>
    public abstract bool PpuMapRead(ushort addr, out uint mappedAddr);

    /// <summary>
    /// Transform PPU bus address write.
    /// </summary>
    public abstract bool PpuMapWrite(ushort addr, out uint mappedAddr);

    /// <summary>
    /// Custom PPU direct read (used for MMC5 extended attributes/exRAM).
    /// </summary>
    public virtual bool PpuReadCustom(ushort addr, out byte data)
    {
        data = 0;
        return false;
    }

    /// <summary>
    /// Custom PPU direct write (used for MMC5 extended attributes/exRAM).
    /// </summary>
    public virtual bool PpuWriteCustom(ushort addr, byte data)
    {
        return false;
    }

    public virtual void Reset() { }

    public virtual MirrorMode Mirror => MirrorMode.Horizontal;

    public virtual void CpuSnoopWrite(ushort addr, byte data) { }
    public virtual void CpuSnoopRead(ushort addr) { }

    public virtual bool IrqState => false;
    public virtual void IrqClear() { }

    public virtual void Scanline() { }
    public virtual void Scanline(int currentScanline, int currentCycle) { }
}
