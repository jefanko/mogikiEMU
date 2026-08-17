using Mogiki.Core.Common;
using Mogiki.Core.Mappers;

namespace Mogiki.Core.Cartridge;

/// <summary>
/// Represents an iNES ROM cartridge and its mapper hardware.
/// </summary>
public sealed class Cartridge
{
    public bool ImageValid { get; private set; }
    public byte MapperId { get; private set; }
    public byte PrgBanks { get; private set; }
    public byte ChrBanks { get; private set; }

    public byte[] PrgMemory { get; private set; } = [];
    public byte[] ChrMemory { get; private set; } = [];
    public byte[] PrgRam { get; } = new byte[8192];

    public Mapper? Mapper { get; private set; }

    public Cartridge(string fileName)
    {
        if (!File.Exists(fileName))
        {
            ImageValid = false;
            return;
        }

        using var stream = File.OpenRead(fileName);
        using var reader = new BinaryReader(stream);

        if (stream.Length < 16)
        {
            ImageValid = false;
            return;
        }

        byte[] headerBytes = reader.ReadBytes(16);
        if (headerBytes[0] != 'N' || headerBytes[1] != 'E' || headerBytes[2] != 'S' || headerBytes[3] != 0x1A)
        {
            ImageValid = false;
            return;
        }

        PrgBanks = headerBytes[4];
        ChrBanks = headerBytes[5];
        byte mapper1 = headerBytes[6];
        byte mapper2 = headerBytes[7];

        // Trainer (512 bytes)
        if ((mapper1 & 0x04) != 0)
        {
            reader.ReadBytes(512);
        }

        MapperId = (byte)(((mapper2 >> 4) << 4) | (mapper1 >> 4));
        var hwMirror = (mapper1 & 0x01) != 0 ? MirrorMode.Vertical : MirrorMode.Horizontal;

        PrgMemory = reader.ReadBytes(PrgBanks * 16384);

        if (ChrBanks == 0)
        {
            // CHR RAM - allocate 8KB
            ChrMemory = new byte[8192];
        }
        else
        {
            ChrMemory = reader.ReadBytes(ChrBanks * 8192);
        }

        Mapper = MapperId switch
        {
            0 => new Mapper000(PrgBanks, ChrBanks, hwMirror),
            1 => new Mapper001(PrgBanks, ChrBanks),
            2 => new Mapper002(PrgBanks, ChrBanks, hwMirror),
            3 => new Mapper003(PrgBanks, ChrBanks, hwMirror),
            4 => new Mapper004(PrgBanks, ChrBanks),
            5 => new Mapper005(PrgBanks, ChrBanks),
            69 => new Mapper069(PrgBanks, ChrBanks),
            _ => null
        };

        ImageValid = Mapper != null;
    }

    public Cartridge(byte[] prgRom, byte[] chrRom, byte mapperId, MirrorMode hwMirror = MirrorMode.Horizontal)
    {
        PrgBanks = (byte)(prgRom.Length / 16384);
        ChrBanks = (byte)(chrRom.Length / 8192);
        MapperId = mapperId;
        PrgMemory = prgRom;
        ChrMemory = chrRom.Length == 0 ? new byte[8192] : chrRom;

        Mapper = MapperId switch
        {
            0 => new Mapper000(PrgBanks, ChrBanks, hwMirror),
            1 => new Mapper001(PrgBanks, ChrBanks),
            2 => new Mapper002(PrgBanks, ChrBanks, hwMirror),
            3 => new Mapper003(PrgBanks, ChrBanks, hwMirror),
            4 => new Mapper004(PrgBanks, ChrBanks),
            5 => new Mapper005(PrgBanks, ChrBanks),
            69 => new Mapper069(PrgBanks, ChrBanks),
            _ => null
        };

        ImageValid = Mapper != null;
    }

    public void Reset()
    {
        Mapper?.Reset();
    }

    public MirrorMode Mirror => Mapper?.Mirror ?? MirrorMode.Horizontal;
    public bool IrqState => Mapper?.IrqState ?? false;
    public void ClearIrq() => Mapper?.IrqClear();
    public void Scanline() => Mapper?.Scanline();
    public void Scanline(int currentScanline, int currentCycle) => Mapper?.Scanline(currentScanline, currentCycle);
    public void CpuSnoopWrite(ushort addr, byte data) => Mapper?.CpuSnoopWrite(addr, data);
    public void CpuSnoopRead(ushort addr) => Mapper?.CpuSnoopRead(addr);

    public bool CpuRead(ushort addr, out byte data)
    {
        data = 0;
        if (Mapper == null) return false;

        if (Mapper.CpuMapRead(addr, out uint mappedAddr))
        {
            if (mappedAddr == Mapper.PrgRamSentinel)
            {
                if (Mapper is Mapper001 m1)
                {
                    data = m1.PrgRam[addr & 0x1FFF];
                    return true;
                }
                if (Mapper is Mapper004 m4)
                {
                    data = m4.PrgRam[addr & 0x1FFF];
                    return true;
                }
                if (Mapper is Mapper005 m5)
                {
                    if (addr is >= 0x5000 and <= 0x5FFF)
                        data = m5.ReadRegister(addr);
                    else
                        data = m5.ReadPrgRam(addr);
                    return true;
                }
                if (Mapper is Mapper069 m69)
                {
                    data = m69.PrgRam[addr & 0x1FFF];
                    return true;
                }
                return true;
            }

            if (mappedAddr < PrgMemory.Length)
            {
                data = PrgMemory[mappedAddr];
                return true;
            }
            return true;
        }
        if (addr is >= 0x6000 and <= 0x7FFF)
        {
            data = PrgRam[addr & 0x1FFF];
            return true;
        }

        return false;
    }

    public bool CpuWrite(ushort addr, byte data)
    {
        if (Mapper == null) return false;

        if (Mapper.CpuMapWrite(addr, out uint mappedAddr, data))
        {
            if (mappedAddr == Mapper.PrgRamSentinel)
            {
                return true;
            }
            if (mappedAddr < PrgMemory.Length)
            {
                PrgMemory[mappedAddr] = data;
                return true;
            }
            return true;
        }
        if (addr is >= 0x6000 and <= 0x7FFF)
        {
            PrgRam[addr & 0x1FFF] = data;
            return true;
        }

        return false;
    }

    public bool PpuRead(ushort addr, out byte data)
    {
        data = 0;
        if (Mapper == null) return false;

        if (Mapper.PpuReadCustom(addr, out data))
        {
            return true;
        }

        if (Mapper.PpuMapRead(addr, out uint mappedAddr))
        {
            if (mappedAddr < ChrMemory.Length)
            {
                data = ChrMemory[mappedAddr];
                return true;
            }
            return true;
        }
        return false;
    }

    public bool PpuWrite(ushort addr, byte data)
    {
        if (Mapper == null) return false;

        if (Mapper.PpuWriteCustom(addr, data))
        {
            return true;
        }

        if (Mapper.PpuMapWrite(addr, out uint mappedAddr))
        {
            if (mappedAddr < ChrMemory.Length)
            {
                ChrMemory[mappedAddr] = data;
                return true;
            }
            return true;
        }
        return false;
    }
}
