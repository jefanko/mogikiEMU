#include "Mapper_002.h"

Mapper_002::Mapper_002(uint8_t prgBanks, uint8_t chrBanks, MIRROR hwMirror)
    : Mapper(prgBanks, chrBanks), mirrorMode(hwMirror) {
  reset();
}

Mapper_002::~Mapper_002() {}

void Mapper_002::reset() { nPRGBankSelect = 0; }

bool Mapper_002::cpuMapRead(uint16_t addr, uint32_t &mapped_addr) {
  if (addr >= 0x8000 && addr <= 0xBFFF) {
    // Switchable 16KB bank at $8000
    mapped_addr = nPRGBankSelect * 0x4000 + (addr & 0x3FFF);
    return true;
  }

  if (addr >= 0xC000 && addr <= 0xFFFF) {
    // Fixed to last 16KB bank at $C000
    mapped_addr = (nPRGBanks - 1) * 0x4000 + (addr & 0x3FFF);
    return true;
  }

  return false;
}

bool Mapper_002::cpuMapWrite(uint16_t addr, uint32_t &mapped_addr) {
  return false;
}

bool Mapper_002::cpuMapWrite(uint16_t addr, uint32_t &mapped_addr,
                             uint8_t data) {
  if (addr >= 0x8000 && addr <= 0xFFFF) {
    // Bank select - use low bits based on number of banks
    nPRGBankSelect = data & 0x0F;
  }
  return false;
}

bool Mapper_002::ppuMapRead(uint16_t addr, uint32_t &mapped_addr) {
  if (addr < 0x2000) {
    // CHR RAM (UxROM has no CHR ROM, uses RAM)
    mapped_addr = addr;
    return true;
  }
  return false;
}

bool Mapper_002::ppuMapWrite(uint16_t addr, uint32_t &mapped_addr) {
  if (addr < 0x2000) {
    // CHR RAM is writable
    mapped_addr = addr;
    return true;
  }
  return false;
}
