using Mogiki.Core.Common;

namespace Mogiki.Core.Mappers;

/// <summary>
/// Mapper 005: MMC5 / ExROM (Nintendo - Castlevania III).
/// </summary>
public sealed class Mapper005 : Mapper
{
    private byte _prgMode = 3;
    private byte _chrMode = 3;
    private byte _prgRamProtect1;
    private byte _prgRamProtect2;
    private byte _exRamMode;
    private byte _ntMapping;

    private byte _fillTile;
    private byte _fillColor;

    private readonly byte[] _prgBankReg = new byte[5];
    private readonly ushort[] _chrBankReg = new ushort[12];
    private byte _chrUpperBits;

    private bool _lastCHRBankWriteIsUpperHalf;
    private bool _bSprite8x16Mode;
    private bool _bRenderEnable;

    private byte _multiplierA = 0xFF;
    private byte _multiplierB = 0xFF;

    private byte _irqScanline;
    private bool _bIRQEnable;
    private bool _bIRQActive;
    private bool _bInFrame;
    private byte _scanlineCounter;

    private int _bgFetchesRemaining;
    private ushort _lastBgTileAddr;
    private byte _lastBgTileExRam;

    private MirrorMode _mirrorMode = MirrorMode.Vertical;

    public byte[] PrgRam { get; } = new byte[64 * 1024];
    public byte[] ExRam { get; } = new byte[1024];
    public byte[] InternalNametable { get; } = new byte[2048];

    public Mapper005(byte prgBanks, byte chrBanks)
        : base(prgBanks, chrBanks)
    {
        Reset();
    }

    public override MirrorMode Mirror => _mirrorMode;
    public override bool IrqState => _bIRQActive && _bIRQEnable;
    public override void IrqClear() => _bIRQActive = false;

    public override void Reset()
    {
        _prgMode = 3;
        _chrMode = 3;
        _prgRamProtect1 = 0;
        _prgRamProtect2 = 0;
        _exRamMode = 0;
        _ntMapping = 0;
        _fillTile = 0;
        _fillColor = 0;
        _chrUpperBits = 0;
        _multiplierA = 0xFF;
        _multiplierB = 0xFF;

        _bSprite8x16Mode = false;
        _bRenderEnable = false;
        _lastCHRBankWriteIsUpperHalf = false;

        _irqScanline = 0;
        _bIRQEnable = false;
        _bIRQActive = false;
        _bInFrame = false;
        _scanlineCounter = 0;
        _lastBgTileAddr = 0;
        _lastBgTileExRam = 0;
        _bgFetchesRemaining = 0;

        _mirrorMode = MirrorMode.Vertical;

        _prgBankReg[0] = 0x00; // $5113: PRG RAM bank 0
        _prgBankReg[1] = 0x80; // $5114: ROM bank 0
        _prgBankReg[2] = 0x80; // $5115: ROM bank 0
        _prgBankReg[3] = 0x80; // $5116: ROM bank 0
        _prgBankReg[4] = 0xFF; // $5117: last 8KB ROM bank

        for (int i = 0; i < 12; i++)
        {
            _chrBankReg[i] = (ushort)i;
        }
    }

    private bool IsPrgRamEnabled => _prgRamProtect1 == 0x02 && _prgRamProtect2 == 0x01;

    public byte ReadPrgRam(ushort addr)
    {
        uint ramAddr = 0;
        if (addr is >= 0x6000 and <= 0x7FFF)
        {
            byte bank = (byte)(_prgBankReg[0] & 0x07);
            ramAddr = (uint)(bank * 8192 + (addr & 0x1FFF));
        }
        else if (addr is >= 0x8000 and < 0xE000)
        {
            int regIndex = -1;
            if (_prgMode is 1 or 2)
            {
                if (addr < 0xC000)
                {
                    byte bank = (byte)(((_prgBankReg[2] & 0x06) | ((addr >> 13) & 1)) & 0x07);
                    ramAddr = (uint)(bank * 8192 + (addr & 0x1FFF));
                    if (ramAddr < PrgRam.Length) return PrgRam[ramAddr];
                }
                else if (addr < 0xE000 && _prgMode == 2)
                {
                    regIndex = 3;
                }
            }
            else if (_prgMode == 3)
            {
                if (addr < 0xA000) regIndex = 1;
                else if (addr < 0xC000) regIndex = 2;
                else if (addr < 0xE000) regIndex = 3;
            }

            if (regIndex >= 0)
            {
                byte bank = (byte)(_prgBankReg[regIndex] & 0x07);
                ramAddr = (uint)(bank * 8192 + (addr & 0x1FFF));
            }
        }

        if (ramAddr < PrgRam.Length)
        {
            return PrgRam[ramAddr];
        }
        return 0;
    }

    public byte ReadRegister(ushort addr)
    {
        if (addr == 0x5204)
        {
            byte status = 0;
            if (_bInFrame) status |= 0x40;
            if (_bIRQActive) status |= 0x80;
            _bIRQActive = false; // Reading clears IRQ pending
            return status;
        }
        if (addr == 0x5205)
        {
            return (byte)((_multiplierA * _multiplierB) & 0xFF);
        }
        if (addr == 0x5206)
        {
            return (byte)(((_multiplierA * _multiplierB) >> 8) & 0xFF);
        }
        if (addr is >= 0x5C00 and <= 0x5FFF)
        {
            if (_exRamMode >= 2)
                return ExRam[addr & 0x03FF];
            return 0;
        }
        return 0;
    }

    public override void CpuSnoopWrite(ushort addr, byte data)
    {
        if (addr is >= 0x2000 and <= 0x3FFF)
        {
            ushort reg = (ushort)(addr & 0x0007);
            if (reg == 0)
            {
                _bSprite8x16Mode = (data & 0x20) != 0;
            }
            else if (reg == 1)
            {
                bool prevRender = _bRenderEnable;
                _bRenderEnable = (data & 0x18) != 0;
                if (!_bRenderEnable && prevRender)
                {
                    _bInFrame = false;
                    _scanlineCounter = 0;
                }
            }
        }
    }

    public override void CpuSnoopRead(ushort addr)
    {
        if (addr is 0xFFFA or 0xFFFB)
        {
            _bInFrame = false;
            _scanlineCounter = 0;
            _bIRQActive = false;
        }
    }

    public override bool CpuMapRead(ushort addr, out uint mappedAddr)
    {
        if (addr is >= 0x5000 and <= 0x5FFF)
        {
            mappedAddr = PrgRamSentinel;
            return true;
        }

        if (addr is >= 0x6000 and <= 0x7FFF)
        {
            mappedAddr = PrgRamSentinel;
            return true;
        }

        if (addr >= 0x8000)
        {
            uint prgRomSize = (uint)(nPRGBanks * 16384);

            switch (_prgMode)
            {
                case 0: // 32KB
                    {
                        uint bank = (uint)((_prgBankReg[4] >> 2) & 0x1F);
                        mappedAddr = ((bank * 32768) + (uint)(addr & 0x7FFF)) % prgRomSize;
                    }
                    break;

                case 1: // Two 16KB
                    if (addr < 0xC000)
                    {
                        bool isRam = (_prgBankReg[2] & 0x80) == 0;
                        if (isRam)
                        {
                            mappedAddr = PrgRamSentinel;
                            return true;
                        }
                        uint bank = (uint)((_prgBankReg[2] >> 1) & 0x3F);
                        mappedAddr = ((bank * 16384) + (uint)(addr & 0x3FFF)) % prgRomSize;
                    }
                    else
                    {
                        uint bank = (uint)((_prgBankReg[4] >> 1) & 0x3F);
                        mappedAddr = ((bank * 16384) + (uint)(addr & 0x3FFF)) % prgRomSize;
                    }
                    break;

                case 2: // 16KB + 8KB + 8KB
                    if (addr < 0xC000)
                    {
                        bool isRam = (_prgBankReg[2] & 0x80) == 0;
                        if (isRam)
                        {
                            mappedAddr = PrgRamSentinel;
                            return true;
                        }
                        uint bank = (uint)((_prgBankReg[2] >> 1) & 0x3F);
                        mappedAddr = ((bank * 16384) + (uint)(addr & 0x3FFF)) % prgRomSize;
                    }
                    else if (addr < 0xE000)
                    {
                        bool isRam = (_prgBankReg[3] & 0x80) == 0;
                        if (isRam)
                        {
                            mappedAddr = PrgRamSentinel;
                            return true;
                        }
                        uint bank = (uint)(_prgBankReg[3] & 0x7F);
                        mappedAddr = ((bank * 8192) + (uint)(addr & 0x1FFF)) % prgRomSize;
                    }
                    else
                    {
                        uint bank = (uint)(_prgBankReg[4] & 0x7F);
                        mappedAddr = ((bank * 8192) + (uint)(addr & 0x1FFF)) % prgRomSize;
                    }
                    break;

                case 3: // Four 8KB
                default:
                    if (addr < 0xA000)
                    {
                        bool isRam = (_prgBankReg[1] & 0x80) == 0;
                        if (isRam)
                        {
                            mappedAddr = PrgRamSentinel;
                            return true;
                        }
                        uint bank = (uint)(_prgBankReg[1] & 0x7F);
                        mappedAddr = ((bank * 8192) + (uint)(addr & 0x1FFF)) % prgRomSize;
                    }
                    else if (addr < 0xC000)
                    {
                        bool isRam = (_prgBankReg[2] & 0x80) == 0;
                        if (isRam)
                        {
                            mappedAddr = PrgRamSentinel;
                            return true;
                        }
                        uint bank = (uint)(_prgBankReg[2] & 0x7F);
                        mappedAddr = ((bank * 8192) + (uint)(addr & 0x1FFF)) % prgRomSize;
                    }
                    else if (addr < 0xE000)
                    {
                        bool isRam = (_prgBankReg[3] & 0x80) == 0;
                        if (isRam)
                        {
                            mappedAddr = PrgRamSentinel;
                            return true;
                        }
                        uint bank = (uint)(_prgBankReg[3] & 0x7F);
                        mappedAddr = ((bank * 8192) + (uint)(addr & 0x1FFF)) % prgRomSize;
                    }
                    else
                    {
                        uint bank = (uint)(_prgBankReg[4] & 0x7F);
                        mappedAddr = ((bank * 8192) + (uint)(addr & 0x1FFF)) % prgRomSize;
                    }
                    break;
            }
            return true;
        }

        mappedAddr = 0;
        return false;
    }

    public override bool CpuMapWrite(ushort addr, out uint mappedAddr, byte data)
    {
        if (addr is >= 0x5000 and <= 0x5FFF)
        {
            mappedAddr = PrgRamSentinel;

            if (addr == 0x5100) _prgMode = (byte)(data & 0x03);
            else if (addr == 0x5101) _chrMode = (byte)(data & 0x03);
            else if (addr == 0x5102) _prgRamProtect1 = (byte)(data & 0x03);
            else if (addr == 0x5103) _prgRamProtect2 = (byte)(data & 0x03);
            else if (addr == 0x5104) _exRamMode = (byte)(data & 0x03);
            else if (addr == 0x5105)
            {
                _ntMapping = data;
                byte nt0 = (byte)((data >> 0) & 0x03);
                byte nt1 = (byte)((data >> 2) & 0x03);
                byte nt2 = (byte)((data >> 4) & 0x03);
                byte nt3 = (byte)((data >> 6) & 0x03);

                if (nt0 == 0 && nt1 == 0 && nt2 == 1 && nt3 == 1)
                    _mirrorMode = MirrorMode.Horizontal;
                else if (nt0 == 0 && nt1 == 1 && nt2 == 0 && nt3 == 1)
                    _mirrorMode = MirrorMode.Vertical;
                else if (nt0 == nt1 && nt1 == nt2 && nt2 == nt3)
                    _mirrorMode = (nt0 == 0) ? MirrorMode.OneScreenLo : MirrorMode.OneScreenHi;
            }
            else if (addr == 0x5106) _fillTile = data;
            else if (addr == 0x5107) _fillColor = (byte)(data & 0x03);
            else if (addr is >= 0x5113 and <= 0x5117)
            {
                _prgBankReg[addr - 0x5113] = data;
            }
            else if (addr is >= 0x5120 and <= 0x512B)
            {
                _chrBankReg[addr - 0x5120] = (ushort)(data | (_chrUpperBits << 8));
                _lastCHRBankWriteIsUpperHalf = addr >= 0x5128;
            }
            else if (addr == 0x5130) _chrUpperBits = (byte)(data & 0x03);
            else if (addr == 0x5203)
            {
                _irqScanline = data;
                if (_irqScanline > 0 && _scanlineCounter == _irqScanline)
                    _bIRQActive = true;
            }
            else if (addr == 0x5204) _bIRQEnable = (data & 0x80) != 0;
            else if (addr == 0x5205) _multiplierA = data;
            else if (addr == 0x5206) _multiplierB = data;
            else if (addr is >= 0x5C00 and <= 0x5FFF)
            {
                if (_exRamMode <= 2)
                    ExRam[addr & 0x03FF] = data;
            }

            return true;
        }

        if (addr is >= 0x6000 and <= 0x7FFF)
        {
            if (IsPrgRamEnabled)
            {
                byte bank = (byte)(_prgBankReg[0] & 0x07);
                uint ramAddr = (uint)(bank * 8192 + (addr & 0x1FFF));
                if (ramAddr < PrgRam.Length)
                    PrgRam[ramAddr] = data;
            }
            mappedAddr = PrgRamSentinel;
            return true;
        }

        if (addr is >= 0x8000 and < 0xE000)
        {
            if (IsPrgRamEnabled)
            {
                if (_prgMode is 1 or 2)
                {
                    if (addr < 0xC000 && (_prgBankReg[2] & 0x80) == 0)
                    {
                        byte bank = (byte)(((_prgBankReg[2] & 0x06) | ((addr >> 13) & 1)) & 0x07);
                        uint ramAddr = (uint)(bank * 8192 + (addr & 0x1FFF));
                        if (ramAddr < PrgRam.Length) PrgRam[ramAddr] = data;
                        mappedAddr = PrgRamSentinel;
                        return true;
                    }
                }
                int regIndex = -1;
                if (_prgMode == 2 && addr is >= 0xC000 and < 0xE000) regIndex = 3;
                else if (_prgMode == 3)
                {
                    if (addr < 0xA000) regIndex = 1;
                    else if (addr < 0xC000) regIndex = 2;
                    else if (addr < 0xE000) regIndex = 3;
                }

                if (regIndex >= 0 && (_prgBankReg[regIndex] & 0x80) == 0)
                {
                    byte bank = (byte)(_prgBankReg[regIndex] & 0x07);
                    uint ramAddr = (uint)(bank * 8192 + (addr & 0x1FFF));
                    if (ramAddr < PrgRam.Length) PrgRam[ramAddr] = data;
                    mappedAddr = PrgRamSentinel;
                    return true;
                }
            }
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
            uint bank = 0;
            uint chrRomSize = (uint)(nCHRBanks * 8192);
            if (chrRomSize == 0) chrRomSize = 8192;

            bool bDualBankActive = _bSprite8x16Mode && _bRenderEnable;

            switch (_chrMode)
            {
                case 0: // 8KB
                    if (bDualBankActive && _bgFetchesRemaining > 0)
                    {
                        bank = _chrBankReg[11];
                        _bgFetchesRemaining--;
                    }
                    else if (!_bRenderEnable && _lastCHRBankWriteIsUpperHalf)
                    {
                        bank = _chrBankReg[11];
                    }
                    else
                    {
                        bank = _chrBankReg[7];
                        if (_bgFetchesRemaining > 0) _bgFetchesRemaining--;
                    }
                    mappedAddr = ((bank * 8192) + addr) % chrRomSize;
                    break;

                case 1: // 4KB
                    if (bDualBankActive && _bgFetchesRemaining > 0)
                    {
                        if (_exRamMode == 1)
                            bank = (uint)((_lastBgTileExRam & 0x3F) | (_chrUpperBits << 6));
                        else
                            bank = _chrBankReg[11];
                        _bgFetchesRemaining--;
                    }
                    else if (!_bRenderEnable && _lastCHRBankWriteIsUpperHalf)
                    {
                        bank = _chrBankReg[11];
                    }
                    else
                    {
                        bank = (addr < 0x1000) ? _chrBankReg[3] : _chrBankReg[7];
                        if (_bgFetchesRemaining > 0) _bgFetchesRemaining--;
                    }
                    mappedAddr = ((bank * 4096) + (uint)(addr & 0x0FFF)) % chrRomSize;
                    break;

                case 2: // 2KB
                    if (bDualBankActive && _bgFetchesRemaining > 0)
                    {
                        if (_exRamMode == 1)
                            bank = (uint)(((_lastBgTileExRam & 0x3F) | (_chrUpperBits << 6)) * 2 + ((addr >> 11) & 0x01));
                        else
                            bank = (addr < 0x0800 || (addr >= 0x1000 && addr < 0x1800)) ? _chrBankReg[9] : _chrBankReg[11];
                        _bgFetchesRemaining--;
                    }
                    else if (!_bRenderEnable && _lastCHRBankWriteIsUpperHalf)
                    {
                        bank = (addr < 0x0800 || (addr >= 0x1000 && addr < 0x1800)) ? _chrBankReg[9] : _chrBankReg[11];
                    }
                    else
                    {
                        if (addr < 0x0800) bank = _chrBankReg[1];
                        else if (addr < 0x1000) bank = _chrBankReg[3];
                        else if (addr < 0x1800) bank = _chrBankReg[5];
                        else bank = _chrBankReg[7];
                        if (_bgFetchesRemaining > 0) _bgFetchesRemaining--;
                    }
                    mappedAddr = ((bank * 2048) + (uint)(addr & 0x07FF)) % chrRomSize;
                    break;

                case 3: // 1KB
                default:
                    {
                        int bankIndex = (addr >> 10) & 0x03;
                        if (bDualBankActive && _bgFetchesRemaining > 0)
                        {
                            if (_exRamMode == 1)
                                bank = (uint)(((_lastBgTileExRam & 0x3F) | (_chrUpperBits << 6)) * 4 + bankIndex);
                            else
                                bank = _chrBankReg[8 + bankIndex];
                            _bgFetchesRemaining--;
                        }
                        else if (!_bRenderEnable && _lastCHRBankWriteIsUpperHalf)
                        {
                            bank = _chrBankReg[8 + bankIndex];
                        }
                        else
                        {
                            int wideIndex = (addr >> 10) & 0x07;
                            bank = _chrBankReg[wideIndex];
                            if (_bgFetchesRemaining > 0) _bgFetchesRemaining--;
                        }
                        mappedAddr = ((bank * 1024) + (uint)(addr & 0x03FF)) % chrRomSize;
                    }
                    break;
            }
            return true;
        }

        mappedAddr = 0;
        return false;
    }

    public override bool PpuMapWrite(ushort addr, out uint mappedAddr)
    {
        if (addr >= 0x2000 && addr <= 0x3EFF)
        {
            mappedAddr = 0;
            return true;
        }
        if (addr < 0x2000 && nCHRBanks == 0)
        {
            mappedAddr = addr;
            return true;
        }
        mappedAddr = 0;
        return false;
    }

    public override bool PpuWriteCustom(ushort addr, byte data)
    {
        if (addr is >= 0x2000 and <= 0x3EFF)
        {
            ushort tempAddr = (ushort)(addr & 0x0FFF);
            byte quadrant = (byte)((tempAddr >> 10) & 0x03);
            byte mode = (byte)((_ntMapping >> (quadrant * 2)) & 0x03);
            ushort offset = (ushort)(tempAddr & 0x03FF);

            switch (mode)
            {
                case 0: // CIRAM Page 0
                    InternalNametable[0 + offset] = data;
                    return true;
                case 1: // CIRAM Page 1
                    InternalNametable[1024 + offset] = data;
                    return true;
                case 2: // Extended RAM
                    ExRam[offset] = data;
                    return true;
                case 3: // Fill Mode
                    return true;
            }
        }
        return false;
    }

    public override bool PpuReadCustom(ushort addr, out byte data)
    {
        if (addr is >= 0x2000 and <= 0x3EFF)
        {
            if ((addr & 0x03FF) >= 0x03C0)
            {
                _bgFetchesRemaining = 2;
            }

            ushort tempAddr = (ushort)(addr & 0x0FFF);
            byte quadrant = (byte)((tempAddr >> 10) & 0x03);
            byte mode = (byte)((_ntMapping >> (quadrant * 2)) & 0x03);
            ushort offset = (ushort)(tempAddr & 0x03FF);

            if (_exRamMode == 1)
            {
                if (offset < 0x03C0)
                {
                    _lastBgTileAddr = offset;
                    _lastBgTileExRam = ExRam[offset];
                }
                else
                {
                    byte palette = (byte)(_lastBgTileExRam & 0x03);
                    data = (byte)(palette | (palette << 2) | (palette << 4) | (palette << 6));
                    return true;
                }
            }

            switch (mode)
            {
                case 0:
                    data = InternalNametable[0 + offset];
                    return true;
                case 1:
                    data = InternalNametable[1024 + offset];
                    return true;
                case 2:
                    data = (_exRamMode >= 2) ? (byte)0 : ExRam[offset];
                    return true;
                case 3:
                    if (offset >= 0x03C0)
                    {
                        byte palette = (byte)(_fillColor & 0x03);
                        data = (byte)(palette | (palette << 2) | (palette << 4) | (palette << 6));
                    }
                    else
                    {
                        data = _fillTile;
                    }
                    return true;
            }
        }

        data = 0;
        return false;
    }

    public override void Scanline() { }

    public override void Scanline(int scanline, int cycle)
    {
        // MMC5 scanline detection at cycle 4 of visible scanlines (0..239)
        if (cycle == 4 && scanline >= 0 && scanline < 240 && _bRenderEnable)
        {
            if (scanline == 0 || !_bInFrame)
            {
                _bInFrame = true;
                _scanlineCounter = 0;
                _bIRQActive = false; // Scanline 0 acknowledges IRQ
            }
            else
            {
                _scanlineCounter++;
                if (_scanlineCounter == _irqScanline && _irqScanline > 0)
                {
                    _bIRQActive = true;
                }
            }
        }

        // End of visible frame at scanline 241
        if (scanline == 241 && cycle == 1)
        {
            _bInFrame = false;
            _scanlineCounter = 0;
            _bIRQActive = false;
        }
    }
}
