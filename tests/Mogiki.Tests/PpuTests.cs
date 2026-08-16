using Mogiki.Core.Ppu;
using Xunit;

namespace Mogiki.Tests;

public class PpuTests
{
    [Fact]
    public void LoopyRegister_Bitfields_ReadAndWriteCorrectly()
    {
        var loopy = new LoopyRegister();

        loopy.CoarseX = 0x1F;
        Assert.Equal(0x1F, loopy.CoarseX);
        Assert.Equal(0x001F, loopy.Reg);

        loopy.CoarseY = 0x1A;
        Assert.Equal(0x1A, loopy.CoarseY);
        Assert.Equal((0x1A << 5) | 0x1F, loopy.Reg);

        loopy.NametableX = 1;
        Assert.Equal(1, loopy.NametableX);

        loopy.NametableY = 1;
        Assert.Equal(1, loopy.NametableY);

        loopy.FineY = 7;
        Assert.Equal(7, loopy.FineY);

        // Verify independent modifications don't corrupt adjacent fields
        loopy.CoarseX = 5;
        Assert.Equal(5, loopy.CoarseX);
        Assert.Equal(0x1A, loopy.CoarseY);
        Assert.Equal(1, loopy.NametableX);
        Assert.Equal(1, loopy.NametableY);
        Assert.Equal(7, loopy.FineY);
    }

    [Fact]
    public void Ppu_StatusRead_ClearsVBlankAndResetsLatch()
    {
        var ppu = new Ppu2C02();

        // Write PPUADDR first latch
        ppu.CpuWrite(0x0006, 0x20);

        // Set VBlank flag
        ppu.Status |= 0x80;

        // Read PPUSTATUS ($2002)
        byte status = ppu.CpuRead(0x0002);
        Assert.Equal(0x80, status & 0x80);

        // Second read: VBlank should now be cleared
        status = ppu.CpuRead(0x0002);
        Assert.Equal(0x00, status & 0x80);

        // Address latch should be reset, so next write to $2006 is high byte again
        ppu.CpuWrite(0x0006, 0x3F);
        ppu.CpuWrite(0x0006, 0x00);
        Assert.Equal(0x3F00, ppu.VramAddr.Reg);
    }

    [Fact]
    public void Ppu_PaletteRam_MirroringHandlesIndicesProperly()
    {
        var ppu = new Ppu2C02();

        // Background color write to $3F00
        ppu.PpuWrite(0x3F00, 0x0F);

        // Universal background color mirror reads ($3F10 -> $3F00)
        Assert.Equal(0x0F, ppu.PpuRead(0x3F10));
    }
}
