using Xunit;

namespace Mogiki.Tests;

public sealed class RomIntegrationTests
{
    [Fact]
    public void Nestest_CompletesWithoutReportedCpuErrors()
    {
        var result = RomTestRunner.Run(
            "nestest.nes",
            maxMasterClocks: 3_000_000,
            startNestestAtC000: true);

        Assert.True(result.Bus.Cpu.Pc == 0xC66E,
            $"nestest stopped at ${result.Bus.Cpu.Pc:X4} after {result.MasterClocks} master clocks; status signature={result.StatusSignatureSeen} status=${result.Status:X2}");
        Assert.Equal(0x00, result.Bus.Cpu.A);
        Assert.Equal(0xFF, result.Bus.Cpu.X);
        Assert.Equal(0x15, result.Bus.Cpu.Y);
        Assert.Equal(0x27, result.Bus.Cpu.St);
        Assert.Equal(0xFD, result.Bus.Cpu.Sp);
        Assert.True(result.MasterClocks < 3_000_000, "nestest did not reach its completion loop");
    }

    [Fact]
    public void BlarggOfficialCpuRomBootsAndRuns()
    {
        var result = RomTestRunner.Run("blargg_cpu_official.nes", 5_000_000);

        Assert.True(result.Frames > 0, "Blargg CPU ROM did not reach a rendered frame");
        Assert.NotEqual(0, result.Bus.Cpu.Pc);
    }

    [Theory]
    [InlineData("instr_test_basics.nes")]
    [InlineData("instr_test_implied.nes")]
    [InlineData("instr_test_branches.nes")]
    public void OfficialInstructionRomReportsPass(string fixture)
    {
        var result = RomTestRunner.Run(fixture, 20_000_000);

        Assert.True(result.StatusSignatureSeen, $"Instruction-test status signature was never written: {result.Text}");
        Assert.True(result.Passed, $"Instruction test failed with status {result.Status}: {result.Text}");
    }

    [Theory]
    [InlineData("ppu_vbl_basics.nes")]
    public void PpuVblankNmiRomReportsPass(string fixture)
    {
        var result = RomTestRunner.Run(fixture, 20_000_000);

        Assert.True(result.StatusSignatureSeen, $"PPU VBL/NMI status signature was never written: {result.Text}");
        Assert.True(result.Passed, $"PPU VBL/NMI test failed with status {result.Status}: {result.Text}");
    }
}
