#pragma once
#include "Mapper.h"
#include <vector>

class Mapper_005 : public Mapper {
public:
  Mapper_005(uint8_t prgBanks, uint8_t chrBanks);
  ~Mapper_005();

  bool cpuMapRead(uint16_t addr, uint32_t &mapped_addr) override;
  bool cpuMapWrite(uint16_t addr, uint32_t &mapped_addr) override;
  bool cpuMapWrite(uint16_t addr, uint32_t &mapped_addr, uint8_t data) override;
  bool ppuMapRead(uint16_t addr, uint32_t &mapped_addr) override;
  bool ppuMapWrite(uint16_t addr, uint32_t &mapped_addr) override;

  // Custom PPU Read for Fill Mode / Complex Mirroring
  bool ppuReadCustom(uint16_t addr, uint8_t &data) override;
  bool ppuWriteCustom(uint16_t addr, uint8_t data) override;

  void reset() override;
  MIRROR mirror() override;

  // IRQ interface
  bool irqState() override { return bIRQActive && bIRQEnable; }
  void irqClear() override { bIRQActive = false; }
  void scanline() override;

  // PRG RAM access
  std::vector<uint8_t> &GetPRGRAM() { return vPRGRAM; }

  // Internal register read (for CPU reads in $5000-$5FFF range)
  uint8_t ReadRegister(uint16_t addr);

  // ExRAM access
  std::vector<uint8_t> &GetExRAM() { return vExRAM; }

private:
  // Configuration registers
  uint8_t prgMode = 3;        // $5100: PRG banking mode (0-3)
  uint8_t chrMode = 3;        // $5101: CHR banking mode (0-3)
  uint8_t prgRamProtect1 = 0; // $5102
  uint8_t prgRamProtect2 = 0; // $5103
  uint8_t exRamMode = 0;      // $5104: Extended RAM mode
  uint8_t ntMapping = 0;      // $5105: Nametable mapping

  // Fill mode
  uint8_t fillTile = 0;  // $5106
  uint8_t fillColor = 0; // $5107

  // PRG bank registers ($5113-$5117)
  uint8_t prgBankReg[5] = {0}; // Index 0 = $5113, ... Index 4 = $5117

  // CHR bank registers ($5120-$512B)
  uint16_t chrBankReg[12] = {0}; // $5120-$512B
  uint8_t chrUpperBits = 0;      // $5130

  // Track which CHR bank set was last written (for 8x8 sprite mode)
  // true = upper half ($5128-$512B), false = lower half ($5120-$5127)
  bool lastCHRBankWriteIsUpperHalf = false;

  // Track if we're in 8x16 sprite mode (read from PPU $2000)
  bool bSprite8x16Mode = false;

  // Multiplier ($5205-$5206)
  uint8_t multiplierA = 0xFF;
  uint8_t multiplierB = 0xFF;

  // Scanline IRQ
  uint8_t irqScanline = 0; // $5203: Target scanline
  bool bIRQEnable = false; // $5204 bit 7
  bool bIRQActive = false; // IRQ pending flag
  bool bInFrame = false;   // In-frame flag
  uint8_t scanlineCounter = 0;

  // For scanline detection (3 consecutive reads from same nametable addr)
  uint16_t lastPPUAddr = 0;
  uint8_t matchCount = 0;

  // For CHR bank switching (Sprite vs Background)
  // When a nametable read is detected, we expect 2 pattern table reads for
  // background
  int bg_fetches_remaining = 0;
  uint16_t lastBgTileAddr = 0;
  uint8_t lastBgTileExRam = 0;

  // Mirroring
  MIRROR mirrorMode = MIRROR::VERTICAL;

  // PRG RAM (up to 64KB for compatibility, though games use 8-32KB)
  std::vector<uint8_t> vPRGRAM;

  // Internal Extended RAM (1KB)
  std::vector<uint8_t> vExRAM;

  // Internal Nametable RAM (2KB - replaces NES internal CIRAM)
  std::vector<uint8_t> internalNametable;

  // Helper functions
  uint32_t GetPRGBankOffset(int bank, int bankSize);
  uint32_t GetCHRBankOffset(int bank, int bankSize);
  bool IsPRGRAMEnabled();
};
