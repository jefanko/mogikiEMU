namespace Mogiki.Core.Video;

/// <summary>
/// A two-slot producer/consumer frame exchange.
///
/// The emulation thread owns a writable slot while it publishes a frame. The
/// presentation thread owns a readable slot until it releases it. A slot is
/// never written while it is being presented, so consumers cannot observe a
/// partially rendered frame. If the UI falls behind, the producer drops a
/// frame instead of blocking the emulation clock.
/// </summary>
public sealed class FrameBufferPipeline
{
    public const int Width = 256;
    public const int Height = 240;
    public const int PixelCount = Width * Height;

    private readonly object _sync = new();
    private readonly uint[][] _buffers =
    [
        new uint[PixelCount],
        new uint[PixelCount]
    ];

    private int _publishedIndex = -1;
    private int _readIndex = -1;
    private int _writeIndex = -1;
    private long _sequence;
    private long _publishedFrames;
    private long _droppedFrames;

    public long PublishedFrames
    {
        get
        {
            lock (_sync)
                return _publishedFrames;
        }
    }

    public long DroppedFrames
    {
        get
        {
            lock (_sync)
                return _droppedFrames;
        }
    }

    public bool HasPublishedFrame
    {
        get
        {
            lock (_sync)
                return _publishedIndex >= 0;
        }
    }

    public bool TryAcquireWrite(out FrameWriteLease lease)
    {
        lock (_sync)
        {
            for (int index = 0; index < _buffers.Length; index++)
            {
                if (index == _publishedIndex || index == _readIndex || index == _writeIndex)
                    continue;

                _writeIndex = index;
                lease = new FrameWriteLease(this, index, _buffers[index]);
                return true;
            }

            _droppedFrames++;
            lease = null!;
            return false;
        }
    }

    public bool TryAcquireLatest(out FrameReadLease lease)
    {
        lock (_sync)
        {
            if (_publishedIndex < 0 || _readIndex >= 0)
            {
                lease = null!;
                return false;
            }

            int index = _publishedIndex;
            _publishedIndex = -1;
            _readIndex = index;
            lease = new FrameReadLease(this, index, _buffers[index], _sequence);
            return true;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _publishedIndex = -1;
            if (_writeIndex >= 0)
                _writeIndex = -1;
        }
    }

    private void CommitWrite(int index)
    {
        lock (_sync)
        {
            if (_writeIndex != index)
                return;

            _writeIndex = -1;
            _publishedIndex = index;
            _sequence++;
            _publishedFrames++;
        }
    }

    private void CancelWrite(int index)
    {
        lock (_sync)
        {
            if (_writeIndex == index)
                _writeIndex = -1;
        }
    }

    private void ReleaseRead(int index)
    {
        lock (_sync)
        {
            if (_readIndex == index)
                _readIndex = -1;
        }
    }

    public sealed class FrameWriteLease : IDisposable
    {
        private readonly FrameBufferPipeline _owner;
        private readonly int _index;
        private bool _completed;

        internal FrameWriteLease(FrameBufferPipeline owner, int index, uint[] buffer)
        {
            _owner = owner;
            _index = index;
            Buffer = buffer;
        }

        public uint[] Buffer { get; }

        public void Commit()
        {
            if (_completed)
                return;

            _completed = true;
            _owner.CommitWrite(_index);
        }

        public void Dispose()
        {
            if (_completed)
                return;

            _completed = true;
            _owner.CancelWrite(_index);
        }
    }

    public sealed class FrameReadLease : IDisposable
    {
        private readonly FrameBufferPipeline _owner;
        private readonly int _index;
        private bool _released;

        internal FrameReadLease(FrameBufferPipeline owner, int index, uint[] buffer, long sequence)
        {
            _owner = owner;
            _index = index;
            Buffer = buffer;
            Sequence = sequence;
        }

        public uint[] Buffer { get; }
        public long Sequence { get; }

        public void Dispose()
        {
            if (_released)
                return;

            _released = true;
            _owner.ReleaseRead(_index);
        }
    }
}
