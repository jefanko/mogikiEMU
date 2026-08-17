using Mogiki.Core.Common;

namespace Mogiki.Core.Mappers;

/// <summary>
/// Mapper 003: CNROM. CPU writes select the 8KB CHR bank.
/// </summary>
public sealed class Mapper003 : Mapper
{
    private readonly MirrorMode _mirrorMode;
    private byte _chrBank;

    public Mapper003(byte prgBanks, byte chrBanks, MirrorMode hwMirror)
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
            mappedAddr = PrgRamSentinel;
            return true;
        }

        mappedAddr = 0;
        return false;
    }

    public override bool CpuMapWrite(ushort addr, out uint mappedAddr, byte data)
    {
        if (addr is >= 0x8000 and <= 0xFFFF)
        {
            _chrBank = nCHRBanks == 0 ? (byte)0 : (byte)(data % nCHRBanks);
            mappedAddr = PrgRamSentinel;
            return true;
        }

        mappedAddr = 0;
        return false;
    }

    public override bool PpuMapRead(ushort addr, out uint mappedAddr)
    {
        if (addr <= 0x1FFF)
        {
            mappedAddr = (uint)(_chrBank * 0x2000 + addr);
            return true;
        }

        mappedAddr = 0;
        return false;
    }

    public override bool PpuMapWrite(ushort addr, out uint mappedAddr)
    {
        mappedAddr = 0;
        return addr <= 0x1FFF && nCHRBanks == 0;
    }

    public override void Reset()
    {
        _chrBank = 0;
    }
}
