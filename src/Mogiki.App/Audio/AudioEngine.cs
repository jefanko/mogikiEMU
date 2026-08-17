using Mogiki.App.Interop;

namespace Mogiki.App.Audio;

/// <summary>
/// Low-latency mono audio output backed by an SDL3 audio stream.
/// SDL3 owns the device-side buffering and format conversion; Mogiki feeds
/// small batches of emulator-generated float samples into the stream.
/// </summary>
public sealed unsafe class AudioEngine : IDisposable
{
    private const int AudioBatchSize = 512;
    private const int MaxQueuedSamples = 8192;

    private readonly float[] _pendingSamples = new float[AudioBatchSize];
    private readonly object _streamLock = new();
    private nint _audioStream;
    private bool _sdlInitialized;
    private int _pendingCount;

    public int SampleRate { get; private set; } = 44100;
    public bool IsAvailable => _audioStream != 0;

    public bool Init()
    {
        try
        {
            if (!SDL3.SDL_Init(SDL3.SDL_INIT_AUDIO))
            {
                Console.Error.WriteLine("SDL3 audio initialization failed.");
                return false;
            }

            _sdlInitialized = true;

            var desired = new SDL3.SDL_AudioSpec
            {
                format = SDL3.SDL_AUDIO_F32,
                channels = 1,
                freq = SampleRate
            };

            _audioStream = SDL3.SDL_OpenAudioDeviceStream(
                SDL3.SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK,
                ref desired,
                nint.Zero,
                nint.Zero);

            if (_audioStream == 0)
            {
                Console.Error.WriteLine("SDL3 audio stream creation failed.");
                return false;
            }

            if (!SDL3.SDL_ResumeAudioStreamDevice(_audioStream))
            {
                Console.Error.WriteLine("SDL3 audio device could not be resumed.");
                SDL3.SDL_DestroyAudioStream(_audioStream);
                _audioStream = 0;
                return false;
            }

            return true;
        }
        catch (DllNotFoundException ex)
        {
            Console.Error.WriteLine($"SDL3.dll was not found: {ex.Message}");
        }
        catch (EntryPointNotFoundException ex)
        {
            Console.Error.WriteLine($"The installed SDL3.dll is incompatible: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"SDL3 audio initialization failed: {ex.Message}");
        }

        return false;
    }

    public void Pause(bool pause)
    {
        if (_audioStream == 0)
            return;

        lock (_streamLock)
        {
            FlushPendingSamples();

            if (pause)
            {
                SDL3.SDL_PauseAudioStreamDevice(_audioStream);
            }
            else
            {
                SDL3.SDL_ResumeAudioStreamDevice(_audioStream);
            }
        }
    }

    public void Reset()
    {
        lock (_streamLock)
        {
            _pendingCount = 0;
            if (_audioStream != 0)
            {
                SDL3.SDL_ClearAudioStream(_audioStream);
            }
        }
    }

    public void WriteSample(float sample)
    {
        if (_audioStream == 0)
            return;

        lock (_streamLock)
        {
            _pendingSamples[_pendingCount++] = sample;
            if (_pendingCount == _pendingSamples.Length)
            {
                FlushPendingSamples();
            }
        }
    }

    private void FlushPendingSamples()
    {
        if (_audioStream == 0 || _pendingCount == 0)
            return;

        // Keep fast-forward mode from building an unbounded SDL stream.
        if (SDL3.SDL_GetAudioStreamQueued(_audioStream) > MaxQueuedSamples * sizeof(float))
        {
            SDL3.SDL_ClearAudioStream(_audioStream);
        }

        fixed (float* samples = _pendingSamples)
        {
            if (!SDL3.SDL_PutAudioStreamData(
                    _audioStream,
                    samples,
                    _pendingCount * sizeof(float)))
            {
                Console.Error.WriteLine("SDL3 rejected an audio sample batch.");
            }
        }

        _pendingCount = 0;
    }

    public void Dispose()
    {
        lock (_streamLock)
        {
            _pendingCount = 0;

            if (_audioStream != 0)
            {
                SDL3.SDL_DestroyAudioStream(_audioStream);
                _audioStream = 0;
            }

            if (_sdlInitialized)
            {
                SDL3.SDL_QuitSubSystem(SDL3.SDL_INIT_AUDIO);
                _sdlInitialized = false;
            }
        }
    }
}
