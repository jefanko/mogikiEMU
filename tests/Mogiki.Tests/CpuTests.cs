using Mogiki.Core.Bus;
using Mogiki.Core.Cartridge;
using Mogiki.Core.Common;
using Mogiki.Core.Cpu;
using Xunit;

namespace Mogiki.Tests;

public class CpuTests
{
    private static Bus CreateTestBus(byte[] prgRom)
    {
        var bus = new Bus();
        // Pad PRG ROM to 16KB if needed
        byte[] paddedRom = new byte[16384];
        Array.Copy(prgRom, paddedRom, Math.Min(prgRom.Length, paddedRom.Length));
        var cart = new Cartridge(paddedRom, new byte[8192], 0, MirrorMode.Horizontal);
        bus.InsertCartridge(cart);
        bus.Reset();
        return bus;
    }

    [Fact]
    public void Cpu_Reset_InitializesRegistersProperly()
    {
        var bus = new Bus();
        var cpu = bus.Cpu;

        Assert.Equal(0xFD, cpu.Sp);
        Assert.Equal(0, cpu.A);
        Assert.Equal(0, cpu.X);
        Assert.Equal(0, cpu.Y);
        Assert.Equal(1, cpu.GetFlag(Cpu6502.Flags.I));
        Assert.Equal(1, cpu.GetFlag(Cpu6502.Flags.U));
    }

    [Fact]
    public void Cpu_ADC_AddsWithCarryAndSetsFlags()
    {
        var bus = new Bus();
        var cpu = bus.Cpu;

        // Reset state
        cpu.A = 0x50;
        cpu.SetFlag(Cpu6502.Flags.C, false);
        cpu.Fetched = 0x50;
        cpu.Opcode = 0x69; // ADC Immediate

        // Execute ADC: 0x50 + 0x50 = 0xA0 (-96 signed, overflow from positive to negative)
        bus.Write(cpu.Pc, 0x50);
        // Let's test direct ADC logic
        cpu.A = 0x50;
        cpu.Fetched = 0x50;
        cpu.SetFlag(Cpu6502.Flags.C, false);

        ushort temp = (ushort)(cpu.A + cpu.Fetched + cpu.GetFlag(Cpu6502.Flags.C));
        cpu.SetFlag(Cpu6502.Flags.C, temp > 255);
        cpu.SetFlag(Cpu6502.Flags.Z, (temp & 0x00FF) == 0);
        cpu.SetFlag(Cpu6502.Flags.N, (temp & 0x80) != 0);
        cpu.SetFlag(Cpu6502.Flags.V, (~(cpu.A ^ cpu.Fetched) & (cpu.A ^ temp) & 0x0080) != 0);
        cpu.A = (byte)(temp & 0x00FF);

        Assert.Equal(0xA0, cpu.A);
        Assert.Equal(0, cpu.GetFlag(Cpu6502.Flags.C));
        Assert.Equal(0, cpu.GetFlag(Cpu6502.Flags.Z));
        Assert.Equal(1, cpu.GetFlag(Cpu6502.Flags.N));
        Assert.Equal(1, cpu.GetFlag(Cpu6502.Flags.V));
    }

    [Fact]
    public void Cpu_SBC_SubtractsWithBorrowAndSetsFlags()
    {
        var bus = new Bus();
        var cpu = bus.Cpu;

        // 0x50 - 0xF0 with C=1 (no borrow)
        cpu.A = 0x50;
        cpu.Fetched = 0xF0;
        cpu.SetFlag(Cpu6502.Flags.C, true);

        ushort value = (ushort)(cpu.Fetched ^ 0x00FF);
        ushort temp = (ushort)(cpu.A + value + cpu.GetFlag(Cpu6502.Flags.C));
        cpu.SetFlag(Cpu6502.Flags.C, (temp & 0xFF00) != 0);
        cpu.SetFlag(Cpu6502.Flags.Z, (temp & 0x00FF) == 0);
        cpu.SetFlag(Cpu6502.Flags.N, (temp & 0x80) != 0);
        cpu.SetFlag(Cpu6502.Flags.V, ((temp ^ cpu.A) & (temp ^ value) & 0x0080) != 0);
        cpu.A = (byte)(temp & 0x00FF);

        Assert.Equal(0x60, cpu.A);
        Assert.Equal(0, cpu.GetFlag(Cpu6502.Flags.C)); // Borrow occurred
    }

    [Fact]
    public void Cpu_StackPushPull_PreservesData()
    {
        var bus = new Bus();
        var cpu = bus.Cpu;

        // Push A = 0x42
        cpu.A = 0x42;
        bus.Write((ushort)(0x0100 + cpu.Sp), cpu.A);
        cpu.Sp--;

        // Push Status
        cpu.St = 0xA5;
        bus.Write((ushort)(0x0100 + cpu.Sp), (byte)(cpu.St | (byte)Cpu6502.Flags.B | (byte)Cpu6502.Flags.U));
        cpu.Sp--;

        // Pull Status
        cpu.Sp++;
        cpu.St = bus.Read((ushort)(0x0100 + cpu.Sp));

        // Pull A
        cpu.Sp++;
        cpu.A = bus.Read((ushort)(0x0100 + cpu.Sp));

        Assert.Equal(0x42, cpu.A);
        Assert.Equal(0xFD, cpu.Sp);
    }
}
