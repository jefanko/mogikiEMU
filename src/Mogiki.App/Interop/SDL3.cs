using System.Runtime.InteropServices;

namespace Mogiki.App.Interop;

/// <summary>
/// Minimal SDL3 audio interop used by Mogiki.
/// The Avalonia window and framebuffer do not depend on SDL.
/// </summary>
public static unsafe class SDL3
{
    private const string NativeLib = "SDL3";

    public const uint SDL_INIT_VIDEO = 0x00000020;
    public const uint SDL_INIT_AUDIO = 0x00000010;
    public const uint SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK = 0xFFFFFFFF;
    public const uint SDL_AUDIO_F32 = 0x00008120;

    public const ulong SDL_WINDOW_RESIZABLE = 0x0000000000000020;
    public const uint SDL_EVENT_QUIT = 0x00000100;
    public const uint SDL_EVENT_WINDOW_CLOSE_REQUESTED = 0x00000210;
    public const uint SDL_EVENT_KEY_DOWN = 0x00000300;
    public const uint SDL_EVENT_KEY_UP = 0x00000301;

    public const uint SDL_PIXELFORMAT_ARGB8888 = 0x16362004;
    public const int SDL_TEXTUREACCESS_STREAMING = 1;
    public const int SDL_SCALEMODE_NEAREST = 0;
    public const int SDL_SCALEMODE_LINEAR = 1;
    public const int SDL_LOGICAL_PRESENTATION_LETTERBOX = 2;

    [StructLayout(LayoutKind.Sequential)]
    public struct SDL_AudioSpec
    {
        public uint format;
        public int channels;
        public int freq;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct SDL_Event
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(0)] public SDL_KeyboardEvent Key;
        [FieldOffset(0)] public SDL_WindowEvent Window;
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct SDL_KeyboardEvent
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(4)] public uint Reserved;
        [FieldOffset(8)] public ulong Timestamp;
        [FieldOffset(16)] public uint WindowId;
        [FieldOffset(20)] public uint Which;
        [FieldOffset(24)] public int Scancode;
        [FieldOffset(28)] public uint Key;
        [FieldOffset(32)] public ushort Mod;
        [FieldOffset(34)] public ushort Raw;
        [FieldOffset(36)] public byte Down;
        [FieldOffset(37)] public byte Repeat;
    }

    [StructLayout(LayoutKind.Explicit, Size = 28)]
    public struct SDL_WindowEvent
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(4)] public uint Reserved;
        [FieldOffset(8)] public ulong Timestamp;
        [FieldOffset(16)] public uint WindowId;
        [FieldOffset(20)] public int Data1;
        [FieldOffset(24)] public int Data2;
    }

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_Init(uint flags);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint SDL_CreateWindow(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string title,
        int width,
        int height,
        ulong flags);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint SDL_CreateRenderer(
        nint window,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? name);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint SDL_CreateTexture(
        nint renderer,
        uint format,
        int access,
        int width,
        int height);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_UpdateTexture(
        nint texture,
        nint rect,
        void* pixels,
        int pitch);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_SetTextureScaleMode(nint texture, int scaleMode);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_SetRenderLogicalPresentation(
        nint renderer,
        int width,
        int height,
        int mode);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_RenderClear(nint renderer);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_RenderTexture(
        nint renderer,
        nint texture,
        nint sourceRect,
        nint destinationRect);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_RenderPresent(nint renderer);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint SDL_GetRendererName(nint renderer);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_PollEvent(out SDL_Event eventData);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_SetWindowFullscreen(
        nint window,
        [MarshalAs(UnmanagedType.I1)] bool fullscreen);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_ShowWindow(nint window);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_SetWindowSize(nint window, int width, int height);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_HideWindow(nint window);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_DestroyTexture(nint texture);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_DestroyRenderer(nint renderer);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_DestroyWindow(nint window);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint SDL_GetError();

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_QuitSubSystem(uint flags);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint SDL_OpenAudioDeviceStream(
        uint devid,
        ref SDL_AudioSpec spec,
        nint callback,
        nint userdata);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_PutAudioStreamData(
        nint stream,
        void* buf,
        int len);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_GetAudioStreamQueued(nint stream);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_ClearAudioStream(nint stream);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_PauseAudioStreamDevice(nint stream);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_ResumeAudioStreamDevice(nint stream);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_DestroyAudioStream(nint stream);
}
