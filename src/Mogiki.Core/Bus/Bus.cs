using System.Runtime.CompilerServices;
using Mogiki.Core.Apu;
using Mogiki.Core.Cartridge;
using Mogiki.Core.Cpu;
using Mogiki.Core.Ppu;

namespace Mogiki.Core.Bus;

/// <summary>
/// Main system bus interconnecting CPU, PPU, APU, RAM, Cartridge, and Controllers.
/// </summary>
public sealed class Bus
{
    public Cpu6502 Cpu { get; }
    public Ppu2C02 Ppu { get; }
    public Apu2A03 Apu { get; }
    public byte[] Ram { get; } = new byte[2048];
    public Cartridge.Cartridge? Cartridge { get; private set; }

    // Controllers
    public byte[] Controller { get; } = new byte[2];
    private readonly byte[] _controllerState = new byte[2];
    private bool _controllerStrobe;

    // Clock
    private uint _systemClockCounter;

    // DMA
    private byte _dmaPage;
    private byte _dmaAddr;
    private byte _dmaData;
    private bool _dmaTransfer;
    private bool _dmaDummy = true;

    public Bus()
    {
        Cpu = new Cpu6502 { Bus = this };
        Ppu = new Ppu2C02();
        Apu = new Apu2A03 { CpuReadCallback = addr => Read(addr, true) };
    }

    public void InsertCartridge(Cartridge.Cartridge cartridge)
    {
        Cartridge = cartridge;
        Ppu.Cartridge = cartridge;
    }

    public void RemoveCartridge()
    {
        Cartridge = null;
        Ppu.Cartridge = null;
    }

    public void Reset()
    {
        Cartridge?.Reset();
        Cpu.Reset();
        Ppu.Reset();
        Apu.Reset();
        _systemClockCounter = 0;
        _dmaTransfer = false;
        _dmaDummy = true;
        _controllerStrobe = false;
        _controllerState[0] = 0;
        _controllerState[1] = 0;
    }

    public void Clock()
    {
        Ppu.Clock();

        if ((_systemClockCounter % 3) == 0)
        {
            // APU runs at CPU rate
            Apu.Clock();

            if (_dmaTransfer)
            {
                if (_dmaDummy)
                {
                    if ((_systemClockCounter % 2) == 1)
                    {
                        _dmaDummy = false;
                    }
                }
                else
                {
                    if ((_systemClockCounter % 2) == 0)
                    {
                        _dmaData = Read((ushort)((_dmaPage << 8) | _dmaAddr));
                    }
                    else
                    {
                        Ppu.OAM[_dmaAddr] = _dmaData;
                        _dmaAddr++;

                        if (_dmaAddr == 0x00)
                        {
                            _dmaTransfer = false;
                            _dmaDummy = true;
                        }
                    }
                }
            }
            else
            {
                if (Cartridge != null && Cartridge.IrqState)
                {
                    Cpu.Irq();
                }
                Cpu.Clock();
            }
        }

        if (Ppu.Nmi)
        {
            Ppu.Nmi = false;
            Cpu.Nmi();
        }

        _systemClockCounter++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetAudioSample() => Apu.GetOutputSample();

    public void Write(ushort addr, byte data)
    {
        Cartridge?.CpuSnoopWrite(addr, data);

        if (Cartridge != null && Cartridge.CpuWrite(addr, data))
        {
            return;
        }

        if (addr is >= 0x0000 and <= 0x1FFF)
        {
            Ram[addr & 0x07FF] = data;
        }
        else if (addr is >= 0x2000 and <= 0x3FFF)
        {
            Ppu.CpuWrite((ushort)(addr & 0x0007), data);
        }
        else if (addr is >= 0x4000 and <= 0x4013)
        {
            Apu.CpuWrite(addr, data);
        }
        else if (addr == 0x4014) // OAM DMA
        {
            _dmaPage = data;
            _dmaAddr = 0x00;
            _dmaTransfer = true;
        }
        else if (addr == 0x4015)
        {
            Apu.CpuWrite(addr, data);
        }
        else if (addr is >= 0x4016 and <= 0x4017)
        {
            if (addr == 0x4016)
            {
                bool strobe = (data & 0x01) != 0;

                // A high strobe continuously exposes the current A button.
                // The falling edge latches both controller ports for serial
                // reads, matching the NES controller protocol.
                if (strobe || _controllerStrobe)
                {
                    LatchControllers();
                }

                _controllerStrobe = strobe;
            }
            else
            {
                Apu.CpuWrite(addr, data);
            }
        }
    }

    public byte Read(ushort addr, bool readOnly = false)
    {
        if (Cartridge != null && addr is 0xFFFA or 0xFFFB)
        {
            Cartridge.CpuSnoopRead(addr);
        }

        if (Cartridge != null && Cartridge.CpuRead(addr, out byte data))
        {
            return data;
        }

        if (addr is >= 0x0000 and <= 0x1FFF)
        {
            return Ram[addr & 0x07FF];
        }

        if (addr is >= 0x2000 and <= 0x3FFF)
        {
            return Ppu.CpuRead((ushort)(addr & 0x0007), readOnly);
        }

        if (addr == 0x4015)
        {
            return Apu.CpuRead(addr);
        }

        if (addr is >= 0x4016 and <= 0x4017)
        {
            int port = addr & 0x0001;
            byte val = (byte)(((_controllerStrobe ? Controller[port] : _controllerState[port]) & 0x80) != 0 ? 1 : 0);

            if (!_controllerStrobe)
            {
                // Once all eight buttons have been shifted out, real NES
                // controllers keep returning 1 rather than 0.
                _controllerState[port] = (byte)((_controllerState[port] << 1) | 0x01);
            }

            return val;
        }

        return 0x00;
    }

    private void LatchControllers()
    {
        _controllerState[0] = Controller[0];
        _controllerState[1] = Controller[1];
    }
}
