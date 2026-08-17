using Mogiki.Core.Bus;
using Mogiki.Core.Cartridge;

namespace Mogiki.Tests;

internal sealed record RomRunResult(
    Bus Bus,
    long MasterClocks,
    int Frames,
    bool StatusSignatureSeen,
    byte Status,
    string Text)
{
    public bool Passed => StatusSignatureSeen && Status == 0;
}

internal sealed record CpuTraceEntry(
    ushort Pc,
    byte A,
    byte X,
    byte Y,
    byte Status,
    byte StackPointer);

internal static class RomTestRunner
{
    public static string Fixture(string fileName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Roms", fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"ROM fixture was not copied to the test output: {fileName}", path);
        }

        return path;
    }

    public static RomRunResult Run(
        string fileName,
        long maxMasterClocks,
        bool startNestestAtC000 = false)
    {
        var bus = new Bus();
        var cartridge = new Cartridge(Fixture(fileName));
        if (!cartridge.ImageValid)
        {
            throw new InvalidDataException($"ROM fixture is not supported: {fileName}");
        }

        bus.InsertCartridge(cartridge);
        bus.Reset();

        if (startNestestAtC000)
        {
            bus.Cpu.Pc = 0xC000;
            bus.Cpu.Cycles = 0;
        }

        bool signatureSeen = false;
        byte status = 0xFF;
        int frames = 0;
        long masterClock;

        for (masterClock = 0; masterClock < maxMasterClocks; masterClock++)
        {
            // Check before the next clock so the final instruction boundary is
            // observed before the harness executes the ROM's idle loop.
            if (startNestestAtC000 && bus.Cpu.Pc == 0xC66E && bus.Cpu.Sp == 0xFD && masterClock > 10_000)
            {
                break;
            }

            bus.Clock();

            if (bus.Ppu.FrameComplete)
            {
                frames++;
                bus.Ppu.FrameComplete = false;
            }

            if (bus.Read(0x6001) == 0xDE
                && bus.Read(0x6002) == 0xB0
                && bus.Read(0x6003) == 0x61)
            {
                signatureSeen = true;
                status = bus.Read(0x6000);
                if (status != 0x80)
                {
                    break;
                }
            }

            // The canonical nestest log ends at the RTS at $C66E. Its
            // automation mode has no $6000 status protocol.
        }

        string text = ReadText(bus);
        return new RomRunResult(bus, masterClock, frames, signatureSeen, status, text);
    }

    public static IReadOnlyList<CpuTraceEntry> CaptureNestestTrace(int instructionCount)
    {
        var bus = new Bus();
        var cartridge = new Cartridge(Fixture("nestest.nes"));
        if (!cartridge.ImageValid)
            throw new InvalidDataException("nestest.nes is not a supported ROM fixture");

        bus.InsertCartridge(cartridge);
        bus.Reset();
        bus.Cpu.Pc = 0xC000;
        bus.Cpu.Cycles = 0;

        var trace = new List<CpuTraceEntry>(instructionCount);
        long maxMasterClocks = instructionCount * 100L;
        for (long masterClock = 0; masterClock < maxMasterClocks && trace.Count < instructionCount; masterClock++)
        {
            // Bus.Clock clocks the CPU every third master clock. Only sample
            // an instruction boundary at the same phase, otherwise the
            // interval between CPU clocks would be counted twice.
            if (masterClock % 3 == 0 && bus.Cpu.Cycles == 0)
            {
                trace.Add(new CpuTraceEntry(
                    bus.Cpu.Pc,
                    bus.Cpu.A,
                    bus.Cpu.X,
                    bus.Cpu.Y,
                    bus.Cpu.St,
                    bus.Cpu.Sp));
            }

            bus.Clock();
            if (bus.Ppu.FrameComplete)
                bus.Ppu.FrameComplete = false;
        }

        return trace;
    }

    private static string ReadText(Bus bus)
    {
        var chars = new List<char>();
        for (ushort address = 0x6004; address < 0x6100; address++)
        {
            byte value = bus.Read(address);
            if (value == 0)
                break;

            chars.Add(value is >= 0x20 and <= 0x7E ? (char)value : '.');
        }

        return new string(chars.ToArray());
    }
}
