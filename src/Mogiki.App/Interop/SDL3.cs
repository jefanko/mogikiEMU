using System.Runtime.InteropServices;

namespace Mogiki.App.Interop;

/// <summary>
/// Minimal SDL3 audio interop used by Mogiki.
/// The Avalonia window and framebuffer do not depend on SDL.
/// </summary>
public static unsafe class SDL3
{
    private const string NativeLib = "SDL3";

    public const uint SDL_INIT_AUDIO = 0x00000010;
    public const uint SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK = 0xFFFFFFFF;
    public const uint SDL_AUDIO_F32 = 0x00008120;

    [StructLayout(LayoutKind.Sequential)]
    public struct SDL_AudioSpec
    {
        public uint format;
        public int channels;
        public int freq;
    }

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_Init(uint flags);

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
