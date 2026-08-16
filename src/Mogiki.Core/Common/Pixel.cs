using System.Runtime.InteropServices;

namespace Mogiki.Core.Common;

/// <summary>
/// Represents a 32-bit RGBA pixel matching the native frame buffer layout.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Pixel
{
    public byte R;
    public byte G;
    public byte B;
    public byte A;

    public Pixel(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public uint RawValue => (uint)(R | (G << 8) | (B << 16) | (A << 24));
}
