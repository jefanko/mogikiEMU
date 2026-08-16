using Mogiki.Core.Bus;
using Xunit;

namespace Mogiki.Tests;

public class BusTests
{
    [Fact]
    public void Bus_Ram_MirrorsProperlyAcrossFirst8KB()
    {
        var bus = new Bus();

        // Write to $0000
        bus.Write(0x0000, 0x55);
        Assert.Equal(0x55, bus.Read(0x0000));

        // Mirror at $0800
        Assert.Equal(0x55, bus.Read(0x0800));

        // Mirror at $1000
        Assert.Equal(0x55, bus.Read(0x1000));

        // Mirror at $1800
        Assert.Equal(0x55, bus.Read(0x1800));
    }

    [Fact]
    public void Bus_DMA_Transfers256BytesToOAM()
    {
        var bus = new Bus();

        // Fill page $0200 with test pattern
        for (int i = 0; i < 256; i++)
        {
            bus.Write((ushort)(0x0200 + i), (byte)i);
        }

        // Trigger DMA by writing 0x02 to $4014
        bus.Write(0x4014, 0x02);

        // Clock 514 CPU cycles (514 * 3 = 1542 system clocks)
        for (int i = 0; i < 1600; i++)
        {
            bus.Clock();
        }

        // Verify PPU OAM contents match source page
        for (int i = 0; i < 256; i++)
        {
            Assert.Equal((byte)i, bus.Ppu.OAM[i]);
        }
    }

    [Fact]
    public void Bus_ControllerStrobe_SerializesButtonsCorrectly()
    {
        var bus = new Bus();

        // Set controller 0 button state (e.g. A=1, B=1, Select=0, Start=1 -> 0b11010000 = 0xD0)
        bus.Controller[0] = 0xD0;

        // Strobe controller ($4016 = 1, then $4016 = 0)
        bus.Write(0x4016, 1);
        bus.Write(0x4016, 0);

        // Read 8 button bits sequentially:
        // Bit 7 (A) = 1
        Assert.Equal(1, bus.Read(0x4016));
        // Bit 6 (B) = 1
        Assert.Equal(1, bus.Read(0x4016));
        // Bit 5 (Select) = 0
        Assert.Equal(0, bus.Read(0x4016));
        // Bit 4 (Start) = 1
        Assert.Equal(1, bus.Read(0x4016));
        // Remaining = 0
        Assert.Equal(0, bus.Read(0x4016));
        Assert.Equal(0, bus.Read(0x4016));
        Assert.Equal(0, bus.Read(0x4016));
        Assert.Equal(0, bus.Read(0x4016));
    }
}
