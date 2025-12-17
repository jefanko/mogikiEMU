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

  for (int i = 0; i < 4; i++)
    pPRGBank[i] = 0;
  for (int i = 0; i < 8; i++) {
    pCHRBank[i] = 0;
    pRegister[i] = 0;
  }

  // Fixed banks
  pPRGBank[0] = 0 * 0x2000;
  pPRGBank[1] = 1 * 0x2000;
  pPRGBank[2] = (nPRGBanks * 2 - 2) * 0x2000;
  pPRGBank[3] = (nPRGBanks * 2 - 1) * 0x2000;
}

bool Mapper_004::cpuMapRead(uint16_t addr, uint32_t &mapped_addr) {
  if (addr >= 0x6000 && addr <= 0x7FFF) {
    // PRG RAM
    mapped_addr = 0xFFFFFFFF;
    return true;
  }

  if (addr >= 0x8000 && addr <= 0x9FFF) {
    mapped_addr = pPRGBank[0] + (addr & 0x1FFF);
    return true;
  }
  if (addr >= 0xA000 && addr <= 0xBFFF) {
    mapped_addr = pPRGBank[1] + (addr & 0x1FFF);
    return true;
  }
  if (addr >= 0xC000 && addr <= 0xDFFF) {
    mapped_addr = pPRGBank[2] + (addr & 0x1FFF);
    return true;
  }
  if (addr >= 0xE000 && addr <= 0xFFFF) {
    mapped_addr = pPRGBank[3] + (addr & 0x1FFF);
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
      // Bank Select
      nTargetRegister = data & 0x07;
      bPRGBankMode = data & 0x40;
      bCHRInversion = data & 0x80;
    } else {
      // Bank Data
      pRegister[nTargetRegister] = data;

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
      if (bPRGBankMode) {
        pPRGBank[2] = (pRegister[6] & 0x3F) * 0x2000;
        pPRGBank[0] = (nPRGBanks * 2 - 2) * 0x2000;
      } else {
        pPRGBank[0] = (pRegister[6] & 0x3F) * 0x2000;
        pPRGBank[2] = (nPRGBanks * 2 - 2) * 0x2000;
      }
      pPRGBank[1] = (pRegister[7] & 0x3F) * 0x2000;
      pPRGBank[3] = (nPRGBanks * 2 - 1) * 0x2000;
    }
    return false;
  }

  if (addr >= 0xA000 && addr <= 0xBFFF) {
    if (!(addr & 0x0001)) {
      // Mirroring
      mirrorMode = (data & 0x01) ? MIRROR::HORIZONTAL : MIRROR::VERTICAL;
    }
    return false;
  }

  if (addr >= 0xC000 && addr <= 0xDFFF) {
    if (!(addr & 0x0001)) {
      // IRQ Latch
      nIRQReload = data;
    } else {
      // IRQ Reload - set flag to reload counter on next scanline
      nIRQCounter = 0;
      bIRQUpdate = true;
    }
    return false;
  }

  if (addr >= 0xE000 && addr <= 0xFFFF) {
    if (!(addr & 0x0001)) {
      // IRQ Disable
      bIRQEnable = false;
      bIRQActive = false;
    } else {
      // IRQ Enable
      bIRQEnable = true;
    }
    return false;
  }

  return false;
}

bool Mapper_004::ppuMapRead(uint16_t addr, uint32_t &mapped_addr) {
  if (addr < 0x2000) {
    mapped_addr = pCHRBank[(addr >> 10) & 0x07] + (addr & 0x03FF);
    return true;
  }
  return false;
}

bool Mapper_004::ppuMapWrite(uint16_t addr, uint32_t &mapped_addr) {
  return false;
}

void Mapper_004::scanline() {
  if (nIRQCounter == 0 || bIRQUpdate) {
    nIRQCounter = nIRQReload;
    bIRQUpdate = false;
  } else {
    nIRQCounter--;
    if (nIRQCounter == 0 && bIRQEnable) {
      bIRQActive = true;
    }
  }
}
