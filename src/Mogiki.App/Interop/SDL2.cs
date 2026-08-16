using System.Runtime.InteropServices;

namespace Mogiki.App.Interop;

public static unsafe class SDL2
{
    private const string NativeLib = "SDL2";

    public const uint SDL_INIT_TIMER = 0x00000001;
    public const uint SDL_INIT_AUDIO = 0x00000010;
    public const uint SDL_INIT_VIDEO = 0x00000020;
    public const uint SDL_INIT_EVENTS = 0x00004000;

    public const int SDL_WINDOWPOS_CENTERED = 0x2FFF0000;
    public const uint SDL_WINDOW_SHOWN = 0x00000004;
    public const uint SDL_WINDOW_RESIZABLE = 0x00000020;

    public const uint SDL_RENDERER_ACCELERATED = 0x00000002;
    public const uint SDL_RENDERER_PRESENTVSYNC = 0x00000004;

    public const uint SDL_PIXELFORMAT_RGBA32 = 376840196;
    public const int SDL_TEXTUREACCESS_STREAMING = 1;

    public const ushort AUDIO_F32SYS = 0x8120;
    public const int SDL_AUDIO_ALLOW_FREQUENCY_CHANGE = 0x00000001;

    public const uint SDL_QUIT = 0x100;
    public const uint SDL_KEYDOWN = 0x300;
    public const uint SDL_KEYUP = 0x301;
    public const uint SDL_DROPFILE = 0x1000;
    public const uint SDL_DROPCOMPLETE = 0x1003;

    // Keycodes
    public const int SDLK_ESCAPE = 27;
    public const int SDLK_SPACE = 32;
    public const int SDLK_a = 'a';
    public const int SDLK_b = 'b';
    public const int SDLK_p = 'p';
    public const int SDLK_s = 's';
    public const int SDLK_x = 'x';
    public const int SDLK_z = 'z';
    public const int SDLK_F1 = 0x4000003A;
    public const int SDLK_RIGHT = 0x4000004F;
    public const int SDLK_LEFT = 0x40000050;
    public const int SDLK_DOWN = 0x40000051;
    public const int SDLK_UP = 0x40000052;

    [StructLayout(LayoutKind.Sequential)]
    public struct SDL_Rect
    {
        public int x;
        public int y;
        public int w;
        public int h;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SDL_Keysym
    {
        public int scancode;
        public int sym;
        public ushort mod;
        public uint unused;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SDL_KeyboardEvent
    {
        public uint type;
        public uint timestamp;
        public uint windowID;
        public byte state;
        public byte repeat;
        public byte padding2;
        public byte padding3;
        public SDL_Keysym keysym;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SDL_DropEvent
    {
        public uint type;
        public uint timestamp;
        public byte* file;
        public uint windowID;
    }

    [StructLayout(LayoutKind.Explicit, Size = 56)]
    public struct SDL_Event
    {
        [FieldOffset(0)] public uint type;
        [FieldOffset(0)] public SDL_KeyboardEvent key;
        [FieldOffset(0)] public SDL_DropEvent drop;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void SDL_AudioCallback(nint userdata, byte* stream, int len);

    [StructLayout(LayoutKind.Sequential)]
    public struct SDL_AudioSpec
    {
        public int freq;
        public ushort format;
        public byte channels;
        public byte silence;
        public ushort samples;
        public ushort padding;
        public uint size;
        public nint callback;
        public nint userdata;
    }

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_Init(uint flags);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_Quit();

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern nint SDL_CreateWindow(string title, int x, int y, int w, int h, uint flags);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_DestroyWindow(nint window);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_SetWindowTitle(nint window, string title);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_SetWindowSize(nint window, int w, int h);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_SetWindowPosition(nint window, int x, int y);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_GetWindowSize(nint window, out int w, out int h);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint SDL_CreateRenderer(nint window, int index, uint flags);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_DestroyRenderer(nint renderer);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_SetRenderDrawColor(nint renderer, byte r, byte g, byte b, byte a);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_RenderClear(nint renderer);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_RenderCopy(nint renderer, nint texture, SDL_Rect* srcrect, SDL_Rect* dstrect);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_RenderPresent(nint renderer);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint SDL_CreateTexture(nint renderer, uint format, int access, int w, int h);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_DestroyTexture(nint texture);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_UpdateTexture(nint texture, SDL_Rect* rect, void* pixels, int pitch);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_PollEvent(SDL_Event* @event);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern uint SDL_OpenAudioDevice(string? device, int iscapture, SDL_AudioSpec* desired, SDL_AudioSpec* obtained, int allowed_changes);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_PauseAudioDevice(uint dev, int pause_on);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_CloseAudioDevice(uint dev);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_free(void* mem);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint SDL_GetError();
}
