using System.Runtime.CompilerServices;

namespace Mogiki.Core.Apu;

/// <summary>
/// Ricoh 2A03 APU (Audio Processing Unit) for NES.
/// </summary>
public sealed class Apu2A03
{
    private static readonly byte[] LengthTable =
    [
        10, 254, 20, 2,  40, 4,  80, 6,  160, 8,  60, 10, 14, 12, 26, 14,
        12, 16,  24, 18, 48, 20, 96, 22, 192, 24, 72, 26, 16, 28, 32, 30
    ];

    private static readonly byte[][] DutyTable =
    [
        [0, 0, 0, 0, 0, 0, 0, 1], // 12.5%
        [0, 0, 0, 0, 0, 0, 1, 1], // 25%
        [0, 0, 0, 0, 1, 1, 1, 1], // 50%
        [1, 1, 1, 1, 1, 1, 0, 0]  // 75%
    ];

    private static readonly byte[] TriangleTable =
    [
        15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5,  4,  3,  2,  1,  0,
        0,  1,  2,  3,  4,  5,  6, 7, 8, 9, 10, 11, 12, 13, 14, 15
    ];

    private static readonly ushort[] NoiseTimerTable =
    [
        4, 8, 16, 32, 64, 96, 128, 160, 202, 254, 380, 508, 762, 1016, 2034, 4068
    ];

    private static readonly ushort[] DmcRateTable =
    [
        428, 380, 340, 320, 286, 254, 226, 214, 190, 160, 142, 128, 106, 84, 72, 54
    ];

    public Func<ushort, byte>? CpuReadCallback { get; set; }

    private ulong _clockCounter;
    private bool _frameCounterMode; // false = 4-step, true = 5-step
    private bool _frameIRQInhibit;
    private bool _frameIRQ;
    private ushort _frameCounter;
    private bool _dmcIRQ;

    public struct PulseChannel
    {
        public bool Enabled;
        public byte Duty;
        public bool LengthHalt;
        public bool ConstantVolume;
        public byte EnvelopePeriod;

        public bool SweepEnabled;
        public byte SweepPeriod;
        public bool SweepNegate;
        public byte SweepShift;

        public ushort TimerPeriod;
        public byte LengthCounter;

        public ushort TimerValue;
        public byte DutyPosition;

        public bool EnvelopeStart;
        public byte EnvelopeDivider;
        public byte EnvelopeDecay;

        public bool SweepReload;
        public byte SweepDivider;
        public ushort SweepTargetPeriod;
        public bool SweepMuting;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClockTimer()
        {
            if (TimerValue == 0)
            {
                TimerValue = TimerPeriod;
                DutyPosition = (byte)((DutyPosition + 1) & 7);
            }
            else
            {
                TimerValue--;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClockEnvelope()
        {
            if (EnvelopeStart)
            {
                EnvelopeStart = false;
                EnvelopeDecay = 15;
                EnvelopeDivider = EnvelopePeriod;
            }
            else
            {
                if (EnvelopeDivider == 0)
                {
                    EnvelopeDivider = EnvelopePeriod;
                    if (EnvelopeDecay > 0)
                        EnvelopeDecay--;
                    else if (LengthHalt)
                        EnvelopeDecay = 15;
                }
                else
                {
                    EnvelopeDivider--;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClockLengthCounter()
        {
            if (LengthCounter > 0 && !LengthHalt)
                LengthCounter--;
        }

        public void UpdateSweepTargetPeriod(bool isChannel1)
        {
            ushort delta = (ushort)(TimerPeriod >> SweepShift);
            if (SweepNegate)
            {
                SweepTargetPeriod = (ushort)(TimerPeriod - delta);
                if (isChannel1)
                    SweepTargetPeriod--;
            }
            else
            {
                SweepTargetPeriod = (ushort)(TimerPeriod + delta);
            }

            SweepMuting = TimerPeriod < 8 || (!SweepNegate && SweepTargetPeriod > 0x7FF);
        }

        public void ClockSweep(bool isChannel1)
        {
            UpdateSweepTargetPeriod(isChannel1);

            if (SweepDivider == 0 && SweepEnabled && !SweepMuting && SweepShift > 0)
                TimerPeriod = SweepTargetPeriod;

            if (SweepDivider == 0 || SweepReload)
            {
                SweepDivider = SweepPeriod;
                SweepReload = false;
            }
            else
            {
                SweepDivider--;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly byte Output()
        {
            if (!Enabled || LengthCounter == 0 || SweepMuting || DutyTable[Duty][DutyPosition] == 0)
                return 0;

            return ConstantVolume ? EnvelopePeriod : EnvelopeDecay;
        }
    }

    public struct TriangleChannel
    {
        public bool Enabled;
        public bool LengthHalt;
        public byte LinearCounterReload;
        public ushort TimerPeriod;
        public byte LengthCounter;

        public ushort TimerValue;
        public byte SequencerPosition;
        public byte LinearCounter;
        public bool LinearCounterReloadFlag;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClockTimer()
        {
            if (TimerValue == 0)
            {
                TimerValue = TimerPeriod;
                if (LengthCounter > 0 && LinearCounter > 0)
                    SequencerPosition = (byte)((SequencerPosition + 1) & 31);
            }
            else
            {
                TimerValue--;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClockLinearCounter()
        {
            if (LinearCounterReloadFlag)
                LinearCounter = LinearCounterReload;
            else if (LinearCounter > 0)
                LinearCounter--;

            if (!LengthHalt)
                LinearCounterReloadFlag = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClockLengthCounter()
        {
            if (LengthCounter > 0 && !LengthHalt)
                LengthCounter--;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly byte Output()
        {
            if (!Enabled || LengthCounter == 0 || LinearCounter == 0)
                return 0;
            if (TimerPeriod < 2)
                return 7;

            return TriangleTable[SequencerPosition];
        }
    }

    public struct NoiseChannel
    {
        public bool Enabled;
        public bool LengthHalt;
        public bool ConstantVolume;
        public byte EnvelopePeriod;
        public bool Mode;
        public byte NoisePeriod;
        public byte LengthCounter;

        public ushort TimerValue;
        public ushort ShiftRegister;

        public bool EnvelopeStart;
        public byte EnvelopeDivider;
        public byte EnvelopeDecay;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClockTimer()
        {
            if (TimerValue == 0)
            {
                TimerValue = NoiseTimerTable[NoisePeriod];
                byte bit = Mode ? (byte)6 : (byte)1;
                ushort feedback = (ushort)((ShiftRegister & 1) ^ ((ShiftRegister >> bit) & 1));
                ShiftRegister = (ushort)((ShiftRegister >> 1) | (feedback << 14));
            }
            else
            {
                TimerValue--;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClockEnvelope()
        {
            if (EnvelopeStart)
            {
                EnvelopeStart = false;
                EnvelopeDecay = 15;
                EnvelopeDivider = EnvelopePeriod;
            }
            else
            {
                if (EnvelopeDivider == 0)
                {
                    EnvelopeDivider = EnvelopePeriod;
                    if (EnvelopeDecay > 0)
                        EnvelopeDecay--;
                    else if (LengthHalt)
                        EnvelopeDecay = 15;
                }
                else
                {
                    EnvelopeDivider--;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClockLengthCounter()
        {
            if (LengthCounter > 0 && !LengthHalt)
                LengthCounter--;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly byte Output()
        {
            if (!Enabled || LengthCounter == 0 || (ShiftRegister & 1) != 0)
                return 0;

            return ConstantVolume ? EnvelopePeriod : EnvelopeDecay;
        }
    }

    public struct DmcChannel
    {
        public bool Enabled;
        public bool IrqEnabled;
        public bool Loop;
        public byte RateIndex;
        public byte DirectLoad;
        public ushort SampleAddress;
        public ushort SampleLength;

        public byte OutputLevel;
        public ushort CurrentAddress;
        public ushort BytesRemaining;
        public byte SampleBuffer;
        public bool SampleBufferEmpty;

        public byte ShiftRegister;
        public byte BitsRemaining;
        public bool SilenceFlag;

        public ushort TimerValue;
        public ushort TimerPeriod;
        public bool IrqFlag;

        public void ClockTimer(Func<ushort, byte>? readCallback, ref bool irqOut)
        {
            if (TimerValue == 0)
            {
                TimerValue = TimerPeriod;

                if (!SilenceFlag)
                {
                    if ((ShiftRegister & 1) != 0)
                    {
                        if (OutputLevel <= 125) OutputLevel += 2;
                    }
                    else
                    {
                        if (OutputLevel >= 2) OutputLevel -= 2;
                    }
                    ShiftRegister >>= 1;
                }

                BitsRemaining--;
                if (BitsRemaining == 0)
                {
                    BitsRemaining = 8;
                    if (SampleBufferEmpty)
                    {
                        SilenceFlag = true;
                    }
                    else
                    {
                        SilenceFlag = false;
                        ShiftRegister = SampleBuffer;
                        SampleBufferEmpty = true;
                    }
                }

                if (SampleBufferEmpty && BytesRemaining > 0 && readCallback != null)
                {
                    SampleBuffer = readCallback(CurrentAddress);
                    SampleBufferEmpty = false;

                    CurrentAddress++;
                    if (CurrentAddress == 0)
                        CurrentAddress = 0x8000;

                    BytesRemaining--;
                    if (BytesRemaining == 0)
                    {
                        if (Loop)
                        {
                            Restart();
                        }
                        else if (IrqEnabled)
                        {
                            IrqFlag = true;
                            irqOut = true;
                        }
                    }
                }
            }
            else
            {
                TimerValue--;
            }
        }

        public void Restart()
        {
            CurrentAddress = SampleAddress;
            BytesRemaining = SampleLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly byte Output() => OutputLevel;
    }

    public PulseChannel Pulse1;
    public PulseChannel Pulse2;
    public TriangleChannel Triangle;
    public NoiseChannel Noise;
    public DmcChannel Dmc;

    public Apu2A03()
    {
        Reset();
    }

    public void Reset()
    {
        _clockCounter = 0;
        _frameCounter = 0;
        _frameCounterMode = false;
        _frameIRQInhibit = false;
        _frameIRQ = false;

        Pulse1 = default;
        Pulse2 = default;
        Triangle = default;
        Noise = default;
        Noise.ShiftRegister = 1;
        Dmc = default;
        _dmcIRQ = false;
    }

    public bool GetIRQ() => _frameIRQ || _dmcIRQ;
    public void ClearFrameIRQ() => _frameIRQ = false;

    private void ClockQuarterFrame()
    {
        Pulse1.ClockEnvelope();
        Pulse2.ClockEnvelope();
        Triangle.ClockLinearCounter();
        Noise.ClockEnvelope();
    }

    private void ClockHalfFrame()
    {
        Pulse1.ClockLengthCounter();
        Pulse1.ClockSweep(true);
        Pulse2.ClockLengthCounter();
        Pulse2.ClockSweep(false);
        Triangle.ClockLengthCounter();
        Noise.ClockLengthCounter();
    }

    public void Clock()
    {
        Triangle.ClockTimer();

        if ((_clockCounter % 2) == 0)
        {
            Pulse1.ClockTimer();
            Pulse2.ClockTimer();
            Noise.ClockTimer();
        }

        Dmc.ClockTimer(CpuReadCallback, ref _dmcIRQ);

        if (!_frameCounterMode)
        {
            if (_frameCounter == 7457)
            {
                ClockQuarterFrame();
            }
            else if (_frameCounter == 14913)
            {
                ClockQuarterFrame();
                ClockHalfFrame();
            }
            else if (_frameCounter == 22371)
            {
                ClockQuarterFrame();
            }
            else if (_frameCounter == 29829)
            {
                ClockQuarterFrame();
                ClockHalfFrame();
                if (!_frameIRQInhibit)
                    _frameIRQ = true;
                _frameCounter = 0;
            }
        }
        else
        {
            if (_frameCounter == 7457)
            {
                ClockQuarterFrame();
            }
            else if (_frameCounter == 14913)
            {
                ClockQuarterFrame();
                ClockHalfFrame();
            }
            else if (_frameCounter == 22371)
            {
                ClockQuarterFrame();
            }
            else if (_frameCounter == 37281)
            {
                ClockQuarterFrame();
                ClockHalfFrame();
                _frameCounter = 0;
            }
        }

        _frameCounter++;
        _clockCounter++;
    }

    public void CpuWrite(ushort addr, byte data)
    {
        switch (addr)
        {
            case 0x4000:
                Pulse1.Duty = (byte)((data >> 6) & 0x03);
                Pulse1.LengthHalt = (data & 0x20) != 0;
                Pulse1.ConstantVolume = (data & 0x10) != 0;
                Pulse1.EnvelopePeriod = (byte)(data & 0x0F);
                break;
            case 0x4001:
                Pulse1.SweepEnabled = (data & 0x80) != 0;
                Pulse1.SweepPeriod = (byte)((data >> 4) & 0x07);
                Pulse1.SweepNegate = (data & 0x08) != 0;
                Pulse1.SweepShift = (byte)(data & 0x07);
                Pulse1.SweepReload = true;
                break;
            case 0x4002:
                Pulse1.TimerPeriod = (ushort)((Pulse1.TimerPeriod & 0xFF00) | data);
                break;
            case 0x4003:
                Pulse1.TimerPeriod = (ushort)((Pulse1.TimerPeriod & 0x00FF) | ((data & 0x07) << 8));
                Pulse1.TimerValue = Pulse1.TimerPeriod;
                if (Pulse1.Enabled)
                    Pulse1.LengthCounter = LengthTable[(data >> 3) & 0x1F];
                Pulse1.EnvelopeStart = true;
                Pulse1.DutyPosition = 0;
                break;

            case 0x4004:
                Pulse2.Duty = (byte)((data >> 6) & 0x03);
                Pulse2.LengthHalt = (data & 0x20) != 0;
                Pulse2.ConstantVolume = (data & 0x10) != 0;
                Pulse2.EnvelopePeriod = (byte)(data & 0x0F);
                break;
            case 0x4005:
                Pulse2.SweepEnabled = (data & 0x80) != 0;
                Pulse2.SweepPeriod = (byte)((data >> 4) & 0x07);
                Pulse2.SweepNegate = (data & 0x08) != 0;
                Pulse2.SweepShift = (byte)(data & 0x07);
                Pulse2.SweepReload = true;
                break;
            case 0x4006:
                Pulse2.TimerPeriod = (ushort)((Pulse2.TimerPeriod & 0xFF00) | data);
                break;
            case 0x4007:
                Pulse2.TimerPeriod = (ushort)((Pulse2.TimerPeriod & 0x00FF) | ((data & 0x07) << 8));
                Pulse2.TimerValue = Pulse2.TimerPeriod;
                if (Pulse2.Enabled)
                    Pulse2.LengthCounter = LengthTable[(data >> 3) & 0x1F];
                Pulse2.EnvelopeStart = true;
                Pulse2.DutyPosition = 0;
                break;

            case 0x4008:
                Triangle.LengthHalt = (data & 0x80) != 0;
                Triangle.LinearCounterReload = (byte)(data & 0x7F);
                break;
            case 0x400A:
                Triangle.TimerPeriod = (ushort)((Triangle.TimerPeriod & 0xFF00) | data);
                break;
            case 0x400B:
                Triangle.TimerPeriod = (ushort)((Triangle.TimerPeriod & 0x00FF) | ((data & 0x07) << 8));
                Triangle.TimerValue = Triangle.TimerPeriod;
                if (Triangle.Enabled)
                    Triangle.LengthCounter = LengthTable[(data >> 3) & 0x1F];
                Triangle.LinearCounterReloadFlag = true;
                break;

            case 0x400C:
                Noise.LengthHalt = (data & 0x20) != 0;
                Noise.ConstantVolume = (data & 0x10) != 0;
                Noise.EnvelopePeriod = (byte)(data & 0x0F);
                break;
            case 0x400E:
                Noise.Mode = (data & 0x80) != 0;
                Noise.NoisePeriod = (byte)(data & 0x0F);
                break;
            case 0x400F:
                if (Noise.Enabled)
                    Noise.LengthCounter = LengthTable[(data >> 3) & 0x1F];
                Noise.EnvelopeStart = true;
                break;

            case 0x4010:
                Dmc.IrqEnabled = (data & 0x80) != 0;
                Dmc.Loop = (data & 0x40) != 0;
                Dmc.RateIndex = (byte)(data & 0x0F);
                Dmc.TimerPeriod = DmcRateTable[Dmc.RateIndex];
                if (!Dmc.IrqEnabled)
                {
                    Dmc.IrqFlag = false;
                    _dmcIRQ = false;
                }
                break;
            case 0x4011:
                Dmc.OutputLevel = (byte)(data & 0x7F);
                break;
            case 0x4012:
                Dmc.SampleAddress = (ushort)(0xC000 | (data << 6));
                break;
            case 0x4013:
                Dmc.SampleLength = (ushort)((data << 4) | 1);
                break;

            case 0x4015:
                Pulse1.Enabled = (data & 0x01) != 0;
                Pulse2.Enabled = (data & 0x02) != 0;
                Triangle.Enabled = (data & 0x04) != 0;
                Noise.Enabled = (data & 0x08) != 0;
                Dmc.Enabled = (data & 0x10) != 0;

                if (!Pulse1.Enabled) Pulse1.LengthCounter = 0;
                if (!Pulse2.Enabled) Pulse2.LengthCounter = 0;
                if (!Triangle.Enabled) Triangle.LengthCounter = 0;
                if (!Noise.Enabled) Noise.LengthCounter = 0;

                Dmc.IrqFlag = false;
                _dmcIRQ = false;

                if (Dmc.Enabled)
                {
                    if (Dmc.BytesRemaining == 0)
                        Dmc.Restart();
                }
                else
                {
                    Dmc.BytesRemaining = 0;
                }
                break;

            case 0x4017:
                _frameCounterMode = (data & 0x80) != 0;
                _frameIRQInhibit = (data & 0x40) != 0;

                if (_frameIRQInhibit)
                    _frameIRQ = false;

                _frameCounter = 0;
                if (_frameCounterMode)
                {
                    ClockQuarterFrame();
                    ClockHalfFrame();
                }
                break;
        }
    }

    public byte CpuRead(ushort addr)
    {
        byte data = 0;
        if (addr == 0x4015)
        {
            if (Pulse1.LengthCounter > 0) data |= 0x01;
            if (Pulse2.LengthCounter > 0) data |= 0x02;
            if (Triangle.LengthCounter > 0) data |= 0x04;
            if (Noise.LengthCounter > 0) data |= 0x08;
            if (Dmc.BytesRemaining > 0) data |= 0x10;
            if (_frameIRQ) data |= 0x40;
            if (Dmc.IrqFlag) data |= 0x80;

            _frameIRQ = false;
        }
        return data;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetOutputSample()
    {
        byte p1 = Pulse1.Output();
        byte p2 = Pulse2.Output();
        byte tri = Triangle.Output();
        byte noi = Noise.Output();
        byte dm = Dmc.Output();

        double pulseOut = 0.0;
        if (p1 + p2 > 0)
        {
            pulseOut = 95.88 / ((8128.0 / (p1 + p2)) + 100.0);
        }

        double tndOut = 0.0;
        double tndSum = tri / 8227.0 + noi / 12241.0 + dm / 22638.0;
        if (tndSum > 0.0)
        {
            tndOut = 159.79 / ((1.0 / tndSum) + 100.0);
        }

        double output = pulseOut + tndOut;
        return (output * 2.0) - 1.0;
    }
}
