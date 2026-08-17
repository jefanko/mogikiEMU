using System.Text.RegularExpressions;
using Xunit;

namespace Mogiki.Tests;

public sealed class NestestTraceTests
{
    private static readonly Regex StatePattern = new(
        "^(?<pc>[0-9A-F]{4})\\s+.*A:(?<a>[0-9A-F]{2}) X:(?<x>[0-9A-F]{2}) Y:(?<y>[0-9A-F]{2}) P:(?<p>[0-9A-F]{2}) SP:(?<sp>[0-9A-F]{2})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void NestestCpuStateMatchesReferenceTrace()
    {
        const int instructionCount = 8991;
        var expected = File.ReadLines(RomTestRunner.Fixture("nestest.log"))
            .Where(line => StatePattern.IsMatch(line))
            .Take(instructionCount)
            .Select(Parse)
            .ToArray();
        var actual = RomTestRunner.CaptureNestestTrace(instructionCount);

        Assert.Equal(instructionCount, expected.Length);
        Assert.Equal(instructionCount, actual.Count);

        for (int i = 0; i < instructionCount; i++)
        {
            Assert.True(
                expected[i] == actual[i],
                $"Trace diverged at instruction {i}: expected {expected[i]}, actual {actual[i]}");
        }
    }

    private static CpuTraceEntry Parse(string line)
    {
        var match = StatePattern.Match(line);
        return new CpuTraceEntry(
            Convert.ToUInt16(match.Groups["pc"].Value, 16),
            Convert.ToByte(match.Groups["a"].Value, 16),
            Convert.ToByte(match.Groups["x"].Value, 16),
            Convert.ToByte(match.Groups["y"].Value, 16),
            Convert.ToByte(match.Groups["p"].Value, 16),
            Convert.ToByte(match.Groups["sp"].Value, 16));
    }
}
