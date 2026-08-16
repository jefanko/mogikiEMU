using Mogiki.Core.Common;

namespace Mogiki.Core.Mappers;

/// <summary>
/// Mapper 002: UxROM (Nintendo).
/// </summary>
public sealed class Mapper002 : Mapper
{
    private readonly MirrorMode _mirrorMode;
    private byte _nPRGBankSelect;

    public Mapper002(byte prgBanks, byte chrBanks, MirrorMode hwMirror)
        : base(prgBanks, chrBanks)
    {
        _mirrorMode = hwMirror;
        Reset();
    }

    public override MirrorMode Mirror => _mirrorMode;

    public override void Reset()
    {
        _nPRGBankSelect = 0;
    }

    public override bool CpuMapRead(ushort addr, out uint mappedAddr)
    {
        if (addr is >= 0x8000 and <= 0xBFFF)
        {
            mappedAddr = (uint)(_nPRGBankSelect * 0x4000 + (addr & 0x3FFF));
            return true;
        }

        if (addr is >= 0xC000 and <= 0xFFFF)
        {
            mappedAddr = (uint)((nPRGBanks - 1) * 0x4000 + (addr & 0x3FFF));
            return true;
        }

        mappedAddr = 0;
        return false;
    }

    public override bool CpuMapWrite(ushort addr, out uint mappedAddr, byte data)
    {
        if (addr is >= 0x8000 and <= 0xFFFF)
        {
            _nPRGBankSelect = (byte)(data & 0x0F);
        }
        mappedAddr = 0;
        return false;
    }

    public override bool CpuMapWrite(ushort addr, out uint mappedAddr)
    {
        mappedAddr = 0;
        return false;
    }

    public override bool PpuMapRead(ushort addr, out uint mappedAddr)
    {
        if (addr < 0x2000)
        {
            mappedAddr = addr;
            return true;
        }
        mappedAddr = 0;
        return false;
    }

    public override bool PpuMapWrite(ushort addr, out uint mappedAddr)
    {
        if (addr < 0x2000)
        {
            mappedAddr = addr;
            return true;
        }
        mappedAddr = 0;
        return false;
    }
}
