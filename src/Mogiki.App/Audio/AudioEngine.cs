using System.Runtime.InteropServices;
using Mogiki.App.Interop;

namespace Mogiki.App.Audio;

public sealed unsafe class AudioEngine : IDisposable
{
    private const int AudioBufferSize = 32768;
    private readonly float[] _audioBuffer = new float[AudioBufferSize];
    private volatile int _writePos;
    private volatile int _readPos;

    private uint _audioDevice;
    private readonly SDL2.SDL_AudioCallback _callbackDelegate;
    private GCHandle _callbackHandle;

    public int SampleRate { get; private set; } = 44100;

    public AudioEngine()
    {
        _callbackDelegate = AudioCallback;
        _callbackHandle = GCHandle.Alloc(_callbackDelegate);
    }

    public bool Init()
    {
        SDL2.SDL_Init(SDL2.SDL_INIT_AUDIO);

        var desired = new SDL2.SDL_AudioSpec
        {
            freq = 44100,
            format = SDL2.AUDIO_F32SYS,
            channels = 1,
            samples = 1024,
            callback = Marshal.GetFunctionPointerForDelegate(_callbackDelegate),
            userdata = nint.Zero
        };

        var obtained = new SDL2.SDL_AudioSpec();
        _audioDevice = SDL2.SDL_OpenAudioDevice(null, 0, &desired, &obtained, SDL2.SDL_AUDIO_ALLOW_FREQUENCY_CHANGE);

        if (_audioDevice == 0)
        {
            return false;
        }

        SampleRate = obtained.freq;
        SDL2.SDL_PauseAudioDevice(_audioDevice, 0); // Start playback
        return true;
    }

    public void Pause(bool pause)
    {
        if (_audioDevice != 0)
        {
            SDL2.SDL_PauseAudioDevice(_audioDevice, pause ? 1 : 0);
        }
    }

    public void Reset()
    {
        _writePos = 0;
        _readPos = 0;
        Array.Clear(_audioBuffer, 0, _audioBuffer.Length);
    }

    public void WriteSample(float sample)
    {
        int write = _writePos;
        int read = _readPos;

        // Keep buffer bounded (~100ms max latency) to prevent delay buildup
        if (write - read > 8192)
        {
            _readPos = write - 4096;
            read = _readPos;
        }

        if (write - read < AudioBufferSize - 1)
        {
            _audioBuffer[write % AudioBufferSize] = sample;
            _writePos = write + 1;
        }
    }

    private void AudioCallback(nint userdata, byte* stream, int len)
    {
        float* output = (float*)stream;
        int samples = len / sizeof(float);

        int read = _readPos;
        int write = _writePos;

        for (int i = 0; i < samples; i++)
        {
            if (read < write)
            {
                output[i] = _audioBuffer[read % AudioBufferSize];
                read++;
            }
            else
            {
                // Buffer underrun: gently decay last sample to eliminate clicking
                output[i] = i > 0 ? output[i - 1] * 0.95f : 0.0f;
            }
        }

        _readPos = read;
    }

    public void Dispose()
    {
        if (_audioDevice != 0)
        {
            SDL2.SDL_CloseAudioDevice(_audioDevice);
            _audioDevice = 0;
        }

        if (_callbackHandle.IsAllocated)
        {
            _callbackHandle.Free();
        }
    }
}
