#include "Bus.h"

Bus::Bus() {
  for (auto &i : ram)
    i = 0x00;
  cpu.ConnectBus(this);

  // Connect DMC memory read callback
  apu.SetReadCallback(
      [this](uint16_t addr) -> uint8_t { return this->read(addr, true); });
}

Bus::~Bus() {}

void Bus::insertCartridge(const std::shared_ptr<Cartridge> &cartridge) {
  this->cart = cartridge;
  ppu.ConnectCartridge(cartridge);
}

void Bus::reset() {
  cart->reset();
  cpu.reset();
  ppu.reset();
  apu.reset();
  nSystemClockCounter = 0;
}

void Bus::clock() {
  ppu.clock();

  if (nSystemClockCounter % 3 == 0) {
    // APU runs at CPU rate
    apu.clock();

    if (dma_transfer) {
      if (dma_dummy) {
        if (nSystemClockCounter % 2 == 1) {
          dma_dummy = false;
        }
      } else {
        if (nSystemClockCounter % 2 == 0) {
          dma_data = read(dma_page << 8 | dma_addr);
        } else {
          ppu.OAM[dma_addr] = dma_data;
          dma_addr++;

          if (dma_addr == 0x00) {
            dma_transfer = false;
            dma_dummy = true;
          }
        }
      }
    } else {
      cpu.clock();
    }

    // Check mapper IRQ (for MMC3) - Level Triggered
    // If IRQ is asserted by mapper, we signal the CPU.
    // The CPU handle the 'I' flag check and only interrupts on instruction
    // boundaries.
    if (cart->GetIRQState()) {
      cpu.irq();
    }
  }

  if (ppu.nmi) {
    ppu.nmi = false;
    cpu.nmi();
  }

  nSystemClockCounter++;
}

void Bus::write(uint16_t addr, uint8_t data) {
  if (cart->cpuWrite(addr, data)) {
  } else if (addr >= 0x0000 && addr <= 0x1FFF) {
    ram[addr & 0x07FF] = data;
  } else if (addr >= 0x2000 && addr <= 0x3FFF) {
    ppu.cpuWrite(addr & 0x0007, data);
  } else if (addr >= 0x4000 && addr <= 0x4013) {
    apu.cpuWrite(addr, data);
  } else if (addr == 0x4014) {
    dma_page = data;
    dma_addr = 0x00;
    dma_transfer = true;
  } else if (addr == 0x4015) {
    apu.cpuWrite(addr, data);
  } else if (addr >= 0x4016 && addr <= 0x4017) {
    if (addr == 0x4017) {
      apu.cpuWrite(addr, data);
    }
    controller_state[addr & 0x0001] = controller[addr & 0x0001];
  }
}

uint8_t Bus::read(uint16_t addr, bool bReadOnly) {
  uint8_t data = 0x00;
  if (cart->cpuRead(addr, data)) {
  } else if (addr >= 0x0000 && addr <= 0x1FFF) {
    data = ram[addr & 0x07FF];
  } else if (addr >= 0x2000 && addr <= 0x3FFF) {
    data = ppu.cpuRead(addr & 0x0007, bReadOnly);
  } else if (addr == 0x4015) {
    data = apu.cpuRead(addr);
  } else if (addr >= 0x4016 && addr <= 0x4017) {
    data = (controller_state[addr & 0x0001] & 0x80) > 0;
    controller_state[addr & 0x0001] <<= 1;
  }
  return data;
}
