using Mogiki.Core.Apu;
using Xunit;

namespace Mogiki.Tests;

public class ApuTests
{
    [Fact]
    public void Apu_Status_LengthCounters_ReportActiveChannels()
    {
        var apu = new Apu2A03();

        // Enable Pulse 1 and Triangle in $4015
        apu.CpuWrite(0x4015, 0x05);

        // Load length counter for Pulse 1 ($4003) with index 0 -> 10
        apu.CpuWrite(0x4003, 0x00);

        // Load length counter for Triangle ($400B) with index 0 -> 10
        apu.CpuWrite(0x400B, 0x00);

        byte status = apu.CpuRead(0x4015);
        Assert.Equal(0x05, status & 0x05);
    }

    [Fact]
    public void Apu_Pulse_EnvelopeDecay_DecrementsOnClock()
    {
        var channel = new Apu2A03.PulseChannel
        {
            Enabled = true,
            EnvelopePeriod = 0,
            EnvelopeStart = true
        };

        // First clock initializes decay to 15
        channel.ClockEnvelope();
        Assert.Equal(15, channel.EnvelopeDecay);

        // Next clock decrements decay to 14
        channel.ClockEnvelope();
        Assert.Equal(14, channel.EnvelopeDecay);
    }

    [Fact]
    public void Apu_Mixer_ZeroInputs_ProducesQuietOutput()
    {
        var apu = new Apu2A03();
        double sample = apu.GetOutputSample();
        Assert.InRange(sample, -1.01, -0.99); // Normalizes around -1.0 when silent
    }
}
