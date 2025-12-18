#pragma once
#include <cstdint>

enum MIRROR { HORIZONTAL, VERTICAL, ONESCREEN_LO, ONESCREEN_HI };

class Mapper {
public:
  Mapper(uint8_t prgBanks, uint8_t chrBanks);
  virtual ~Mapper();

  // Transform CPU bus address into PRG ROM offset
  virtual bool cpuMapRead(uint16_t addr, uint32_t &mapped_addr) = 0;
  virtual bool cpuMapWrite(uint16_t addr, uint32_t &mapped_addr) = 0;
  virtual bool cpuMapWrite(uint16_t addr, uint32_t &mapped_addr, uint8_t data) {
    return cpuMapWrite(addr, mapped_addr);
  }

  // Transform PPU bus address into CHR ROM offset
  virtual bool ppuMapRead(uint16_t addr, uint32_t &mapped_addr) = 0;
  virtual bool ppuMapWrite(uint16_t addr, uint32_t &mapped_addr) = 0;

  // Custom PPU Read (Data retrieval) - for complex mirroring (MMC5)
  // Returns true if the mapper has provided data directly
  virtual bool ppuReadCustom(uint16_t addr, uint8_t &data) { return false; }

  // Custom PPU Write (Data storage) - for complex mirroring (MMC5)
  virtual bool ppuWriteCustom(uint16_t addr, uint8_t data) { return false; }

  virtual void reset() {}

  // Get current mirroring mode
  virtual MIRROR mirror() { return MIRROR::HORIZONTAL; }

  // IRQ interface (for mappers like MMC3)
  virtual bool irqState() { return false; }
  virtual void irqClear() {}
  virtual void scanline() {}

protected:
  uint8_t nPRGBanks = 0;
  uint8_t nCHRBanks = 0;
};
