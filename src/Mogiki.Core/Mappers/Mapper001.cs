using Mogiki.Core.Common;

namespace Mogiki.Core.Mappers;

/// <summary>
/// Mapper 001: MMC1 (Nintendo SxROM).
/// </summary>
public sealed class Mapper001 : Mapper
{
    private byte _nControlRegister;
    private byte _nLoadRegister;
    private byte _nLoadRegisterCount;

    private byte _nCHRBankSelect4Lo;
    private byte _nCHRBankSelect4Hi;
    private byte _nCHRBankSelect8;

    private byte _nPRGBankSelect16Lo;
    private byte _nPRGBankSelect16Hi;
    private byte _nPRGBankSelect32;

    private MirrorMode _mirrorMode;
    public byte[] PrgRam { get; } = new byte[8192];

    public Mapper001(byte prgBanks, byte chrBanks)
        : base(prgBanks, chrBanks)
    {
        Reset();
    }

    public override void Reset()
    {
        _nControlRegister = 0x1C;
        _nLoadRegister = 0x00;
        _nLoadRegisterCount = 0;

        _nCHRBankSelect4Lo = 0;
        _nCHRBankSelect4Hi = 0;
        _nCHRBankSelect8 = 0;

        _nPRGBankSelect16Lo = 0;
        _nPRGBankSelect16Hi = (byte)(nPRGBanks - 1);
        _nPRGBankSelect32 = 0;

        _mirrorMode = MirrorMode.Horizontal;
    }

    public override MirrorMode Mirror => _mirrorMode;

    public override bool CpuMapRead(ushort addr, out uint mappedAddr)
    {
        if (addr is >= 0x6000 and <= 0x7FFF)
        {
            mappedAddr = PrgRamSentinel;
            return true;
        }

        if (addr >= 0x8000)
        {
            if ((_nControlRegister & 0x08) != 0)
            {
                // 16KB PRG mode
                if (addr <= 0xBFFF)
                {
                    mappedAddr = (uint)(_nPRGBankSelect16Lo * 0x4000 + (addr & 0x3FFF));
                    return true;
                }
                else
                {
                    mappedAddr = (uint)(_nPRGBankSelect16Hi * 0x4000 + (addr & 0x3FFF));
                    return true;
                }
            }
            else
            {
                // 32KB PRG mode
                mappedAddr = (uint)(_nPRGBankSelect32 * 0x8000 + (addr & 0x7FFF));
                return true;
            }
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

        if (addr >= 0x8000)
        {
            mappedAddr = 0;
            if ((data & 0x80) != 0)
            {
                // Reset shift register
                _nLoadRegister = 0x00;
                _nLoadRegisterCount = 0;
                _nControlRegister |= 0x0C;
            }
            else
            {
                _nLoadRegister >>= 1;
                _nLoadRegister |= (byte)((data & 0x01) << 4);
                _nLoadRegisterCount++;

                if (_nLoadRegisterCount == 5)
                {
                    byte targetReg = (byte)((addr >> 13) & 0x03);

                    if (targetReg == 0)
                    {
                        // Control register (0x8000-0x9FFF)
                        _nControlRegister = (byte)(_nLoadRegister & 0x1F);

                        _mirrorMode = (_nControlRegister & 0x03) switch
                        {
                            0 => MirrorMode.OneScreenLo,
                            1 => MirrorMode.OneScreenHi,
                            2 => MirrorMode.Vertical,
                            _ => MirrorMode.Horizontal
                        };
                    }
                    else if (targetReg == 1)
                    {
                        // CHR bank 0 (0xA000-0xBFFF)
                        if ((_nControlRegister & 0x10) != 0)
                            _nCHRBankSelect4Lo = (byte)(_nLoadRegister & 0x1F);
                        else
                            _nCHRBankSelect8 = (byte)(_nLoadRegister & 0x1E);
                    }
                    else if (targetReg == 2)
                    {
                        // CHR bank 1 (0xC000-0xDFFF)
                        if ((_nControlRegister & 0x10) != 0)
                            _nCHRBankSelect4Hi = (byte)(_nLoadRegister & 0x1F);
                    }
                    else if (targetReg == 3)
                    {
                        // PRG bank (0xE000-0xFFFF)
                        byte prgMode = (byte)((_nControlRegister >> 2) & 0x03);

                        if (prgMode is 0 or 1)
                        {
                            _nPRGBankSelect32 = (byte)((_nLoadRegister & 0x0E) >> 1);
                        }
                        else if (prgMode == 2)
                        {
                            _nPRGBankSelect16Lo = 0;
                            _nPRGBankSelect16Hi = (byte)(_nLoadRegister & 0x0F);
                        }
                        else if (prgMode == 3)
                        {
                            _nPRGBankSelect16Lo = (byte)(_nLoadRegister & 0x0F);
                            _nPRGBankSelect16Hi = (byte)(nPRGBanks - 1);
                        }
                    }

                    _nLoadRegister = 0x00;
                    _nLoadRegisterCount = 0;
                }
            }
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
            if (nCHRBanks == 0)
            {
                mappedAddr = addr;
                return true;
            }

            if ((_nControlRegister & 0x10) != 0)
            {
                // 4KB CHR mode
                if (addr <= 0x0FFF)
                    mappedAddr = (uint)(_nCHRBankSelect4Lo * 0x1000 + (addr & 0x0FFF));
                else
                    mappedAddr = (uint)(_nCHRBankSelect4Hi * 0x1000 + (addr & 0x0FFF));
                return true;
            }
            else
            {
                // 8KB CHR mode
                mappedAddr = (uint)(_nCHRBankSelect8 * 0x1000 + addr);
                return true;
            }
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
