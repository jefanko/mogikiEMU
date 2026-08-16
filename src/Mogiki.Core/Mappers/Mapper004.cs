using Mogiki.Core.Common;

namespace Mogiki.Core.Mappers;

/// <summary>
/// Mapper 004: MMC3 / TxROM (Nintendo).
/// </summary>
public sealed class Mapper004 : Mapper
{
    private byte _nTargetRegister;
    private bool _bPRGBankMode;
    private bool _bCHRInversion;
    private MirrorMode _mirrorMode;

    private readonly byte[] _pRegister = new byte[8];
    private readonly uint[] _pCHRBank = new uint[8];
    private readonly uint[] _pPRGBank = new uint[4];

    private bool _bIRQActive;
    private bool _bIRQEnable;
    private bool _bIRQUpdate;
    private byte _nIRQCounter;
    private byte _nIRQReload;

    public byte[] PrgRam { get; } = new byte[8192];

    public Mapper004(byte prgBanks, byte chrBanks)
        : base(prgBanks, chrBanks)
    {
        Reset();
    }

    public override MirrorMode Mirror => _mirrorMode;
    public override bool IrqState => _bIRQActive && _bIRQEnable;
    public override void IrqClear() => _bIRQActive = false;

    public override void Reset()
    {
        _nTargetRegister = 0;
        _bPRGBankMode = false;
        _bCHRInversion = false;
        _mirrorMode = MirrorMode.Horizontal;

        _bIRQActive = false;
        _bIRQEnable = false;
        _bIRQUpdate = false;
        _nIRQCounter = 0;
        _nIRQReload = 0;

        Array.Clear(_pRegister, 0, _pRegister.Length);
        UpdateBanks();
    }

    private void UpdateBanks()
    {
        // Update CHR banks
        // R0/R1 select 2KB banks (bit 0 ignored), R2-R5 select 1KB banks
        if (_bCHRInversion)
        {
            _pCHRBank[0] = (uint)(_pRegister[2] * 0x0400);
            _pCHRBank[1] = (uint)(_pRegister[3] * 0x0400);
            _pCHRBank[2] = (uint)(_pRegister[4] * 0x0400);
            _pCHRBank[3] = (uint)(_pRegister[5] * 0x0400);
            _pCHRBank[4] = (uint)((_pRegister[0] & 0xFE) * 0x0400);
            _pCHRBank[5] = (uint)(((_pRegister[0] & 0xFE) + 1) * 0x0400);
            _pCHRBank[6] = (uint)((_pRegister[1] & 0xFE) * 0x0400);
            _pCHRBank[7] = (uint)(((_pRegister[1] & 0xFE) + 1) * 0x0400);
        }
        else
        {
            _pCHRBank[0] = (uint)((_pRegister[0] & 0xFE) * 0x0400);
            _pCHRBank[1] = (uint)(((_pRegister[0] & 0xFE) + 1) * 0x0400);
            _pCHRBank[2] = (uint)((_pRegister[1] & 0xFE) * 0x0400);
            _pCHRBank[3] = (uint)(((_pRegister[1] & 0xFE) + 1) * 0x0400);
            _pCHRBank[4] = (uint)(_pRegister[2] * 0x0400);
            _pCHRBank[5] = (uint)(_pRegister[3] * 0x0400);
            _pCHRBank[6] = (uint)(_pRegister[4] * 0x0400);
            _pCHRBank[7] = (uint)(_pRegister[5] * 0x0400);
        }

        // Update PRG banks
        uint num8K = (uint)(nPRGBanks * 2);
        if (_bPRGBankMode)
        {
            _pPRGBank[0] = (num8K - 2) * 0x2000;
            _pPRGBank[1] = (uint)((_pRegister[7] & 0x3F) * 0x2000);
            _pPRGBank[2] = (uint)((_pRegister[6] & 0x3F) * 0x2000);
            _pPRGBank[3] = (num8K - 1) * 0x2000;
        }
        else
        {
            _pPRGBank[0] = (uint)((_pRegister[6] & 0x3F) * 0x2000);
            _pPRGBank[1] = (uint)((_pRegister[7] & 0x3F) * 0x2000);
            _pPRGBank[2] = (num8K - 2) * 0x2000;
            _pPRGBank[3] = (num8K - 1) * 0x2000;
        }
    }

    public override bool CpuMapRead(ushort addr, out uint mappedAddr)
    {
        if (addr is >= 0x6000 and <= 0x7FFF)
        {
            mappedAddr = PrgRamSentinel;
            return true;
        }

        uint prgRomSize = (uint)(nPRGBanks * 16384);
        if (prgRomSize == 0) prgRomSize = 16384;

        if (addr is >= 0x8000 and <= 0x9FFF)
        {
            mappedAddr = (_pPRGBank[0] + (uint)(addr & 0x1FFF)) % prgRomSize;
            return true;
        }
        if (addr is >= 0xA000 and <= 0xBFFF)
        {
            mappedAddr = (_pPRGBank[1] + (uint)(addr & 0x1FFF)) % prgRomSize;
            return true;
        }
        if (addr is >= 0xC000 and <= 0xDFFF)
        {
            mappedAddr = (_pPRGBank[2] + (uint)(addr & 0x1FFF)) % prgRomSize;
            return true;
        }
        if (addr is >= 0xE000 and <= 0xFFFF)
        {
            mappedAddr = (_pPRGBank[3] + (uint)(addr & 0x1FFF)) % prgRomSize;
            return true;
        }

        mappedAddr = 0;
        return false;
    }

    public override bool CpuMapWrite(ushort addr, out uint mappedAddr, byte data)
    {
        if (addr is >= 0x6000 and <= 0x7FFF)
        {
            mappedAddr = PrgRamSentinel;
            PrgRam[addr & 0x1FFF] = data;
            return true;
        }

        if (addr is >= 0x8000 and <= 0x9FFF)
        {
            if ((addr & 0x0001) == 0)
            {
                // Bank Select ($8000)
                _nTargetRegister = (byte)(data & 0x07);
                _bPRGBankMode = (data & 0x40) != 0;
                _bCHRInversion = (data & 0x80) != 0;
                UpdateBanks();
            }
            else
            {
                // Bank Data ($8001)
                _pRegister[_nTargetRegister] = data;
                UpdateBanks();
            }
            mappedAddr = 0;
            return false;
        }

        if (addr is >= 0xA000 and <= 0xBFFF)
        {
            if ((addr & 0x0001) == 0)
            {
                // Mirroring ($A000)
                _mirrorMode = (data & 0x01) != 0 ? MirrorMode.Horizontal : MirrorMode.Vertical;
            }
            mappedAddr = 0;
            return false;
        }

        if (addr is >= 0xC000 and <= 0xDFFF)
        {
            if ((addr & 0x0001) == 0)
            {
                // IRQ Latch ($C000)
                _nIRQReload = data;
            }
            else
            {
                // IRQ Reload ($C001)
                _bIRQUpdate = true;
            }
            mappedAddr = 0;
            return false;
        }

        if (addr is >= 0xE000 and <= 0xFFFF)
        {
            if ((addr & 0x0001) == 0)
            {
                // IRQ Disable ($E000)
                _bIRQEnable = false;
                _bIRQActive = false;
            }
            else
            {
                // IRQ Enable ($E001)
                _bIRQEnable = true;
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
            mappedAddr = (_pCHRBank[(addr >> 10) & 0x07] + (uint)(addr & 0x03FF)) % chrRomSize;
            return true;
        }
        mappedAddr = 0;
        return false;
    }

    public override bool PpuMapWrite(ushort addr, out uint mappedAddr)
    {
        mappedAddr = 0;
        return false;
    }

    public override void Scanline()
    {
        // NESdev MMC3 scanline counter logic
        if (_nIRQCounter == 0 || _bIRQUpdate)
        {
            _nIRQCounter = _nIRQReload;
            _bIRQUpdate = false;
        }
        else
        {
            _nIRQCounter--;
        }

        if (_nIRQCounter == 0 && _bIRQEnable)
        {
            _bIRQActive = true;
        }
    }
}
