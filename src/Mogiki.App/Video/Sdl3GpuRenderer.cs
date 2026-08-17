using System.Runtime.InteropServices;
using Mogiki.App.Interop;

namespace Mogiki.App.Video;

/// <summary>
/// Presents NES frames through SDL3's accelerated renderer.
///
/// SDL chooses the graphics backend behind SDL_Renderer. Auto mode asks for
/// Vulkan/OpenGL first and then falls back to the native accelerated backend
/// before the Avalonia bitmap path is used.
/// </summary>
public sealed unsafe class Sdl3GpuRenderer : IDisposable
{
    private readonly string? _requestedBackend;
    private nint _window;
    private nint _renderer;
    private nint _texture;
    private bool _sdlInitialized;
    private bool _fullscreen;
    private bool _bilinear;
    private int _logicalWidth;
    private int _logicalHeight;

    public Sdl3GpuRenderer(string? backend = null)
    {
        _requestedBackend = backend;
    }

    public bool IsAvailable => _window != 0 && _renderer != 0 && _texture != 0;
    public string BackendName { get; private set; } = "Unavailable";

    public event Action? Closed;
    public event Action<uint, bool>? KeyChanged;

    public bool TryStart(string title, int logicalWidth, int logicalHeight, int scale, bool bilinear)
    {
        if (IsAvailable)
            return true;

        try
        {
            if (!SDL3.SDL_Init(SDL3.SDL_INIT_VIDEO))
                throw new InvalidOperationException(ErrorMessage("SDL3 video initialization failed"));

            _sdlInitialized = true;
            _window = SDL3.SDL_CreateWindow(
                title,
                Math.Max(logicalWidth * Math.Max(1, scale), 640),
                Math.Max(logicalHeight * Math.Max(1, scale), 480),
                SDL3.SDL_WINDOW_RESIZABLE);

            if (_window == 0)
                throw new InvalidOperationException(ErrorMessage("SDL3 game window creation failed"));

            _renderer = SDL3.SDL_CreateRenderer(_window, RendererName());
            if (_renderer == 0)
                throw new InvalidOperationException(ErrorMessage("SDL3 accelerated renderer creation failed"));

            _texture = SDL3.SDL_CreateTexture(
                _renderer,
                SDL3.SDL_PIXELFORMAT_ARGB8888,
                SDL3.SDL_TEXTUREACCESS_STREAMING,
                256,
                240);

            if (_texture == 0)
                throw new InvalidOperationException(ErrorMessage("SDL3 frame texture creation failed"));

            _logicalWidth = logicalWidth;
            _logicalHeight = logicalHeight;
            if (!SDL3.SDL_SetRenderLogicalPresentation(
                    _renderer,
                    logicalWidth,
                    logicalHeight,
                    SDL3.SDL_LOGICAL_PRESENTATION_LETTERBOX))
            {
                throw new InvalidOperationException(ErrorMessage("SDL3 logical presentation setup failed"));
            }

            SetBilinearFilter(bilinear);
            BackendName = Marshal.PtrToStringUTF8(SDL3.SDL_GetRendererName(_renderer)) ?? "SDL3 accelerated";
            SDL3.SDL_ShowWindow(_window);
            return true;
        }
        catch (DllNotFoundException ex)
        {
            Console.Error.WriteLine($"SDL3 renderer unavailable: {ex.Message}");
        }
        catch (EntryPointNotFoundException ex)
        {
            Console.Error.WriteLine($"SDL3 renderer API unavailable: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
        }

        DisposeNative();
        return false;
    }

    public void SetBilinearFilter(bool enabled)
    {
        _bilinear = enabled;
        if (_texture != 0)
        {
            SDL3.SDL_SetTextureScaleMode(
                _texture,
                enabled ? SDL3.SDL_SCALEMODE_LINEAR : SDL3.SDL_SCALEMODE_NEAREST);
        }
    }

    public void SetLogicalSize(int width, int height)
    {
        _logicalWidth = width;
        _logicalHeight = height;
        if (_renderer != 0)
        {
            SDL3.SDL_SetRenderLogicalPresentation(
                _renderer,
                width,
                height,
                SDL3.SDL_LOGICAL_PRESENTATION_LETTERBOX);
        }
    }

    public void ApplyScale(int scale)
    {
        if (_window == 0 || _fullscreen)
            return;

        int width = Math.Max(_logicalWidth * Math.Max(1, scale), 640);
        int height = Math.Max(_logicalHeight * Math.Max(1, scale), 480);
        SDL3.SDL_SetWindowSize(_window, width, height);
    }

    public void Present(uint[] pixels)
    {
        if (!IsAvailable || pixels.Length < 256 * 240)
            return;

        PumpEvents();
        if (!IsAvailable)
            return;

        SetBilinearFilter(_bilinear);

        fixed (uint* source = pixels)
        {
            if (!SDL3.SDL_UpdateTexture(_texture, nint.Zero, source, 256 * sizeof(uint)))
            {
                Console.Error.WriteLine(ErrorMessage("SDL3 texture upload failed"));
                return;
            }
        }

        SDL3.SDL_RenderClear(_renderer);
        SDL3.SDL_RenderTexture(_renderer, _texture, nint.Zero, nint.Zero);
        if (!SDL3.SDL_RenderPresent(_renderer))
            Console.Error.WriteLine(ErrorMessage("SDL3 frame presentation failed"));
    }

    public void ToggleFullscreen()
    {
        if (_window == 0)
            return;

        _fullscreen = !_fullscreen;
        if (!SDL3.SDL_SetWindowFullscreen(_window, _fullscreen))
            Console.Error.WriteLine(ErrorMessage("SDL3 fullscreen request failed"));
    }

    private void PumpEvents()
    {
        while (SDL3.SDL_PollEvent(out var eventData))
        {
            if (eventData.Type is SDL3.SDL_EVENT_QUIT or SDL3.SDL_EVENT_WINDOW_CLOSE_REQUESTED)
            {
                Closed?.Invoke();
                return;
            }

            if (eventData.Type == SDL3.SDL_EVENT_KEY_DOWN)
                KeyChanged?.Invoke(eventData.Key.Key, true);
            else if (eventData.Type == SDL3.SDL_EVENT_KEY_UP)
                KeyChanged?.Invoke(eventData.Key.Key, false);
        }
    }

    private string? RendererName()
    {
        if (string.IsNullOrWhiteSpace(_requestedBackend)
            || string.Equals(_requestedBackend, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return "vulkan,opengl,direct3d11,direct3d12";
        }

        return _requestedBackend.ToLowerInvariant() switch
        {
            "vulkan" => "vulkan",
            "opengl" => "opengl",
            "direct3d11" => "direct3d11",
            "direct3d12" => "direct3d12",
            _ => _requestedBackend
        };
    }

    private static string ErrorMessage(string prefix)
    {
        nint error = SDL3.SDL_GetError();
        string detail = error == 0 ? "unknown SDL error" : Marshal.PtrToStringUTF8(error) ?? "unknown SDL error";
        return $"{prefix}: {detail}";
    }

    private void DisposeNative()
    {
        if (_texture != 0)
        {
            SDL3.SDL_DestroyTexture(_texture);
            _texture = 0;
        }

        if (_renderer != 0)
        {
            SDL3.SDL_DestroyRenderer(_renderer);
            _renderer = 0;
        }

        if (_window != 0)
        {
            SDL3.SDL_DestroyWindow(_window);
            _window = 0;
        }

        if (_sdlInitialized)
        {
            SDL3.SDL_QuitSubSystem(SDL3.SDL_INIT_VIDEO);
            _sdlInitialized = false;
        }

        BackendName = "Unavailable";
    }

    public void Dispose()
    {
        DisposeNative();
        GC.SuppressFinalize(this);
    }
}
