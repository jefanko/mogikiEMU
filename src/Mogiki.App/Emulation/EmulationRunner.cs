using System.Diagnostics;
using Mogiki.App.Audio;
using Mogiki.Core.Video;

namespace Mogiki.App.Emulation;

/// <summary>
/// Clocks an <see cref="EmulatorSession"/> on a dedicated thread and publishes
/// completed frames to a double-buffered presentation pipeline.
/// </summary>
public sealed class EmulationRunner : IDisposable
{
    private const double TargetFrameRate = 60.0988;
    private const double CpuFrequency = 1_789_773.0;
    private const double FilterAlpha = 0.4;

    private readonly EmulatorSession _session;
    private readonly FrameBufferPipeline _framePipeline;
    private readonly AudioEngine _audio;

    private Thread? _thread;
    private bool _running;
    private bool _paused = true;
    private bool _fastForward;
    private bool _soundEnabled = true;
    private int _volume = 100;
    private int _controllerState;

    public EmulationRunner(
        EmulatorSession session,
        FrameBufferPipeline framePipeline,
        AudioEngine? audio = null)
    {
        _session = session;
        _framePipeline = framePipeline;
        _audio = audio ?? new AudioEngine();
    }

    public EmulatorSession Session => _session;
    public FrameBufferPipeline FramePipeline => _framePipeline;
    public AudioEngine Audio => _audio;

    public bool IsRunning => Volatile.Read(ref _running);
    public bool IsRomLoaded => _session.IsLoaded;

    public bool IsPaused
    {
        get => Volatile.Read(ref _paused);
        set
        {
            Volatile.Write(ref _paused, value);
            _audio.Pause(value || !SoundEnabled);
        }
    }

    public bool FastForward
    {
        get => Volatile.Read(ref _fastForward);
        set => Volatile.Write(ref _fastForward, value);
    }

    public bool SoundEnabled
    {
        get => Volatile.Read(ref _soundEnabled);
        set
        {
            Volatile.Write(ref _soundEnabled, value);
            _audio.Pause(!value || IsPaused);
        }
    }

    public int Volume
    {
        get => Volatile.Read(ref _volume);
        set => Volatile.Write(ref _volume, Math.Clamp(value, 0, 100));
    }

    public event Action? FrameReady;
    public event Action<double>? FpsUpdated;
    public event Action<Exception>? Faulted;

    public void Start()
    {
        if (IsRunning)
            return;

        _audio.Init();
        _audio.Pause(true);

        Volatile.Write(ref _running, true);
        _thread = new Thread(RunLoop)
        {
            IsBackground = true,
            Name = "Mogiki Emulation Thread",
            Priority = ThreadPriority.AboveNormal
        };
        _thread.Start();
    }

    public bool LoadRom(string path)
    {
        bool wasPaused = IsPaused;
        IsPaused = true;

        bool loaded = _session.LoadRom(path);
        if (!loaded)
        {
            IsPaused = wasPaused;
            return false;
        }

        _audio.Reset();
        _framePipeline.Clear();
        IsPaused = false;
        return true;
    }

    public void StopGame()
    {
        IsPaused = true;
        _session.Unload();
        _audio.Reset();
        _framePipeline.Clear();
        Volatile.Write(ref _controllerState, 0);
    }

    public void Reset()
    {
        _session.Reset();
        _audio.Reset();
        _framePipeline.Clear();
    }

    public void SetControllerState(byte state)
    {
        Volatile.Write(ref _controllerState, state);
    }

    private void RunLoop()
    {
        var frameStopwatch = Stopwatch.StartNew();
        var fpsStopwatch = Stopwatch.StartNew();
        int renderedFrames = 0;
        double audioSampleCounter = 0.0;
        double lastSample = 0.0;
        double sampleFrequency = _audio.SampleRate > 0 ? _audio.SampleRate : 44_100;
        double cyclesPerSample = CpuFrequency / sampleFrequency;

        try
        {
            while (IsRunning)
            {
                if (!_session.IsLoaded || IsPaused)
                {
                    Thread.Sleep(4);
                    continue;
                }

                _session.Bus.Controller[0] = (byte)Volatile.Read(ref _controllerState);

                bool completedFrame = false;
                lock (_session.SyncRoot)
                {
                    if (!_session.IsLoaded || IsPaused)
                        continue;

                    do
                    {
                        _session.Clock();

                        audioSampleCounter += 1.0 / 3.0;
                        if (audioSampleCounter >= cyclesPerSample)
                        {
                            audioSampleCounter -= cyclesPerSample;
                            if (SoundEnabled)
                            {
                                double rawSample = _session.Bus.GetAudioSample();
                                double filtered = lastSample + FilterAlpha * (rawSample - lastSample);
                                lastSample = filtered;
                                float volume = Volume / 100.0f * 0.5f;
                                _audio.WriteSample((float)(filtered * volume));
                            }
                        }

                        completedFrame = _session.Bus.Ppu.FrameComplete;
                    }
                    while (!completedFrame && IsRunning && !IsPaused);

                    if (completedFrame)
                    {
                        _session.Bus.Ppu.FrameComplete = false;
                        if (_framePipeline.TryAcquireWrite(out var writeLease))
                        {
                            using (writeLease)
                            {
                                _session.Bus.Ppu.CopyFrameTo(writeLease.Buffer);
                                writeLease.Commit();
                            }
                        }
                    }
                }

                if (!completedFrame)
                    continue;

                renderedFrames++;
                FrameReady?.Invoke();

                if (fpsStopwatch.ElapsedMilliseconds >= 1000)
                {
                    double fps = renderedFrames * 1000.0 / fpsStopwatch.ElapsedMilliseconds;
                    renderedFrames = 0;
                    fpsStopwatch.Restart();
                    FpsUpdated?.Invoke(fps);
                }

                PaceFrame(frameStopwatch);
            }
        }
        catch (Exception ex)
        {
            Faulted?.Invoke(ex);
        }
        finally
        {
            Volatile.Write(ref _running, false);
        }
    }

    private void PaceFrame(Stopwatch frameStopwatch)
    {
        if (FastForward)
        {
            frameStopwatch.Restart();
            return;
        }

        double targetMilliseconds = 1000.0 / TargetFrameRate;
        double sleepMilliseconds = targetMilliseconds - frameStopwatch.Elapsed.TotalMilliseconds;
        if (sleepMilliseconds > 1.5)
            Thread.Sleep((int)(sleepMilliseconds - 1.0));

        while (frameStopwatch.Elapsed.TotalMilliseconds < targetMilliseconds && IsRunning)
            Thread.SpinWait(10);

        frameStopwatch.Restart();
    }

    public void Dispose()
    {
        Volatile.Write(ref _running, false);
        IsPaused = true;
        _thread?.Join(1000);
        _thread = null;
        _audio.Dispose();
    }
}
