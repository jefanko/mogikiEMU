using System.Runtime.CompilerServices;
using Mogiki.Core.Cartridge;
using Mogiki.Core.Common;

namespace Mogiki.Core.Ppu;

/// <summary>
/// Ricoh 2C02 PPU (Picture Processing Unit) for NES.
/// </summary>
public sealed class Ppu2C02
{
    public Cartridge.Cartridge? Cartridge { get; set; }

    public bool Nmi { get; set; }
    public bool FrameComplete { get; set; }

    // Screen buffer (256x240 pixels)
    public Pixel[] Screen { get; } = new Pixel[256 * 240];
    public uint[] ScreenArgb { get; } = new uint[256 * 240];

    // OAM (Object Attribute Memory)
    public byte[] OAM { get; } = new byte[256];
    private byte _oamAddr;

    // VRAM
    private readonly byte[,] _tblName = new byte[2, 1024];
    private readonly byte[] _tblPalette = new byte[32];
    private readonly Pixel[] _palScreen = new Pixel[0x40];
    private readonly uint[] _palScreenArgb = new uint[0x40];

    // Registers
    public byte Status;
    public byte Control;
    public byte Mask;

    public LoopyRegister VramAddr;
    public LoopyRegister TramAddr;

    private byte _fineX;
    private byte _addressLatch;
    private byte _ppuDataBuffer;

    private short _scanline;
    private short _cycle;

    // Background Shifters
    private byte _bgNextTileId;
    private byte _bgNextTileAttrib;
    private byte _bgNextTileLsb;
    private byte _bgNextTileMsb;
    private ushort _bgShifterPatternLo;
    private ushort _bgShifterPatternHi;
    private ushort _bgShifterAttribLo;
    private ushort _bgShifterAttribHi;

    // Sprite Shifters
    public struct ObjectAttributeEntry
    {
        public byte Y;
        public byte Id;
        public byte Attribute;
        public byte X;
    }

    private byte _spriteCount;
    private readonly ObjectAttributeEntry[] _spriteScanline = new ObjectAttributeEntry[8];
    private readonly byte[] _spriteShifterPatternLo = new byte[8];
    private readonly byte[] _spriteShifterPatternHi = new byte[8];

    private bool _bSpriteZeroHitPossible;
    private bool _bSpriteZeroBeingRendered;
    private bool _oddFrame;

    public Ppu2C02()
    {
        InitializePalette();
        Reset();
    }

    private void InitializePalette()
    {
        _palScreen[0x00] = new(84, 84, 84);
        _palScreen[0x01] = new(0, 30, 116);
        _palScreen[0x02] = new(8, 16, 144);
        _palScreen[0x03] = new(48, 0, 136);
        _palScreen[0x04] = new(68, 0, 100);
        _palScreen[0x05] = new(92, 0, 48);
        _palScreen[0x06] = new(84, 4, 0);
        _palScreen[0x07] = new(60, 24, 0);
        _palScreen[0x08] = new(32, 42, 0);
        _palScreen[0x09] = new(8, 58, 0);
        _palScreen[0x0A] = new(0, 64, 0);
        _palScreen[0x0B] = new(0, 60, 0);
        _palScreen[0x0C] = new(0, 50, 60);
        _palScreen[0x0D] = new(0, 0, 0);
        _palScreen[0x0E] = new(0, 0, 0);
        _palScreen[0x0F] = new(0, 0, 0);

        _palScreen[0x10] = new(152, 150, 152);
        _palScreen[0x11] = new(8, 76, 196);
        _palScreen[0x12] = new(48, 50, 236);
        _palScreen[0x13] = new(92, 30, 228);
        _palScreen[0x14] = new(136, 20, 176);
        _palScreen[0x15] = new(160, 20, 100);
        _palScreen[0x16] = new(152, 34, 32);
        _palScreen[0x17] = new(120, 60, 0);
        _palScreen[0x18] = new(84, 90, 0);
        _palScreen[0x19] = new(40, 114, 0);
        _palScreen[0x1A] = new(8, 124, 0);
        _palScreen[0x1B] = new(0, 118, 40);
        _palScreen[0x1C] = new(0, 102, 120);
        _palScreen[0x1D] = new(0, 0, 0);
        _palScreen[0x1E] = new(0, 0, 0);
        _palScreen[0x1F] = new(0, 0, 0);

        _palScreen[0x20] = new(236, 238, 236);
        _palScreen[0x21] = new(76, 154, 236);
        _palScreen[0x22] = new(120, 124, 236);
        _palScreen[0x23] = new(176, 98, 236);
        _palScreen[0x24] = new(228, 84, 236);
        _palScreen[0x25] = new(236, 88, 180);
        _palScreen[0x26] = new(236, 106, 100);
        _palScreen[0x27] = new(212, 136, 32);
        _palScreen[0x28] = new(160, 170, 0);
        _palScreen[0x29] = new(116, 196, 0);
        _palScreen[0x2A] = new(76, 208, 32);
        _palScreen[0x2B] = new(56, 204, 108);
        _palScreen[0x2C] = new(56, 180, 204);
        _palScreen[0x2D] = new(60, 60, 60);
        _palScreen[0x2E] = new(0, 0, 0);
        _palScreen[0x2F] = new(0, 0, 0);

        _palScreen[0x30] = new(236, 238, 236);
        _palScreen[0x31] = new(168, 204, 236);
        _palScreen[0x32] = new(188, 188, 236);
        _palScreen[0x33] = new(212, 178, 236);
        _palScreen[0x34] = new(236, 174, 236);
        _palScreen[0x35] = new(236, 174, 212);
        _palScreen[0x36] = new(236, 180, 176);
        _palScreen[0x37] = new(228, 196, 144);
        _palScreen[0x38] = new(204, 210, 120);
        _palScreen[0x39] = new(180, 222, 120);
        _palScreen[0x3A] = new(168, 226, 144);
        _palScreen[0x3B] = new(152, 226, 180);
        _palScreen[0x3C] = new(160, 214, 228);
        _palScreen[0x3D] = new(160, 162, 160);
        _palScreen[0x3E] = new(0, 0, 0);
        _palScreen[0x3F] = new(0, 0, 0);

        for (int i = 0; i < 0x40; i++)
        {
            _palScreenArgb[i] = (uint)((255 << 24) | (_palScreen[i].R << 16) | (_palScreen[i].G << 8) | _palScreen[i].B);
        }
    }

    public void Reset()
    {
        _fineX = 0;
        _addressLatch = 0;
        _ppuDataBuffer = 0;
        _scanline = 0;
        _cycle = 0;

        _bgNextTileId = 0;
        _bgNextTileAttrib = 0;
        _bgNextTileLsb = 0;
        _bgNextTileMsb = 0;
        _bgShifterPatternLo = 0;
        _bgShifterPatternHi = 0;
        _bgShifterAttribLo = 0;
        _bgShifterAttribHi = 0;

        Status = 0;
        Mask = 0;
        Control = 0;
        VramAddr.Reg = 0;
        TramAddr.Reg = 0;
        _oddFrame = false;
    }

    public Pixel GetColorFromPaletteRam(byte palette, byte pixel)
    {
        return _palScreen[PpuRead((ushort)(0x3F00 + (palette << 2) + pixel)) & 0x3F];
    }

    public byte CpuRead(ushort addr, bool readOnly = false)
    {
        byte data = 0x00;
        if (readOnly)
        {
            switch (addr)
            {
                case 0x0000: data = Control; break;
                case 0x0001: data = Mask; break;
                case 0x0002: data = Status; break;
            }
        }
        else
        {
            switch (addr)
            {
                case 0x0002: // PPUSTATUS
                    data = (byte)((Status & 0xE0) | (_ppuDataBuffer & 0x1F));
                    Status = (byte)(Status & ~0x80); // Clear vertical blank
                    _addressLatch = 0;
                    break;

                case 0x0004: // OAMDATA
                    data = OAM[_oamAddr];
                    break;

                case 0x0007: // PPUDATA
                    data = _ppuDataBuffer;
                    _ppuDataBuffer = PpuRead(VramAddr.Reg);
                    if (VramAddr.Reg >= 0x3F00)
                        data = _ppuDataBuffer;
                    VramAddr.Reg += (ushort)((Control & 0x04) != 0 ? 32 : 1);
                    break;
            }
        }
        return data;
    }

    public void CpuWrite(ushort addr, byte data)
    {
        switch (addr)
        {
            case 0x0000: // PPUCTRL
                Control = data;
                TramAddr.NametableX = (byte)(Control & 0x01);
                TramAddr.NametableY = (byte)((Control >> 1) & 0x01);
                break;

            case 0x0001: // PPUMASK
                Mask = data;
                break;

            case 0x0003: // OAMADDR
                _oamAddr = data;
                break;

            case 0x0004: // OAMDATA
                OAM[_oamAddr] = data;
                break;

            case 0x0005: // PPUSCROLL
                if (_addressLatch == 0)
                {
                    _fineX = (byte)(data & 0x07);
                    TramAddr.CoarseX = (byte)(data >> 3);
                    _addressLatch = 1;
                }
                else
                {
                    TramAddr.FineY = (byte)(data & 0x07);
                    TramAddr.CoarseY = (byte)(data >> 3);
                    _addressLatch = 0;
                }
                break;

            case 0x0006: // PPUADDR
                if (_addressLatch == 0)
                {
                    TramAddr.Reg = (ushort)(((data & 0x3F) << 8) | (TramAddr.Reg & 0x00FF));
                    _addressLatch = 1;
                }
                else
                {
                    TramAddr.Reg = (ushort)((TramAddr.Reg & 0xFF00) | data);
                    VramAddr = TramAddr;
                    _addressLatch = 0;
                }
                break;

            case 0x0007: // PPUDATA
                PpuWrite(VramAddr.Reg, data);
                VramAddr.Reg += (ushort)((Control & 0x04) != 0 ? 32 : 1);
                break;
        }
    }

    public byte PpuRead(ushort addr)
    {
        byte data = 0;
        addr &= 0x3FFF;

        if (Cartridge != null && Cartridge.PpuRead(addr, out data))
        {
            return data;
        }

        if (addr is >= 0x2000 and <= 0x3EFF)
        {
            addr &= 0x0FFF;
            MirrorMode mirror = Cartridge?.Mirror ?? MirrorMode.Horizontal;

            if (mirror == MirrorMode.Vertical)
            {
                if (addr is >= 0x0000 and <= 0x03FF) data = _tblName[0, addr & 0x03FF];
                else if (addr is >= 0x0400 and <= 0x07FF) data = _tblName[1, addr & 0x03FF];
                else if (addr is >= 0x0800 and <= 0x0BFF) data = _tblName[0, addr & 0x03FF];
                else if (addr is >= 0x0C00 and <= 0x0FFF) data = _tblName[1, addr & 0x03FF];
            }
            else if (mirror == MirrorMode.Horizontal)
            {
                if (addr is >= 0x0000 and <= 0x03FF) data = _tblName[0, addr & 0x03FF];
                else if (addr is >= 0x0400 and <= 0x07FF) data = _tblName[0, addr & 0x03FF];
                else if (addr is >= 0x0800 and <= 0x0BFF) data = _tblName[1, addr & 0x03FF];
                else if (addr is >= 0x0C00 and <= 0x0FFF) data = _tblName[1, addr & 0x03FF];
            }
            else if (mirror == MirrorMode.OneScreenLo)
            {
                data = _tblName[0, addr & 0x03FF];
            }
            else if (mirror == MirrorMode.OneScreenHi)
            {
                data = _tblName[1, addr & 0x03FF];
            }
        }
        else if (addr is >= 0x3F00 and <= 0x3FFF)
        {
            addr &= 0x001F;
            if (addr == 0x0010) addr = 0x0000;
            if (addr == 0x0014) addr = 0x0004;
            if (addr == 0x0018) addr = 0x0008;
            if (addr == 0x001C) addr = 0x000C;
            data = (byte)(_tblPalette[addr] & ((Mask & 0x01) != 0 ? 0x30 : 0x3F));
        }

        return data;
    }

    public void PpuWrite(ushort addr, byte data)
    {
        addr &= 0x3FFF;

        if (Cartridge != null && Cartridge.PpuWrite(addr, data))
        {
            return;
        }

        if (addr is >= 0x2000 and <= 0x3EFF)
        {
            addr &= 0x0FFF;
            MirrorMode mirror = Cartridge?.Mirror ?? MirrorMode.Horizontal;

            if (mirror == MirrorMode.Vertical)
            {
                if (addr is >= 0x0000 and <= 0x03FF) _tblName[0, addr & 0x03FF] = data;
                else if (addr is >= 0x0400 and <= 0x07FF) _tblName[1, addr & 0x03FF] = data;
                else if (addr is >= 0x0800 and <= 0x0BFF) _tblName[0, addr & 0x03FF] = data;
                else if (addr is >= 0x0C00 and <= 0x0FFF) _tblName[1, addr & 0x03FF] = data;
            }
            else if (mirror == MirrorMode.Horizontal)
            {
                if (addr is >= 0x0000 and <= 0x03FF) _tblName[0, addr & 0x03FF] = data;
                else if (addr is >= 0x0400 and <= 0x07FF) _tblName[0, addr & 0x03FF] = data;
                else if (addr is >= 0x0800 and <= 0x0BFF) _tblName[1, addr & 0x03FF] = data;
                else if (addr is >= 0x0C00 and <= 0x0FFF) _tblName[1, addr & 0x03FF] = data;
            }
            else if (mirror == MirrorMode.OneScreenLo)
            {
                _tblName[0, addr & 0x03FF] = data;
            }
            else if (mirror == MirrorMode.OneScreenHi)
            {
                _tblName[1, addr & 0x03FF] = data;
            }
        }
        else if (addr is >= 0x3F00 and <= 0x3FFF)
        {
            addr &= 0x001F;
            if (addr == 0x0010) addr = 0x0000;
            if (addr == 0x0014) addr = 0x0004;
            if (addr == 0x0018) addr = 0x0008;
            if (addr == 0x001C) addr = 0x000C;
            _tblPalette[addr] = data;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IncrementScrollX()
    {
        if ((Mask & 0x18) != 0) // render_background || render_sprites
        {
            if (VramAddr.CoarseX == 31)
            {
                VramAddr.CoarseX = 0;
                VramAddr.NametableX = (byte)(~VramAddr.NametableX & 0x01);
            }
            else
            {
                VramAddr.CoarseX++;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IncrementScrollY()
    {
        if ((Mask & 0x18) != 0) // render_background || render_sprites
        {
            if (VramAddr.FineY < 7)
            {
                VramAddr.FineY++;
            }
            else
            {
                VramAddr.FineY = 0;
                if (VramAddr.CoarseY == 29)
                {
                    VramAddr.CoarseY = 0;
                    VramAddr.NametableY = (byte)(~VramAddr.NametableY & 0x01);
                }
                else if (VramAddr.CoarseY == 31)
                {
                    VramAddr.CoarseY = 0;
                }
                else
                {
                    VramAddr.CoarseY++;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TransferAddressX()
    {
        if ((Mask & 0x18) != 0)
        {
            VramAddr.NametableX = TramAddr.NametableX;
            VramAddr.CoarseX = TramAddr.CoarseX;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TransferAddressY()
    {
        if ((Mask & 0x18) != 0)
        {
            VramAddr.FineY = TramAddr.FineY;
            VramAddr.NametableY = TramAddr.NametableY;
            VramAddr.CoarseY = TramAddr.CoarseY;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LoadBackgroundShifters()
    {
        _bgShifterPatternLo = (ushort)((_bgShifterPatternLo & 0xFF00) | _bgNextTileLsb);
        _bgShifterPatternHi = (ushort)((_bgShifterPatternHi & 0xFF00) | _bgNextTileMsb);
        _bgShifterAttribLo = (ushort)((_bgShifterAttribLo & 0xFF00) | ((_bgNextTileAttrib & 0b01) != 0 ? 0xFF : 0x00));
        _bgShifterAttribHi = (ushort)((_bgShifterAttribHi & 0xFF00) | ((_bgNextTileAttrib & 0b10) != 0 ? 0xFF : 0x00));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateShifters()
    {
        if ((Mask & 0x08) != 0) // render_background
        {
            _bgShifterPatternLo <<= 1;
            _bgShifterPatternHi <<= 1;
            _bgShifterAttribLo <<= 1;
            _bgShifterAttribHi <<= 1;
        }

        if ((Mask & 0x10) != 0 && _cycle is >= 1 and < 258) // render_sprites
        {
            for (int i = 0; i < _spriteCount; i++)
            {
                if (_spriteScanline[i].X > 0)
                {
                    _spriteScanline[i].X--;
                }
                else
                {
                    _spriteShifterPatternLo[i] <<= 1;
                    _spriteShifterPatternHi[i] <<= 1;
                }
            }
        }
    }

    public void Clock()
    {
        bool renderBg = (Mask & 0x08) != 0;
        bool renderSpr = (Mask & 0x10) != 0;
        bool renderAny = renderBg || renderSpr;

        if (_scanline is >= -1 and < 240)
        {
            // Odd frame cycle skip on scanline 0
            if (_scanline == 0 && _cycle == 0 && _oddFrame && renderAny)
            {
                _cycle = 1;
            }

            if (_scanline == -1 && _cycle == 1)
            {
                Status = (byte)(Status & ~0xE0); // Clear vblank, sprite 0 hit, overflow
                Array.Clear(_spriteShifterPatternLo, 0, 8);
                Array.Clear(_spriteShifterPatternHi, 0, 8);
            }

            if ((_cycle is >= 2 and < 258) || (_cycle is >= 321 and < 338))
            {
                UpdateShifters();

                switch ((_cycle - 1) % 8)
                {
                    case 0:
                        LoadBackgroundShifters();
                        _bgNextTileId = PpuRead((ushort)(0x2000 | (VramAddr.Reg & 0x0FFF)));
                        break;
                    case 2:
                        _bgNextTileAttrib = PpuRead((ushort)(0x23C0 | (VramAddr.NametableY << 11) |
                                                              (VramAddr.NametableX << 10) |
                                                              ((VramAddr.CoarseY >> 2) << 3) |
                                                              (VramAddr.CoarseX >> 2)));
                        if ((VramAddr.CoarseY & 0x02) != 0) _bgNextTileAttrib >>= 4;
                        if ((VramAddr.CoarseX & 0x02) != 0) _bgNextTileAttrib >>= 2;
                        _bgNextTileAttrib &= 0x03;
                        break;
                    case 4:
                        _bgNextTileLsb = PpuRead((ushort)((((Control >> 4) & 0x01) << 12) +
                                                          ((ushort)_bgNextTileId << 4) + VramAddr.FineY + 0));
                        break;
                    case 6:
                        _bgNextTileMsb = PpuRead((ushort)((((Control >> 4) & 0x01) << 12) +
                                                          ((ushort)_bgNextTileId << 4) + VramAddr.FineY + 8));
                        break;
                    case 7:
                        IncrementScrollX();
                        break;
                }
            }

            if (_cycle == 256)
            {
                IncrementScrollY();
            }

            if (_cycle == 257)
            {
                LoadBackgroundShifters();
                TransferAddressX();
            }

            // MMC5 scanline detection at cycle 4
            if (_cycle == 4 && _scanline is >= 0 and < 240)
            {
                Cartridge?.Scanline(_scanline, _cycle);
            }

            // MMC3 scanline clock at cycle 260 / 324
            if (_scanline is >= -1 and < 240 && renderAny)
            {
                int clockCycle = ((Control >> 4) & 0x01) == 1 ? 324 : 260;
                if (_cycle == clockCycle)
                {
                    Cartridge?.Scanline();
                }
            }

            if (_scanline == -1 && _cycle is >= 280 and < 305)
            {
                TransferAddressY();
            }

            // Sprite Evaluation at cycle 257
            if (_cycle == 257 && _scanline >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    _spriteScanline[i] = new ObjectAttributeEntry { Y = 0xFF, Id = 0xFF, Attribute = 0xFF, X = 0xFF };
                    _spriteShifterPatternLo[i] = 0;
                    _spriteShifterPatternHi[i] = 0;
                }
                _spriteCount = 0;
                _bSpriteZeroHitPossible = false;

                byte spriteSize = (byte)((Control & 0x20) != 0 ? 16 : 8);
                byte nOAMEntry = 0;

                while (nOAMEntry < 64 && _spriteCount < 9)
                {
                    short diff = (short)(_scanline - OAM[nOAMEntry * 4 + 0]);
                    if (diff >= 0 && diff < spriteSize)
                    {
                        if (_spriteCount < 8)
                        {
                            if (nOAMEntry == 0)
                                _bSpriteZeroHitPossible = true;

                            _spriteScanline[_spriteCount] = new ObjectAttributeEntry
                            {
                                Y = OAM[nOAMEntry * 4 + 0],
                                Id = OAM[nOAMEntry * 4 + 1],
                                Attribute = OAM[nOAMEntry * 4 + 2],
                                X = OAM[nOAMEntry * 4 + 3]
                            };
                            _spriteCount++;
                        }
                    }
                    nOAMEntry++;
                }

                if (_spriteCount > 8) Status |= 0x20; // sprite_overflow
            }

            // Sprite pattern fetch at cycle 340
            if (_cycle == 340)
            {
                bool is8x16 = (Control & 0x20) != 0;
                for (byte i = 0; i < _spriteCount; i++)
                {
                    byte spritePatternBitsLo, spritePatternBitsHi;
                    ushort spritePatternAddrLo;

                    if (!is8x16)
                    {
                        // 8x8 Sprite
                        byte patternTable = (byte)((Control >> 3) & 0x01);
                        if ((_spriteScanline[i].Attribute & 0x80) == 0) // No vertical flip
                        {
                            spritePatternAddrLo = (ushort)((patternTable << 12) |
                                                          (_spriteScanline[i].Id << 4) |
                                                          (_scanline - _spriteScanline[i].Y));
                        }
                        else
                        {
                            spritePatternAddrLo = (ushort)((patternTable << 12) |
                                                          (_spriteScanline[i].Id << 4) |
                                                          (7 - (_scanline - _spriteScanline[i].Y)));
                        }
                    }
                    else
                    {
                        // 8x16 Sprite
                        byte patternTable = (byte)(_spriteScanline[i].Id & 0x01);
                        byte tileId = (byte)(_spriteScanline[i].Id & 0xFE);
                        short yOffset = (short)(_scanline - _spriteScanline[i].Y);

                        if ((_spriteScanline[i].Attribute & 0x80) == 0) // No vertical flip
                        {
                            if (yOffset < 8)
                                spritePatternAddrLo = (ushort)((patternTable << 12) | (tileId << 4) | (yOffset & 0x07));
                            else
                                spritePatternAddrLo = (ushort)((patternTable << 12) | ((tileId + 1) << 4) | (yOffset & 0x07));
                        }
                        else
                        {
                            if (yOffset < 8)
                                spritePatternAddrLo = (ushort)((patternTable << 12) | ((tileId + 1) << 4) | ((7 - yOffset) & 0x07));
                            else
                                spritePatternAddrLo = (ushort)((patternTable << 12) | (tileId << 4) | ((7 - yOffset) & 0x07));
                        }
                    }

                    ushort spritePatternAddrHi = (ushort)(spritePatternAddrLo + 8);
                    spritePatternBitsLo = PpuRead(spritePatternAddrLo);
                    spritePatternBitsHi = PpuRead(spritePatternAddrHi);

                    if ((_spriteScanline[i].Attribute & 0x40) != 0) // Horizontal flip
                    {
                        static byte FlipByte(byte b)
                        {
                            b = (byte)(((b & 0xF0) >> 4) | ((b & 0x0F) << 4));
                            b = (byte)(((b & 0xCC) >> 2) | ((b & 0x33) << 2));
                            b = (byte)(((b & 0xAA) >> 1) | ((b & 0x55) << 1));
                            return b;
                        }
                        spritePatternBitsLo = FlipByte(spritePatternBitsLo);
                        spritePatternBitsHi = FlipByte(spritePatternBitsHi);
                    }

                    _spriteShifterPatternLo[i] = spritePatternBitsLo;
                    _spriteShifterPatternHi[i] = spritePatternBitsHi;
                }
            }
        }

        // VBlank Entry at scanline 241
        if (_scanline == 241 && _cycle == 1)
        {
            Status |= 0x80; // vertical_blank
            if ((Control & 0x80) != 0) // enable_nmi
                Nmi = true;
            Cartridge?.Scanline(_scanline, _cycle);
        }

        // Pixel Composition
        byte bgPixel = 0x00;
        byte bgPalette = 0x00;

        if (renderBg)
        {
            if ((Mask & 0x02) != 0 || _cycle > 8) // render_background_left
            {
                ushort bitMux = (ushort)(0x8000 >> _fineX);
                byte p0Pixel = (byte)((_bgShifterPatternLo & bitMux) != 0 ? 1 : 0);
                byte p1Pixel = (byte)((_bgShifterPatternHi & bitMux) != 0 ? 1 : 0);
                bgPixel = (byte)((p1Pixel << 1) | p0Pixel);

                byte bgPal0 = (byte)((_bgShifterAttribLo & bitMux) != 0 ? 1 : 0);
                byte bgPal1 = (byte)((_bgShifterAttribHi & bitMux) != 0 ? 1 : 0);
                bgPalette = (byte)((bgPal1 << 1) | bgPal0);
            }
        }

        byte fgPixel = 0x00;
        byte fgPalette = 0x00;
        bool fgPriority = false;

        if (renderSpr)
        {
            _bSpriteZeroBeingRendered = false;

            if ((Mask & 0x04) != 0 || _cycle > 8) // render_sprites_left
            {
                for (byte i = 0; i < _spriteCount; i++)
                {
                    if (_spriteScanline[i].X == 0)
                    {
                        byte fgPixelLo = (byte)((_spriteShifterPatternLo[i] & 0x80) != 0 ? 1 : 0);
                        byte fgPixelHi = (byte)((_spriteShifterPatternHi[i] & 0x80) != 0 ? 1 : 0);
                        fgPixel = (byte)((fgPixelHi << 1) | fgPixelLo);

                        fgPalette = (byte)((_spriteScanline[i].Attribute & 0x03) + 0x04);
                        fgPriority = (_spriteScanline[i].Attribute & 0x20) == 0;

                        if (fgPixel != 0)
                        {
                            if (i == 0)
                                _bSpriteZeroBeingRendered = true;
                            break;
                        }
                    }
                }
            }
        }

        byte pixel = 0x00;
        byte palette = 0x00;

        if (bgPixel == 0 && fgPixel == 0)
        {
            pixel = 0;
            palette = 0;
        }
        else if (bgPixel == 0 && fgPixel > 0)
        {
            pixel = fgPixel;
            palette = fgPalette;
        }
        else if (bgPixel > 0 && fgPixel == 0)
        {
            pixel = bgPixel;
            palette = bgPalette;
        }
        else if (bgPixel > 0 && fgPixel > 0)
        {
            if (fgPriority)
            {
                pixel = fgPixel;
                palette = fgPalette;
            }
            else
            {
                pixel = bgPixel;
                palette = bgPalette;
            }

            // Sprite 0 Hit detection
            if (_bSpriteZeroHitPossible && _bSpriteZeroBeingRendered)
            {
                if (renderBg && renderSpr)
                {
                    if ((Mask & 0x06) != 0x06) // Left clipping active
                    {
                        if (_cycle is >= 9 and < 256)
                            Status |= 0x40; // sprite_zero_hit
                    }
                    else
                    {
                        if (_cycle is >= 1 and < 256)
                            Status |= 0x40; // sprite_zero_hit
                    }
                }
            }
        }

        if (_scanline is >= 0 and < 240 && _cycle is >= 1 and <= 256)
        {
            byte palIdx = (byte)(PpuRead((ushort)(0x3F00 + (palette << 2) + pixel)) & 0x3F);
            int idx = _scanline * 256 + (_cycle - 1);
            ScreenArgb[idx] = _palScreenArgb[palIdx];
            Screen[idx] = _palScreen[palIdx];
        }

        _cycle++;
        if (_cycle >= 341)
        {
            _cycle = 0;
            _scanline++;
            if (_scanline >= 261)
            {
                _scanline = -1;
                FrameComplete = true;
                _oddFrame = !_oddFrame;
            }
        }
    }
}
