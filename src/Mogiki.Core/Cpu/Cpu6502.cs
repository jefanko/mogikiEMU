using System.Runtime.CompilerServices;
using Mogiki.Core.Bus;

namespace Mogiki.Core.Cpu;

/// <summary>
/// Ricoh 2A03 / MOS 6502 CPU Core for NES.
/// High performance zero-overhead instruction dispatch via unmanaged function pointers.
/// </summary>
public sealed unsafe class Cpu6502
{
    // CPU Registers
    public byte A;      // Accumulator
    public byte X;      // X Register
    public byte Y;      // Y Register
    public byte St;     // Status Register
    public byte Sp;     // Stack Pointer
    public ushort Pc;   // Program Counter

    // Status Flags
    [Flags]
    public enum Flags : byte
    {
        C = 1 << 0, // Carry Bit
        Z = 1 << 1, // Zero
        I = 1 << 2, // Disable Interrupts
        D = 1 << 3, // Decimal Mode (unused on NES 2A03)
        B = 1 << 4, // Break
        U = 1 << 5, // Unused / Reserved (always pushed as 1)
        V = 1 << 6, // Overflow
        N = 1 << 7  // Negative
    }

    public Bus.Bus? Bus { get; set; }

    public byte Fetched;
    public ushort AddrAbs;
    public ushort AddrRel;
    public byte Opcode;
    public byte Cycles;
    public uint ClockCount;

    public bool NMIPending;

    private readonly struct Instruction
    {
        public readonly string Name;
        public readonly delegate*<Cpu6502, byte> Operate;
        public readonly delegate*<Cpu6502, byte> AddrMode;
        public readonly byte BaseCycles;
        public readonly bool IsImplied;

        public Instruction(string name, delegate*<Cpu6502, byte> operate, delegate*<Cpu6502, byte> addrMode, byte cycles)
        {
            Name = name;
            Operate = operate;
            AddrMode = addrMode;
            BaseCycles = cycles;
            IsImplied = (nint)addrMode == (nint)(delegate*<Cpu6502, byte>)&ImpliedMode;
        }
    }

    private readonly Instruction[] _lookup;

    public Cpu6502()
    {
        _lookup = CreateLookupTable();
        Reset();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte GetFlag(Flags f) => (St & (byte)f) != 0 ? (byte)1 : (byte)0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetFlag(Flags f, bool v)
    {
        if (v)
            St |= (byte)f;
        else
            St &= (byte)~f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte Read(ushort addr) => Bus?.Read(addr, false) ?? 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Write(ushort addr, byte data) => Bus?.Write(addr, data);

    public void Reset()
    {
        A = 0;
        X = 0;
        Y = 0;
        St = (byte)(Flags.U | Flags.I);
        Sp = 0xFD;

        AddrAbs = 0xFFFC;
        ushort lo = Read((ushort)(AddrAbs + 0));
        ushort hi = Read((ushort)(AddrAbs + 1));
        Pc = (ushort)((hi << 8) | lo);

        AddrRel = 0x0000;
        AddrAbs = 0x0000;
        Fetched = 0x00;

        NMIPending = false;
        Cycles = 8;
    }

    public void Irq()
    {
        // Handled as level-triggered interrupt in Clock()
    }

    public void Nmi()
    {
        NMIPending = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clock()
    {
        if (Cycles == 0)
        {
            bool irqAsserted = Bus != null && ((Bus.Cartridge != null && Bus.Cartridge.IrqState) || Bus.Apu.GetIRQ());

            if (NMIPending)
            {
                NMIPending = false;
                Write((ushort)(0x0100 + Sp), (byte)((Pc >> 8) & 0xFF));
                Sp--;
                Write((ushort)(0x0100 + Sp), (byte)(Pc & 0xFF));
                Sp--;

                // Push status with B=0 and U=1, preserving original I flag
                byte statusToPush = (byte)((St & ~(byte)Flags.B) | (byte)Flags.U);
                Write((ushort)(0x0100 + Sp), statusToPush);
                Sp--;

                SetFlag(Flags.I, true);

                AddrAbs = 0xFFFA;
                ushort lo = Read((ushort)(AddrAbs + 0));
                ushort hi = Read((ushort)(AddrAbs + 1));
                Pc = (ushort)((hi << 8) | lo);

                Cycles = 8;
            }
            else if (irqAsserted && GetFlag(Flags.I) == 0)
            {
                Write((ushort)(0x0100 + Sp), (byte)((Pc >> 8) & 0xFF));
                Sp--;
                Write((ushort)(0x0100 + Sp), (byte)(Pc & 0xFF));
                Sp--;

                // Push status with B=0 and U=1, preserving original I flag
                byte statusToPush = (byte)((St & ~(byte)Flags.B) | (byte)Flags.U);
                Write((ushort)(0x0100 + Sp), statusToPush);
                Sp--;

                SetFlag(Flags.I, true);

                AddrAbs = 0xFFFE;
                ushort lo = Read((ushort)(AddrAbs + 0));
                ushort hi = Read((ushort)(AddrAbs + 1));
                Pc = (ushort)((hi << 8) | lo);

                Cycles = 7;
            }
            else
            {
                Opcode = Read(Pc);
                SetFlag(Flags.U, true);
                Pc++;

                ref readonly var inst = ref _lookup[Opcode];
                Cycles = inst.BaseCycles;

                byte additionalCycle1 = inst.AddrMode(this);
                byte additionalCycle2 = inst.Operate(this);

                Cycles += (byte)(additionalCycle1 & additionalCycle2);
                SetFlag(Flags.U, true);
            }
        }

        ClockCount++;
        Cycles--;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte Fetch()
    {
        if (!_lookup[Opcode].IsImplied)
            Fetched = Read(AddrAbs);
        return Fetched;
    }

    // ==========================================
    // Addressing Modes
    // ==========================================
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ImpliedMode(Cpu6502 c)
    {
        c.Fetched = c.A;
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ImmediateMode(Cpu6502 c)
    {
        c.AddrAbs = c.Pc++;
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ZeroPageMode(Cpu6502 c)
    {
        c.AddrAbs = (ushort)(c.Read(c.Pc++) & 0x00FF);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ZeroPageXMode(Cpu6502 c)
    {
        c.AddrAbs = (ushort)((c.Read(c.Pc++) + c.X) & 0x00FF);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ZeroPageYMode(Cpu6502 c)
    {
        c.AddrAbs = (ushort)((c.Read(c.Pc++) + c.Y) & 0x00FF);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte RelativeMode(Cpu6502 c)
    {
        c.AddrRel = c.Read(c.Pc++);
        if ((c.AddrRel & 0x80) != 0)
            c.AddrRel |= 0xFF00;
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte AbsoluteMode(Cpu6502 c)
    {
        ushort lo = c.Read(c.Pc++);
        ushort hi = c.Read(c.Pc++);
        c.AddrAbs = (ushort)((hi << 8) | lo);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte AbsoluteXMode(Cpu6502 c)
    {
        ushort lo = c.Read(c.Pc++);
        ushort hi = c.Read(c.Pc++);
        c.AddrAbs = (ushort)(((hi << 8) | lo) + c.X);
        return (byte)((c.AddrAbs & 0xFF00) != (hi << 8) ? 1 : 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte AbsoluteYMode(Cpu6502 c)
    {
        ushort lo = c.Read(c.Pc++);
        ushort hi = c.Read(c.Pc++);
        c.AddrAbs = (ushort)(((hi << 8) | lo) + c.Y);
        return (byte)((c.AddrAbs & 0xFF00) != (hi << 8) ? 1 : 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte IndirectMode(Cpu6502 c)
    {
        ushort ptrLo = c.Read(c.Pc++);
        ushort ptrHi = c.Read(c.Pc++);
        ushort ptr = (ushort)((ptrHi << 8) | ptrLo);

        // Hardware page boundary wrapping bug emulation
        if (ptrLo == 0x00FF)
            c.AddrAbs = (ushort)((c.Read((ushort)(ptr & 0xFF00)) << 8) | c.Read(ptr));
        else
            c.AddrAbs = (ushort)((c.Read((ushort)(ptr + 1)) << 8) | c.Read(ptr));

        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte IndirectXMode(Cpu6502 c)
    {
        ushort t = c.Read(c.Pc++);
        ushort lo = c.Read((ushort)((t + c.X) & 0x00FF));
        ushort hi = c.Read((ushort)((t + c.X + 1) & 0x00FF));
        c.AddrAbs = (ushort)((hi << 8) | lo);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte IndirectYMode(Cpu6502 c)
    {
        ushort t = c.Read(c.Pc++);
        ushort lo = c.Read((ushort)(t & 0x00FF));
        ushort hi = c.Read((ushort)((t + 1) & 0x00FF));
        c.AddrAbs = (ushort)(((hi << 8) | lo) + c.Y);
        return (byte)((c.AddrAbs & 0xFF00) != (hi << 8) ? 1 : 0);
    }

    // ==========================================
    // Opcodes
    // ==========================================
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ADC(Cpu6502 c)
    {
        c.Fetch();
        ushort temp = (ushort)(c.A + c.Fetched + c.GetFlag(Flags.C));
        c.SetFlag(Flags.C, temp > 255);
        c.SetFlag(Flags.Z, (temp & 0x00FF) == 0);
        c.SetFlag(Flags.N, (temp & 0x80) != 0);
        c.SetFlag(Flags.V, (~(c.A ^ c.Fetched) & (c.A ^ temp) & 0x0080) != 0);
        c.A = (byte)(temp & 0x00FF);
        return 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte SBC(Cpu6502 c)
    {
        c.Fetch();
        ushort value = (ushort)(c.Fetched ^ 0x00FF);
        ushort temp = (ushort)(c.A + value + c.GetFlag(Flags.C));
        c.SetFlag(Flags.C, (temp & 0xFF00) != 0);
        c.SetFlag(Flags.Z, (temp & 0x00FF) == 0);
        c.SetFlag(Flags.N, (temp & 0x80) != 0);
        c.SetFlag(Flags.V, ((temp ^ c.A) & (temp ^ value) & 0x0080) != 0);
        c.A = (byte)(temp & 0x00FF);
        return 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte AND(Cpu6502 c)
    {
        c.Fetch();
        c.A &= c.Fetched;
        c.SetFlag(Flags.Z, c.A == 0);
        c.SetFlag(Flags.N, (c.A & 0x80) != 0);
        return 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ORA(Cpu6502 c)
    {
        c.Fetch();
        c.A |= c.Fetched;
        c.SetFlag(Flags.Z, c.A == 0);
        c.SetFlag(Flags.N, (c.A & 0x80) != 0);
        return 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte EOR(Cpu6502 c)
    {
        c.Fetch();
        c.A ^= c.Fetched;
        c.SetFlag(Flags.Z, c.A == 0);
        c.SetFlag(Flags.N, (c.A & 0x80) != 0);
        return 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ASL(Cpu6502 c)
    {
        c.Fetch();
        ushort temp = (ushort)(c.Fetched << 1);
        c.SetFlag(Flags.C, (temp & 0xFF00) != 0);
        c.SetFlag(Flags.Z, (temp & 0x00FF) == 0);
        c.SetFlag(Flags.N, (temp & 0x80) != 0);

        if (c._lookup[c.Opcode].IsImplied)
            c.A = (byte)(temp & 0x00FF);
        else
            c.Write(c.AddrAbs, (byte)(temp & 0x00FF));
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte LSR(Cpu6502 c)
    {
        c.Fetch();
        c.SetFlag(Flags.C, (c.Fetched & 0x01) != 0);
        ushort temp = (ushort)(c.Fetched >> 1);
        c.SetFlag(Flags.Z, (temp & 0x00FF) == 0);
        c.SetFlag(Flags.N, (temp & 0x80) != 0);

        if (c._lookup[c.Opcode].IsImplied)
            c.A = (byte)(temp & 0x00FF);
        else
            c.Write(c.AddrAbs, (byte)(temp & 0x00FF));
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ROL(Cpu6502 c)
    {
        c.Fetch();
        ushort temp = (ushort)((c.Fetched << 1) | c.GetFlag(Flags.C));
        c.SetFlag(Flags.C, (temp & 0xFF00) != 0);
        c.SetFlag(Flags.Z, (temp & 0x00FF) == 0);
        c.SetFlag(Flags.N, (temp & 0x80) != 0);

        if (c._lookup[c.Opcode].IsImplied)
            c.A = (byte)(temp & 0x00FF);
        else
            c.Write(c.AddrAbs, (byte)(temp & 0x00FF));
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ROR(Cpu6502 c)
    {
        c.Fetch();
        ushort temp = (ushort)((c.Fetched >> 1) | (c.GetFlag(Flags.C) << 7));
        c.SetFlag(Flags.C, (c.Fetched & 0x01) != 0);
        c.SetFlag(Flags.Z, (temp & 0x00FF) == 0);
        c.SetFlag(Flags.N, (temp & 0x80) != 0);

        if (c._lookup[c.Opcode].IsImplied)
            c.A = (byte)(temp & 0x00FF);
        else
            c.Write(c.AddrAbs, (byte)(temp & 0x00FF));
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte BCC(Cpu6502 c)
    {
        if (c.GetFlag(Flags.C) == 0)
        {
            c.Cycles++;
            c.AddrAbs = (ushort)(c.Pc + c.AddrRel);
            if ((c.AddrAbs & 0xFF00) != (c.Pc & 0xFF00))
                c.Cycles++;
            c.Pc = c.AddrAbs;
        }
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte BCS(Cpu6502 c)
    {
        if (c.GetFlag(Flags.C) == 1)
        {
            c.Cycles++;
            c.AddrAbs = (ushort)(c.Pc + c.AddrRel);
            if ((c.AddrAbs & 0xFF00) != (c.Pc & 0xFF00))
                c.Cycles++;
            c.Pc = c.AddrAbs;
        }
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte BEQ(Cpu6502 c)
    {
        if (c.GetFlag(Flags.Z) == 1)
        {
            c.Cycles++;
            c.AddrAbs = (ushort)(c.Pc + c.AddrRel);
            if ((c.AddrAbs & 0xFF00) != (c.Pc & 0xFF00))
                c.Cycles++;
            c.Pc = c.AddrAbs;
        }
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte BNE(Cpu6502 c)
    {
        if (c.GetFlag(Flags.Z) == 0)
        {
            c.Cycles++;
            c.AddrAbs = (ushort)(c.Pc + c.AddrRel);
            if ((c.AddrAbs & 0xFF00) != (c.Pc & 0xFF00))
                c.Cycles++;
            c.Pc = c.AddrAbs;
        }
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte BMI(Cpu6502 c)
    {
        if (c.GetFlag(Flags.N) == 1)
        {
            c.Cycles++;
            c.AddrAbs = (ushort)(c.Pc + c.AddrRel);
            if ((c.AddrAbs & 0xFF00) != (c.Pc & 0xFF00))
                c.Cycles++;
            c.Pc = c.AddrAbs;
        }
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte BPL(Cpu6502 c)
    {
        if (c.GetFlag(Flags.N) == 0)
        {
            c.Cycles++;
            c.AddrAbs = (ushort)(c.Pc + c.AddrRel);
            if ((c.AddrAbs & 0xFF00) != (c.Pc & 0xFF00))
                c.Cycles++;
            c.Pc = c.AddrAbs;
        }
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte BVC(Cpu6502 c)
    {
        if (c.GetFlag(Flags.V) == 0)
        {
            c.Cycles++;
            c.AddrAbs = (ushort)(c.Pc + c.AddrRel);
            if ((c.AddrAbs & 0xFF00) != (c.Pc & 0xFF00))
                c.Cycles++;
            c.Pc = c.AddrAbs;
        }
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte BVS(Cpu6502 c)
    {
        if (c.GetFlag(Flags.V) == 1)
        {
            c.Cycles++;
            c.AddrAbs = (ushort)(c.Pc + c.AddrRel);
            if ((c.AddrAbs & 0xFF00) != (c.Pc & 0xFF00))
                c.Cycles++;
            c.Pc = c.AddrAbs;
        }
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte BIT(Cpu6502 c)
    {
        c.Fetch();
        byte temp = (byte)(c.A & c.Fetched);
        c.SetFlag(Flags.Z, temp == 0);
        c.SetFlag(Flags.N, (c.Fetched & (1 << 7)) != 0);
        c.SetFlag(Flags.V, (c.Fetched & (1 << 6)) != 0);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte BRK(Cpu6502 c)
    {
        c.Pc++;
        c.Write((ushort)(0x0100 + c.Sp), (byte)((c.Pc >> 8) & 0xFF));
        c.Sp--;
        c.Write((ushort)(0x0100 + c.Sp), (byte)(c.Pc & 0xFF));
        c.Sp--;

        byte statusToPush = (byte)(c.St | (byte)Flags.B | (byte)Flags.U);
        c.Write((ushort)(0x0100 + c.Sp), statusToPush);
        c.Sp--;

        c.SetFlag(Flags.I, true);
        c.Pc = (ushort)(c.Read(0xFFFE) | (c.Read(0xFFFF) << 8));
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte CLC(Cpu6502 c) { c.SetFlag(Flags.C, false); return 0; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte CLD(Cpu6502 c) { c.SetFlag(Flags.D, false); return 0; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte CLI(Cpu6502 c) { c.SetFlag(Flags.I, false); return 0; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte CLV(Cpu6502 c) { c.SetFlag(Flags.V, false); return 0; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte SEC(Cpu6502 c) { c.SetFlag(Flags.C, true); return 0; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte SED(Cpu6502 c) { c.SetFlag(Flags.D, true); return 0; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte SEI(Cpu6502 c) { c.SetFlag(Flags.I, true); return 0; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte CMP(Cpu6502 c)
    {
        c.Fetch();
        ushort temp = (ushort)(c.A - c.Fetched);
        c.SetFlag(Flags.C, c.A >= c.Fetched);
        c.SetFlag(Flags.Z, (temp & 0x00FF) == 0);
        c.SetFlag(Flags.N, (temp & 0x80) != 0);
        return 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte CPX(Cpu6502 c)
    {
        c.Fetch();
        ushort temp = (ushort)(c.X - c.Fetched);
        c.SetFlag(Flags.C, c.X >= c.Fetched);
        c.SetFlag(Flags.Z, (temp & 0x00FF) == 0);
        c.SetFlag(Flags.N, (temp & 0x80) != 0);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte CPY(Cpu6502 c)
    {
        c.Fetch();
        ushort temp = (ushort)(c.Y - c.Fetched);
        c.SetFlag(Flags.C, c.Y >= c.Fetched);
        c.SetFlag(Flags.Z, (temp & 0x00FF) == 0);
        c.SetFlag(Flags.N, (temp & 0x80) != 0);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte DEC(Cpu6502 c)
    {
        c.Fetch();
        ushort temp = (ushort)(c.Fetched - 1);
        c.Write(c.AddrAbs, (byte)(temp & 0x00FF));
        c.SetFlag(Flags.Z, (temp & 0x00FF) == 0);
        c.SetFlag(Flags.N, (temp & 0x80) != 0);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte DEX(Cpu6502 c)
    {
        c.X--;
        c.SetFlag(Flags.Z, c.X == 0);
        c.SetFlag(Flags.N, (c.X & 0x80) != 0);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte DEY(Cpu6502 c)
    {
        c.Y--;
        c.SetFlag(Flags.Z, c.Y == 0);
        c.SetFlag(Flags.N, (c.Y & 0x80) != 0);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte INC(Cpu6502 c)
    {
        c.Fetch();
        ushort temp = (ushort)(c.Fetched + 1);
        c.Write(c.AddrAbs, (byte)(temp & 0x00FF));
        c.SetFlag(Flags.Z, (temp & 0x00FF) == 0);
        c.SetFlag(Flags.N, (temp & 0x80) != 0);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte INX(Cpu6502 c)
    {
        c.X++;
        c.SetFlag(Flags.Z, c.X == 0);
        c.SetFlag(Flags.N, (c.X & 0x80) != 0);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte INY(Cpu6502 c)
    {
        c.Y++;
        c.SetFlag(Flags.Z, c.Y == 0);
        c.SetFlag(Flags.N, (c.Y & 0x80) != 0);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte JMP(Cpu6502 c)
    {
        c.Pc = c.AddrAbs;
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte JSR(Cpu6502 c)
    {
        c.Pc--;
        c.Write((ushort)(0x0100 + c.Sp), (byte)((c.Pc >> 8) & 0xFF));
        c.Sp--;
        c.Write((ushort)(0x0100 + c.Sp), (byte)(c.Pc & 0xFF));
        c.Sp--;
        c.Pc = c.AddrAbs;
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte LDA(Cpu6502 c)
    {
        c.Fetch();
        c.A = c.Fetched;
        c.SetFlag(Flags.Z, c.A == 0);
        c.SetFlag(Flags.N, (c.A & 0x80) != 0);
        return 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte LDX(Cpu6502 c)
    {
        c.Fetch();
        c.X = c.Fetched;
        c.SetFlag(Flags.Z, c.X == 0);
        c.SetFlag(Flags.N, (c.X & 0x80) != 0);
        return 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte LDY(Cpu6502 c)
    {
        c.Fetch();
        c.Y = c.Fetched;
        c.SetFlag(Flags.Z, c.Y == 0);
        c.SetFlag(Flags.N, (c.Y & 0x80) != 0);
        return 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte NOP(Cpu6502 c) => 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte PHA(Cpu6502 c)
    {
        c.Write((ushort)(0x0100 + c.Sp), c.A);
        c.Sp--;
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte PHP(Cpu6502 c)
    {
        c.Write((ushort)(0x0100 + c.Sp), (byte)(c.St | (byte)Flags.B | (byte)Flags.U));
        c.Sp--;
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte PLA(Cpu6502 c)
    {
        c.Sp++;
        c.A = c.Read((ushort)(0x0100 + c.Sp));
        c.SetFlag(Flags.Z, c.A == 0);
        c.SetFlag(Flags.N, (c.A & 0x80) != 0);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte PLP(Cpu6502 c)
    {
        c.Sp++;
        c.St = c.Read((ushort)(0x0100 + c.Sp));
        c.SetFlag(Flags.U, true);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte RTI(Cpu6502 c)
    {
        c.Sp++;
        c.St = c.Read((ushort)(0x0100 + c.Sp));
        c.SetFlag(Flags.U, true);
        c.SetFlag(Flags.B, false);

        c.Sp++;
        ushort lo = c.Read((ushort)(0x0100 + c.Sp));
        c.Sp++;
        ushort hi = c.Read((ushort)(0x0100 + c.Sp));
        c.Pc = (ushort)((hi << 8) | lo);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte RTS(Cpu6502 c)
    {
        c.Sp++;
        ushort lo = c.Read((ushort)(0x0100 + c.Sp));
        c.Sp++;
        ushort hi = c.Read((ushort)(0x0100 + c.Sp));
        c.Pc = (ushort)(((hi << 8) | lo) + 1);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte STA(Cpu6502 c)
    {
        c.Write(c.AddrAbs, c.A);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte STX(Cpu6502 c)
    {
        c.Write(c.AddrAbs, c.X);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte STY(Cpu6502 c)
    {
        c.Write(c.AddrAbs, c.Y);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte TAX(Cpu6502 c)
    {
        c.X = c.A;
        c.SetFlag(Flags.Z, c.X == 0);
        c.SetFlag(Flags.N, (c.X & 0x80) != 0);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte TAY(Cpu6502 c)
    {
        c.Y = c.A;
        c.SetFlag(Flags.Z, c.Y == 0);
        c.SetFlag(Flags.N, (c.Y & 0x80) != 0);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte TSX(Cpu6502 c)
    {
        c.X = c.Sp;
        c.SetFlag(Flags.Z, c.X == 0);
        c.SetFlag(Flags.N, (c.X & 0x80) != 0);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte TXA(Cpu6502 c)
    {
        c.A = c.X;
        c.SetFlag(Flags.Z, c.A == 0);
        c.SetFlag(Flags.N, (c.A & 0x80) != 0);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte TXS(Cpu6502 c)
    {
        c.Sp = c.X;
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte TYA(Cpu6502 c)
    {
        c.A = c.Y;
        c.SetFlag(Flags.Z, c.A == 0);
        c.SetFlag(Flags.N, (c.A & 0x80) != 0);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte XXX(Cpu6502 c) => 0;

    private static Instruction[] CreateLookupTable()
    {
        return
        [
            new("BRK", &BRK, &ImmediateMode, 7),
            new("ORA", &ORA, &IndirectXMode, 6),
            new("???", &XXX, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 8),
            new("???", &NOP, &ImpliedMode, 3),
            new("ORA", &ORA, &ZeroPageMode, 3),
            new("ASL", &ASL, &ZeroPageMode, 5),
            new("???", &XXX, &ImpliedMode, 5),
            new("PHP", &PHP, &ImpliedMode, 3),
            new("ORA", &ORA, &ImmediateMode, 2),
            new("ASL", &ASL, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 2),
            new("???", &NOP, &ImpliedMode, 4),
            new("ORA", &ORA, &AbsoluteMode, 4),
            new("ASL", &ASL, &AbsoluteMode, 6),
            new("???", &XXX, &ImpliedMode, 6),

            new("BPL", &BPL, &RelativeMode, 2),
            new("ORA", &ORA, &IndirectYMode, 5),
            new("???", &XXX, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 8),
            new("???", &NOP, &ImpliedMode, 4),
            new("ORA", &ORA, &ZeroPageXMode, 4),
            new("ASL", &ASL, &ZeroPageXMode, 6),
            new("???", &XXX, &ImpliedMode, 6),
            new("CLC", &CLC, &ImpliedMode, 2),
            new("ORA", &ORA, &AbsoluteYMode, 4),
            new("???", &NOP, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 7),
            new("???", &NOP, &ImpliedMode, 4),
            new("ORA", &ORA, &AbsoluteXMode, 4),
            new("ASL", &ASL, &AbsoluteXMode, 7),
            new("???", &XXX, &ImpliedMode, 7),

            new("JSR", &JSR, &AbsoluteMode, 6),
            new("AND", &AND, &IndirectXMode, 6),
            new("???", &XXX, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 8),
            new("BIT", &BIT, &ZeroPageMode, 3),
            new("AND", &AND, &ZeroPageMode, 3),
            new("ROL", &ROL, &ZeroPageMode, 5),
            new("???", &XXX, &ImpliedMode, 5),
            new("PLP", &PLP, &ImpliedMode, 4),
            new("AND", &AND, &ImmediateMode, 2),
            new("ROL", &ROL, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 2),
            new("BIT", &BIT, &AbsoluteMode, 4),
            new("AND", &AND, &AbsoluteMode, 4),
            new("ROL", &ROL, &AbsoluteMode, 6),
            new("???", &XXX, &ImpliedMode, 6),

            new("BMI", &BMI, &RelativeMode, 2),
            new("AND", &AND, &IndirectYMode, 5),
            new("???", &XXX, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 8),
            new("???", &NOP, &ImpliedMode, 4),
            new("AND", &AND, &ZeroPageXMode, 4),
            new("ROL", &ROL, &ZeroPageXMode, 6),
            new("???", &XXX, &ImpliedMode, 6),
            new("SEC", &SEC, &ImpliedMode, 2),
            new("AND", &AND, &AbsoluteYMode, 4),
            new("???", &NOP, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 7),
            new("???", &NOP, &ImpliedMode, 4),
            new("AND", &AND, &AbsoluteXMode, 4),
            new("ROL", &ROL, &AbsoluteXMode, 7),
            new("???", &XXX, &ImpliedMode, 7),

            new("RTI", &RTI, &ImpliedMode, 6),
            new("EOR", &EOR, &IndirectXMode, 6),
            new("???", &XXX, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 8),
            new("???", &NOP, &ImpliedMode, 3),
            new("EOR", &EOR, &ZeroPageMode, 3),
            new("LSR", &LSR, &ZeroPageMode, 5),
            new("???", &XXX, &ImpliedMode, 5),
            new("PHA", &PHA, &ImpliedMode, 3),
            new("EOR", &EOR, &ImmediateMode, 2),
            new("LSR", &LSR, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 2),
            new("JMP", &JMP, &AbsoluteMode, 3),
            new("EOR", &EOR, &AbsoluteMode, 4),
            new("LSR", &LSR, &AbsoluteMode, 6),
            new("???", &XXX, &ImpliedMode, 6),

            new("BVC", &BVC, &RelativeMode, 2),
            new("EOR", &EOR, &IndirectYMode, 5),
            new("???", &XXX, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 8),
            new("???", &NOP, &ImpliedMode, 4),
            new("EOR", &EOR, &ZeroPageXMode, 4),
            new("LSR", &LSR, &ZeroPageXMode, 6),
            new("???", &XXX, &ImpliedMode, 6),
            new("CLI", &CLI, &ImpliedMode, 2),
            new("EOR", &EOR, &AbsoluteYMode, 4),
            new("???", &NOP, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 7),
            new("???", &NOP, &ImpliedMode, 4),
            new("EOR", &EOR, &AbsoluteXMode, 4),
            new("LSR", &LSR, &AbsoluteXMode, 7),
            new("???", &XXX, &ImpliedMode, 7),

            new("RTS", &RTS, &ImpliedMode, 6),
            new("ADC", &ADC, &IndirectXMode, 6),
            new("???", &XXX, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 8),
            new("???", &NOP, &ImpliedMode, 3),
            new("ADC", &ADC, &ZeroPageMode, 3),
            new("ROR", &ROR, &ZeroPageMode, 5),
            new("???", &XXX, &ImpliedMode, 5),
            new("PLA", &PLA, &ImpliedMode, 4),
            new("ADC", &ADC, &ImmediateMode, 2),
            new("ROR", &ROR, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 2),
            new("JMP", &JMP, &IndirectMode, 5),
            new("ADC", &ADC, &AbsoluteMode, 4),
            new("ROR", &ROR, &AbsoluteMode, 6),
            new("???", &XXX, &ImpliedMode, 6),

            new("BVS", &BVS, &RelativeMode, 2),
            new("ADC", &ADC, &IndirectYMode, 5),
            new("???", &XXX, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 8),
            new("???", &NOP, &ImpliedMode, 4),
            new("ADC", &ADC, &ZeroPageXMode, 4),
            new("ROR", &ROR, &ZeroPageXMode, 6),
            new("???", &XXX, &ImpliedMode, 6),
            new("SEI", &SEI, &ImpliedMode, 2),
            new("ADC", &ADC, &AbsoluteYMode, 4),
            new("???", &NOP, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 7),
            new("???", &NOP, &ImpliedMode, 4),
            new("ADC", &ADC, &AbsoluteXMode, 4),
            new("ROR", &ROR, &AbsoluteXMode, 7),
            new("???", &XXX, &ImpliedMode, 7),

            new("???", &NOP, &ImpliedMode, 2),
            new("STA", &STA, &IndirectXMode, 6),
            new("???", &NOP, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 6),
            new("STY", &STY, &ZeroPageMode, 3),
            new("STA", &STA, &ZeroPageMode, 3),
            new("STX", &STX, &ZeroPageMode, 3),
            new("???", &XXX, &ImpliedMode, 3),
            new("DEY", &DEY, &ImpliedMode, 2),
            new("???", &NOP, &ImpliedMode, 2),
            new("TXA", &TXA, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 2),
            new("STY", &STY, &AbsoluteMode, 4),
            new("STA", &STA, &AbsoluteMode, 4),
            new("STX", &STX, &AbsoluteMode, 4),
            new("???", &XXX, &ImpliedMode, 4),

            new("BCC", &BCC, &RelativeMode, 2),
            new("STA", &STA, &IndirectYMode, 6),
            new("???", &XXX, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 6),
            new("STY", &STY, &ZeroPageXMode, 4),
            new("STA", &STA, &ZeroPageXMode, 4),
            new("STX", &STX, &ZeroPageYMode, 4),
            new("???", &XXX, &ImpliedMode, 4),
            new("TYA", &TYA, &ImpliedMode, 2),
            new("STA", &STA, &AbsoluteYMode, 5),
            new("TXS", &TXS, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 5),
            new("???", &NOP, &ImpliedMode, 5),
            new("STA", &STA, &AbsoluteXMode, 5),
            new("???", &XXX, &ImpliedMode, 5),
            new("???", &XXX, &ImpliedMode, 5),

            new("LDY", &LDY, &ImmediateMode, 2),
            new("LDA", &LDA, &IndirectXMode, 6),
            new("LDX", &LDX, &ImmediateMode, 2),
            new("???", &XXX, &ImpliedMode, 6),
            new("LDY", &LDY, &ZeroPageMode, 3),
            new("LDA", &LDA, &ZeroPageMode, 3),
            new("LDX", &LDX, &ZeroPageMode, 3),
            new("???", &XXX, &ImpliedMode, 3),
            new("TAY", &TAY, &ImpliedMode, 2),
            new("LDA", &LDA, &ImmediateMode, 2),
            new("TAX", &TAX, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 2),
            new("LDY", &LDY, &AbsoluteMode, 4),
            new("LDA", &LDA, &AbsoluteMode, 4),
            new("LDX", &LDX, &AbsoluteMode, 4),
            new("???", &XXX, &ImpliedMode, 4),

            new("BCS", &BCS, &RelativeMode, 2),
            new("LDA", &LDA, &IndirectYMode, 5),
            new("???", &XXX, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 5),
            new("LDY", &LDY, &ZeroPageXMode, 4),
            new("LDA", &LDA, &ZeroPageXMode, 4),
            new("LDX", &LDX, &ZeroPageYMode, 4),
            new("???", &XXX, &ImpliedMode, 4),
            new("CLV", &CLV, &ImpliedMode, 2),
            new("LDA", &LDA, &AbsoluteYMode, 4),
            new("TSX", &TSX, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 4),
            new("LDY", &LDY, &AbsoluteXMode, 4),
            new("LDA", &LDA, &AbsoluteXMode, 4),
            new("LDX", &LDX, &AbsoluteYMode, 4),
            new("???", &XXX, &ImpliedMode, 4),

            new("CPY", &CPY, &ImmediateMode, 2),
            new("CMP", &CMP, &IndirectXMode, 6),
            new("???", &NOP, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 8),
            new("CPY", &CPY, &ZeroPageMode, 3),
            new("CMP", &CMP, &ZeroPageMode, 3),
            new("DEC", &DEC, &ZeroPageMode, 5),
            new("???", &XXX, &ImpliedMode, 5),
            new("INY", &INY, &ImpliedMode, 2),
            new("CMP", &CMP, &ImmediateMode, 2),
            new("DEX", &DEX, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 2),
            new("CPY", &CPY, &AbsoluteMode, 4),
            new("CMP", &CMP, &AbsoluteMode, 4),
            new("DEC", &DEC, &AbsoluteMode, 6),
            new("???", &XXX, &ImpliedMode, 6),

            new("BNE", &BNE, &RelativeMode, 2),
            new("CMP", &CMP, &IndirectYMode, 5),
            new("???", &XXX, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 8),
            new("???", &NOP, &ImpliedMode, 4),
            new("CMP", &CMP, &ZeroPageXMode, 4),
            new("DEC", &DEC, &ZeroPageXMode, 6),
            new("???", &XXX, &ImpliedMode, 6),
            new("CLD", &CLD, &ImpliedMode, 2),
            new("CMP", &CMP, &AbsoluteYMode, 4),
            new("NOP", &NOP, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 7),
            new("???", &NOP, &ImpliedMode, 4),
            new("CMP", &CMP, &AbsoluteXMode, 4),
            new("DEC", &DEC, &AbsoluteXMode, 7),
            new("???", &XXX, &ImpliedMode, 7),

            new("CPX", &CPX, &ImmediateMode, 2),
            new("SBC", &SBC, &IndirectXMode, 6),
            new("???", &NOP, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 8),
            new("CPX", &CPX, &ZeroPageMode, 3),
            new("SBC", &SBC, &ZeroPageMode, 3),
            new("INC", &INC, &ZeroPageMode, 5),
            new("???", &XXX, &ImpliedMode, 5),
            new("INX", &INX, &ImpliedMode, 2),
            new("SBC", &SBC, &ImmediateMode, 2),
            new("NOP", &NOP, &ImpliedMode, 2),
            new("???", &SBC, &ImpliedMode, 2),
            new("CPX", &CPX, &AbsoluteMode, 4),
            new("SBC", &SBC, &AbsoluteMode, 4),
            new("INC", &INC, &AbsoluteMode, 6),
            new("???", &XXX, &ImpliedMode, 6),

            new("BEQ", &BEQ, &RelativeMode, 2),
            new("SBC", &SBC, &IndirectYMode, 5),
            new("???", &XXX, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 8),
            new("???", &NOP, &ImpliedMode, 4),
            new("SBC", &SBC, &ZeroPageXMode, 4),
            new("INC", &INC, &ZeroPageXMode, 6),
            new("???", &XXX, &ImpliedMode, 6),
            new("SED", &SED, &ImpliedMode, 2),
            new("SBC", &SBC, &AbsoluteYMode, 4),
            new("NOP", &NOP, &ImpliedMode, 2),
            new("???", &XXX, &ImpliedMode, 7),
            new("???", &NOP, &ImpliedMode, 4),
            new("SBC", &SBC, &AbsoluteXMode, 4),
            new("INC", &INC, &AbsoluteXMode, 7),
            new("???", &XXX, &ImpliedMode, 7)
        ];
    }
}
