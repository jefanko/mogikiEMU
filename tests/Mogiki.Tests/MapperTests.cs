using Mogiki.Core.Common;
using Mogiki.Core.Mappers;
using Xunit;

namespace Mogiki.Tests;

public class MapperTests
{
    [Fact]
    public void Mapper000_NROM_Mirrors16KBPRGCorrectly()
    {
        var mapper = new Mapper000(1, 1, MirrorMode.Horizontal);

        // $8000 -> 0x0000
        Assert.True(mapper.CpuMapRead(0x8000, out uint mappedAddr1));
        Assert.Equal(0x0000u, mappedAddr1);

        // $C000 -> 0x0000 (mirrored for 16KB PRG ROM)
        Assert.True(mapper.CpuMapRead(0xC000, out uint mappedAddr2));
        Assert.Equal(0x0000u, mappedAddr2);
    }

    [Fact]
    public void Mapper001_MMC1_ShiftRegisterWritesUpdateControl()
    {
        var mapper = new Mapper001(2, 1);

        // Write 5 bits to $8000 (control reg): 0b00010 (Vertical Mirroring)
        // Bit 0 = 0
        mapper.CpuMapWrite(0x8000, out _, 0x00);
        // Bit 1 = 1
        mapper.CpuMapWrite(0x8000, out _, 0x01);
        // Bit 2 = 0
        mapper.CpuMapWrite(0x8000, out _, 0x00);
        // Bit 3 = 0
        mapper.CpuMapWrite(0x8000, out _, 0x00);
        // Bit 4 = 0 (5th write commits)
        mapper.CpuMapWrite(0x8000, out _, 0x00);

        Assert.Equal(MirrorMode.Vertical, mapper.Mirror);
    }

    [Fact]
    public void Mapper004_MMC3_ScanlineCounter_TriggersIRQ()
    {
        var mapper = new Mapper004(4, 4);

        // Set IRQ Latch to 3 ($C000)
        mapper.CpuMapWrite(0xC000, out _, 3);

        // Reload IRQ Counter ($C001)
        mapper.CpuMapWrite(0xC001, out _, 0);

        // Enable IRQ ($E001)
        mapper.CpuMapWrite(0xE001, out _, 0);

        Assert.False(mapper.IrqState);

        // Scanline 1: counter reloads to 3
        mapper.Scanline();
        Assert.False(mapper.IrqState);

        // Scanline 2: counter = 2
        mapper.Scanline();
        Assert.False(mapper.IrqState);

        // Scanline 3: counter = 1
        mapper.Scanline();
        Assert.False(mapper.IrqState);

        // Scanline 4: counter = 0 -> IRQ fires!
        mapper.Scanline();
        Assert.True(mapper.IrqState);

        // IRQ Clear
        mapper.IrqClear();
        Assert.False(mapper.IrqState);
    }

    [Fact]
    public void Mapper005_MMC5_HardwareMultiplier_CalculatesCorrectly()
    {
        var mapper = new Mapper005(4, 4);

        // Multiplier A = 0x12 (18), Multiplier B = 0x34 (52)
        // Product = 18 * 52 = 936 = 0x03A8
        mapper.CpuMapWrite(0x5205, out _, 0x12);
        mapper.CpuMapWrite(0x5206, out _, 0x34);

        byte lo = mapper.ReadRegister(0x5205);
        byte hi = mapper.ReadRegister(0x5206);

        Assert.Equal(0xA8, lo);
        Assert.Equal(0x03, hi);
    }

    [Fact]
    public void Mapper069_FME7_TimerIRQ_DecrementsAndFires()
    {
        var mapper = new Mapper069(4, 4);

        // Command 0xD: IRQ Control (enable IRQ bit 0 and counter bit 7 -> 0x81)
        mapper.CpuMapWrite(0x8000, out _, 0x0D);
        mapper.CpuMapWrite(0xA000, out _, 0x81);

        // Command 0xE: IRQ Counter Low = 2
        mapper.CpuMapWrite(0x8000, out _, 0x0E);
        mapper.CpuMapWrite(0xA000, out _, 0x02);

        // Command 0xF: IRQ Counter High = 0
        mapper.CpuMapWrite(0x8000, out _, 0x0F);
        mapper.CpuMapWrite(0xA000, out _, 0x00);

        Assert.False(mapper.IrqState);

        // Count 1: counter = 1
        mapper.CountIRQ();
        Assert.False(mapper.IrqState);

        // Count 2: counter = 0
        mapper.CountIRQ();
        Assert.False(mapper.IrqState);

        // Count 3: counter wraps to 0xFFFF -> IRQ fires!
        mapper.CountIRQ();
        Assert.True(mapper.IrqState);
    }
}
