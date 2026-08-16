using System.Runtime.CompilerServices;

namespace Mogiki.Core.Ppu;

/// <summary>
/// 15-bit internal PPU address/scroll register (Loopy register).
/// Bit Layout:
/// yyy NN YYYYY XXXXX
/// ||| || ||||| +++++-- Coarse X (bits 0-4)
/// ||| || +++++-------- Coarse Y (bits 5-9)
/// ||| |+-------------- Nametable X (bit 10)
/// ||| +--------------- Nametable Y (bit 11)
/// +++----------------- Fine Y (bits 12-14)
/// </summary>
public struct LoopyRegister
{
    public ushort Reg;

    public byte CoarseX
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (byte)(Reg & 0x001F);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Reg = (ushort)((Reg & ~0x001F) | (value & 0x1F));
    }

    public byte CoarseY
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (byte)((Reg >> 5) & 0x001F);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Reg = (ushort)((Reg & ~(0x001F << 5)) | ((value & 0x1F) << 5));
    }

    public byte NametableX
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (byte)((Reg >> 10) & 0x0001);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Reg = (ushort)((Reg & ~(0x0001 << 10)) | ((value & 0x01) << 10));
    }

    public byte NametableY
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (byte)((Reg >> 11) & 0x0001);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Reg = (ushort)((Reg & ~(0x0001 << 11)) | ((value & 0x01) << 11));
    }

    public byte FineY
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (byte)((Reg >> 12) & 0x0007);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Reg = (ushort)((Reg & ~(0x0007 << 12)) | ((value & 0x07) << 12));
    }
}
