using Mogiki.Core.Video;

namespace Mogiki.Tests;

public sealed class FrameBufferTests
{
    [Fact]
    public void FramePipelineNeverReusesTheFrameCurrentlyBeingRead()
    {
        var pipeline = new FrameBufferPipeline();

        Assert.True(pipeline.TryAcquireWrite(out var firstWrite));
        firstWrite.Buffer[0] = 0x11223344;
        firstWrite.Commit();

        Assert.True(pipeline.TryAcquireLatest(out var firstRead));
        Assert.Equal(0x11223344u, firstRead.Buffer[0]);

        Assert.True(pipeline.TryAcquireWrite(out var secondWrite));
        secondWrite.Buffer[0] = 0x55667788;
        secondWrite.Commit();

        Assert.Equal(0x11223344u, firstRead.Buffer[0]);
        Assert.False(pipeline.TryAcquireWrite(out _));

        firstRead.Dispose();

        Assert.True(pipeline.TryAcquireLatest(out var secondRead));
        using (secondRead)
        {
            Assert.Equal(0x55667788u, secondRead.Buffer[0]);
            Assert.Equal(2, secondRead.Sequence);
        }
    }

    [Fact]
    public void FramePipelineDropsWhenBothSlotsAreOwned()
    {
        var pipeline = new FrameBufferPipeline();

        Assert.True(pipeline.TryAcquireWrite(out var firstWrite));
        firstWrite.Commit();
        Assert.True(pipeline.TryAcquireLatest(out var firstRead));

        Assert.True(pipeline.TryAcquireWrite(out var secondWrite));
        secondWrite.Commit();

        Assert.False(pipeline.TryAcquireWrite(out _));
        Assert.Equal(1, pipeline.DroppedFrames);

        firstRead.Dispose();
    }
}
