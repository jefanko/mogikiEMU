#include "Mapper_004.h"

Mapper_004::Mapper_004(uint8_t prgBanks, uint8_t chrBanks)
    : Mapper(prgBanks, chrBanks) {
  vRAMStatic.resize(8192, 0);
  reset();
}

Mapper_004::~Mapper_004() {}

void Mapper_004::reset() {
  nTargetRegister = 0;
  bPRGBankMode = false;
  bCHRInversion = false;
  mirrorMode = MIRROR::HORIZONTAL;

  bIRQActive = false;
  bIRQEnable = false;
  bIRQUpdate = false;
  nIRQCounter = 0;
  nIRQReload = 0;

  for (int i = 0; i < 8; i++) {
    pRegister[i] = 0;
  }

  UpdateBanks();
}

void Mapper_004::UpdateBanks() {
  // Update CHR banks
  // R0/R1 select 2KB banks (bit 0 ignored), R2-R5 select 1KB banks
  if (bCHRInversion) {
    // CHR inversion: $0000-$0FFF = 4x 1KB, $1000-$1FFF = 2x 2KB
    pCHRBank[0] = pRegister[2] * 0x0400;
    pCHRBank[1] = pRegister[3] * 0x0400;
    pCHRBank[2] = pRegister[4] * 0x0400;
    pCHRBank[3] = pRegister[5] * 0x0400;
    pCHRBank[4] = (pRegister[0] & 0xFE) * 0x0400;
    pCHRBank[5] = ((pRegister[0] & 0xFE) + 1) * 0x0400;
    pCHRBank[6] = (pRegister[1] & 0xFE) * 0x0400;
    pCHRBank[7] = ((pRegister[1] & 0xFE) + 1) * 0x0400;
  } else {
    // Normal: $0000-$0FFF = 2x 2KB, $1000-$1FFF = 4x 1KB
    pCHRBank[0] = (pRegister[0] & 0xFE) * 0x0400;
    pCHRBank[1] = ((pRegister[0] & 0xFE) + 1) * 0x0400;
    pCHRBank[2] = (pRegister[1] & 0xFE) * 0x0400;
    pCHRBank[3] = ((pRegister[1] & 0xFE) + 1) * 0x0400;
    pCHRBank[4] = pRegister[2] * 0x0400;
    pCHRBank[5] = pRegister[3] * 0x0400;
    pCHRBank[6] = pRegister[4] * 0x0400;
    pCHRBank[7] = pRegister[5] * 0x0400;
  }

  // Update PRG banks
  uint32_t num8k = nPRGBanks * 2;
  if (bPRGBankMode) {
    pPRGBank[0] = (num8k - 2) * 0x2000;
    pPRGBank[1] = (pRegister[7] & 0x3F) * 0x2000;
    pPRGBank[2] = (pRegister[6] & 0x3F) * 0x2000;
    pPRGBank[3] = (num8k - 1) * 0x2000;
  } else {
    pPRGBank[0] = (pRegister[6] & 0x3F) * 0x2000;
    pPRGBank[1] = (pRegister[7] & 0x3F) * 0x2000;
    pPRGBank[2] = (num8k - 2) * 0x2000;
    pPRGBank[3] = (num8k - 1) * 0x2000;
  }
}

bool Mapper_004::cpuMapRead(uint16_t addr, uint32_t &mapped_addr) {
  if (addr >= 0x6000 && addr <= 0x7FFF) {
    // PRG RAM
    mapped_addr = 0xFFFFFFFF;
    return true;
  }

  uint32_t prgRomSize = nPRGBanks * 16384;
  if (prgRomSize == 0) prgRomSize = 16384;

  if (addr >= 0x8000 && addr <= 0x9FFF) {
    mapped_addr = (pPRGBank[0] + (addr & 0x1FFF)) % prgRomSize;
    return true;
  }
  if (addr >= 0xA000 && addr <= 0xBFFF) {
    mapped_addr = (pPRGBank[1] + (addr & 0x1FFF)) % prgRomSize;
    return true;
  }
  if (addr >= 0xC000 && addr <= 0xDFFF) {
    mapped_addr = (pPRGBank[2] + (addr & 0x1FFF)) % prgRomSize;
    return true;
  }
  if (addr >= 0xE000 && addr <= 0xFFFF) {
    mapped_addr = (pPRGBank[3] + (addr & 0x1FFF)) % prgRomSize;
    return true;
  }

  return false;
}

bool Mapper_004::cpuMapWrite(uint16_t addr, uint32_t &mapped_addr) {
  return false;
}

bool Mapper_004::cpuMapWrite(uint16_t addr, uint32_t &mapped_addr,
                             uint8_t data) {
  if (addr >= 0x6000 && addr <= 0x7FFF) {
    mapped_addr = 0xFFFFFFFF;
    vRAMStatic[addr & 0x1FFF] = data;
    return true;
  }

  if (addr >= 0x8000 && addr <= 0x9FFF) {
    if (!(addr & 0x0001)) {
      // Bank Select ($8000)
      nTargetRegister = data & 0x07;
      bPRGBankMode = (data & 0x40) != 0;
      bCHRInversion = (data & 0x80) != 0;
      UpdateBanks();
    } else {
      // Bank Data ($8001)
      pRegister[nTargetRegister] = data;
      UpdateBanks();
    }
    return false;
  }

  if (addr >= 0xA000 && addr <= 0xBFFF) {
    if (!(addr & 0x0001)) {
      // Mirroring ($A000)
      mirrorMode = (data & 0x01) ? MIRROR::HORIZONTAL : MIRROR::VERTICAL;
    }
    return false;
  }

  if (addr >= 0xC000 && addr <= 0xDFFF) {
    if (!(addr & 0x0001)) {
      // IRQ Latch ($C000)
      nIRQReload = data;
    } else {
      // IRQ Reload ($C001)
      bIRQUpdate = true;
    }
    return false;
  }

  if (addr >= 0xE000 && addr <= 0xFFFF) {
    if (!(addr & 0x0001)) {
      // IRQ Disable ($E000)
      bIRQEnable = false;
      bIRQActive = false;
    } else {
      // IRQ Enable ($E001)
      bIRQEnable = true;
    }
    return false;
  }

  return false;
}

bool Mapper_004::ppuMapRead(uint16_t addr, uint32_t &mapped_addr) {
  if (addr < 0x2000) {
    uint32_t chrRomSize = nCHRBanks * 8192;
    if (chrRomSize == 0) chrRomSize = 8192;
    mapped_addr = (pCHRBank[(addr >> 10) & 0x07] + (addr & 0x03FF)) % chrRomSize;
    return true;
  }
  return false;
}

bool Mapper_004::ppuMapWrite(uint16_t addr, uint32_t &mapped_addr) {
  return false;
}

void Mapper_004::scanline() {
  // NESdev MMC3 scanline counter logic:
  // When clocked, if counter is 0 or reload flag is set:
  //   counter = reload value
  //   clear reload flag
  // Else:
  //   counter--
  // If counter is 0 and IRQ is enabled:
  //   trigger IRQ
  if (nIRQCounter == 0 || bIRQUpdate) {
    nIRQCounter = nIRQReload;
    bIRQUpdate = false;
  } else {
    nIRQCounter--;
  }

  if (nIRQCounter == 0 && bIRQEnable) {
    bIRQActive = true;
  }
}
