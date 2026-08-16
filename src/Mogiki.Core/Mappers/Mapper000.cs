using Mogiki.Core.Common;

namespace Mogiki.Core.Mappers;

/// <summary>
/// Mapper 000: NROM (Standard NES Mapper).
/// </summary>
public sealed class Mapper000 : Mapper
{
    private readonly MirrorMode _mirrorMode;

    public Mapper000(byte prgBanks, byte chrBanks, MirrorMode hwMirror)
        : base(prgBanks, chrBanks)
    {
        _mirrorMode = hwMirror;
    }

    public override MirrorMode Mirror => _mirrorMode;

    public override bool CpuMapRead(ushort addr, out uint mappedAddr)
    {
        if (addr is >= 0x8000 and <= 0xFFFF)
        {
            mappedAddr = (uint)(addr & (nPRGBanks > 1 ? 0x7FFF : 0x3FFF));
            return true;
        }
        mappedAddr = 0;
        return false;
    }

    public override bool CpuMapWrite(ushort addr, out uint mappedAddr)
    {
        if (addr is >= 0x8000 and <= 0xFFFF)
        {
            mappedAddr = (uint)(addr & (nPRGBanks > 1 ? 0x7FFF : 0x3FFF));
            return true;
        }
        mappedAddr = 0;
        return false;
    }

    public override bool PpuMapRead(ushort addr, out uint mappedAddr)
    {
        if (addr <= 0x1FFF)
        {
            mappedAddr = addr;
            return true;
        }
        mappedAddr = 0;
        return false;
    }

    public override bool PpuMapWrite(ushort addr, out uint mappedAddr)
    {
        if (addr <= 0x1FFF && nCHRBanks == 0) // CHR RAM
        {
            mappedAddr = addr;
            return true;
        }
        mappedAddr = 0;
        return false;
    }
}
