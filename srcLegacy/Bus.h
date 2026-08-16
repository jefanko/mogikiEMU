#pragma once
#include "APU2A03.h"
#include "CPU6502.h"
#include "Cartridge.h"
#include "PPU2C02.h"
#include <array>
#include <cstdint>
#include <memory>

class Bus {
public:
  Bus();
  ~Bus();

public: // Devices
  CPU6502 cpu;
  PPU2C02 ppu;
  APU2A03 apu;
  std::array<uint8_t, 2048> ram;
  std::shared_ptr<Cartridge> cart;

  // Controllers
  uint8_t controller[2] = {0, 0};

public: // Bus Read/Write
  void write(uint16_t addr, uint8_t data);
  uint8_t read(uint16_t addr, bool bReadOnly = false);

  void insertCartridge(const std::shared_ptr<Cartridge> &cartridge);
  void reset();
  void clock();

  // Get audio sample for SDL callback
  double GetAudioSample() { return apu.GetOutputSample(); }

private:
  uint32_t nSystemClockCounter = 0;

  // Controller state
  uint8_t controller_state[2] = {0, 0};

  // DMA
  uint8_t dma_page = 0x00;
  uint8_t dma_addr = 0x00;
  uint8_t dma_data = 0x00;
  bool dma_transfer = false;
  bool dma_dummy = true;

  // Mapper IRQ edge detection
  bool bPrevMapperIRQ = false;
};
