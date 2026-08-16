using Mogiki.Core.Common;

namespace Mogiki.Core.Mappers;

/// <summary>
/// Mapper 069: Sunsoft FME-7 / Sunsoft 5B.
/// </summary>
public sealed class Mapper069 : Mapper
{
    private byte _commandRegister;
    private readonly byte[] _prgBank = new byte[4];
    private readonly byte[] _chrBank = new byte[8];

    private bool _prgRamEnable;
    private bool _prgRamSelect;
    private MirrorMode _mirrorMode = MirrorMode.Vertical;

    private bool _bIRQEnable;
    private bool _bIRQCounterEnable;
    private bool _bIRQActive;
    private ushort _irqCounter;

    public byte[] PrgRam { get; } = new byte[8192];

    public Mapper069(byte prgBanks, byte chrBanks)
        : base(prgBanks, chrBanks)
    {
        Reset();
    }

    public override MirrorMode Mirror => _mirrorMode;
    public override bool IrqState => _bIRQActive && _bIRQEnable;
    public override void IrqClear() => _bIRQActive = false;

    public override void Reset()
    {
        _commandRegister = 0;

        _prgBank[0] = 0;
        _prgBank[1] = 0;
        _prgBank[2] = 0;
        _prgBank[3] = 0;
        _prgRamEnable = false;
        _prgRamSelect = false;

        for (int i = 0; i < 8; i++)
        {
            _chrBank[i] = (byte)i;
        }

        _mirrorMode = MirrorMode.Vertical;
        _bIRQEnable = false;
        _bIRQCounterEnable = false;
        _bIRQActive = false;
        _irqCounter = 0;
    }

    public void CountIRQ()
    {
        if (_bIRQCounterEnable)
        {
            _irqCounter--;
            if (_irqCounter == 0xFFFF && _bIRQEnable)
            {
                _bIRQActive = true;
            }
        }
    }

    public override bool CpuMapRead(ushort addr, out uint mappedAddr)
    {
        uint prgRomSize = (uint)(nPRGBanks * 16384);

        // $6000-$7FFF: PRG RAM or ROM bank
        if (addr is >= 0x6000 and <= 0x7FFF)
        {
            if (_prgRamSelect)
            {
                mappedAddr = PrgRamSentinel;
                return true;
            }
            uint bank = (uint)(_prgBank[0] & 0x3F);
            mappedAddr = ((bank * 8192) + (uint)(addr & 0x1FFF)) % prgRomSize;
            return true;
        }

        // $8000-$9FFF
        if (addr is >= 0x8000 and <= 0x9FFF)
        {
            uint bank = (uint)(_prgBank[1] & 0x3F);
            mappedAddr = ((bank * 8192) + (uint)(addr & 0x1FFF)) % prgRomSize;
            return true;
        }

        // $A000-$BFFF
        if (addr is >= 0xA000 and <= 0xBFFF)
        {
            uint bank = (uint)(_prgBank[2] & 0x3F);
            mappedAddr = ((bank * 8192) + (uint)(addr & 0x1FFF)) % prgRomSize;
            return true;
        }

        // $C000-$DFFF
        if (addr is >= 0xC000 and <= 0xDFFF)
        {
            uint bank = (uint)(_prgBank[3] & 0x3F);
            mappedAddr = ((bank * 8192) + (uint)(addr & 0x1FFF)) % prgRomSize;
            return true;
        }

        // $E000-$FFFF
        if (addr >= 0xE000)
        {
            uint lastBank = (uint)((nPRGBanks * 2) - 1);
            mappedAddr = ((lastBank * 8192) + (uint)(addr & 0x1FFF)) % prgRomSize;
            return true;
        }

        mappedAddr = 0;
        return false;
    }

    public override bool CpuMapWrite(ushort addr, out uint mappedAddr, byte data)
    {
        if (addr is >= 0x6000 and <= 0x7FFF)
        {
            if (_prgRamSelect && _prgRamEnable)
            {
                PrgRam[addr & 0x1FFF] = data;
                mappedAddr = PrgRamSentinel;
                return true;
            }
            mappedAddr = 0;
            return false;
        }

        // $8000-$9FFF: Command Register
        if (addr is >= 0x8000 and <= 0x9FFF)
        {
            _commandRegister = (byte)(data & 0x0F);
            mappedAddr = 0;
            return false;
        }

        // $A000-$BFFF: Parameter Register
        if (addr is >= 0xA000 and <= 0xBFFF)
        {
            switch (_commandRegister)
            {
                case <= 0x7:
                    _chrBank[_commandRegister] = data;
                    break;

                case 0x8:
                    _prgRamEnable = (data & 0x80) != 0;
                    _prgRamSelect = (data & 0x40) != 0;
                    _prgBank[0] = (byte)(data & 0x3F);
                    break;

                case 0x9:
                    _prgBank[1] = (byte)(data & 0x3F);
                    break;

                case 0xA:
                    _prgBank[2] = (byte)(data & 0x3F);
                    break;

                case 0xB:
                    _prgBank[3] = (byte)(data & 0x3F);
                    break;

                case 0xC:
                    _mirrorMode = (data & 0x03) switch
                    {
                        0 => MirrorMode.Vertical,
                        1 => MirrorMode.Horizontal,
                        2 => MirrorMode.OneScreenLo,
                        _ => MirrorMode.OneScreenHi
                    };
                    break;

                case 0xD:
                    _bIRQEnable = (data & 0x01) != 0;
                    _bIRQCounterEnable = (data & 0x80) != 0;
                    if (!_bIRQEnable)
                    {
                        _bIRQActive = false;
                    }
                    break;

                case 0xE:
                    _irqCounter = (ushort)((_irqCounter & 0xFF00) | data);
                    break;

                case 0xF:
                    _irqCounter = (ushort)((_irqCounter & 0x00FF) | (data << 8));
                    break;
            }
            mappedAddr = 0;
            return false;
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
            uint chrRomSize = (uint)(nCHRBanks * 8192);
            if (chrRomSize == 0) chrRomSize = 8192;

            int bankIndex = (addr >> 10) & 0x07;
            uint bank = _chrBank[bankIndex];
            mappedAddr = ((bank * 1024) + (uint)(addr & 0x03FF)) % chrRomSize;
            return true;
        }
        mappedAddr = 0;
        return false;
    }

    public override bool PpuMapWrite(ushort addr, out uint mappedAddr)
    {
        if (addr < 0x2000 && nCHRBanks == 0)
        {
            mappedAddr = addr;
            return true;
        }
        mappedAddr = 0;
        return false;
    }
}
