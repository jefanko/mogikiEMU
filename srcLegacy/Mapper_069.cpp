#include "Mapper_069.h"
#include <fstream>

// Track if we've logged bank changes (only log first few)
static int bankChangeCount = 0;

Mapper_069::Mapper_069(uint8_t prgBanks, uint8_t chrBanks)
    : Mapper(prgBanks, chrBanks) {
  vPRGRAM.resize(8192, 0);
  reset();
}

Mapper_069::~Mapper_069() {}

void Mapper_069::reset() {
  commandRegister = 0;

  // Initialize PRG banks
  prgBank[0] = 0;
  prgBank[1] = 0;
  prgBank[2] = 0;
  prgBank[3] = 0;
  prgRamEnable = false;
  prgRamSelect = false;

  // Initialize CHR banks to 1:1 mapping
  for (int i = 0; i < 8; i++) {
    chrBank[i] = i;
  }

  mirrorMode = MIRROR::VERTICAL;

  bIRQEnable = false;
  bIRQCounterEnable = false;
  bIRQActive = false;
  irqCounter = 0;
}

void Mapper_069::CountIRQ() {
  if (bIRQCounterEnable) {
    irqCounter--;
    if (irqCounter == 0xFFFF && bIRQEnable) {
      bIRQActive = true;
    }
  }
}

bool Mapper_069::cpuMapRead(uint16_t addr, uint32_t &mapped_addr) {
  uint32_t prgRomSize = nPRGBanks * 16384;

  // $6000-$7FFF: PRG RAM or ROM bank
  if (addr >= 0x6000 && addr <= 0x7FFF) {
    if (prgRamSelect) {
      // PRG RAM
      mapped_addr = 0xFFFFFFFF;
      return true;
    } else {
      // PRG ROM
      uint32_t bank = prgBank[0] & 0x3F;
      mapped_addr = ((bank * 8192) + (addr & 0x1FFF)) % prgRomSize;
      return true;
    }
  }

  // $8000-$9FFF: Switchable 8KB PRG ROM
  if (addr >= 0x8000 && addr <= 0x9FFF) {
    uint32_t bank = prgBank[1] & 0x3F;
    mapped_addr = ((bank * 8192) + (addr & 0x1FFF)) % prgRomSize;
    return true;
  }

  // $A000-$BFFF: Switchable 8KB PRG ROM
  if (addr >= 0xA000 && addr <= 0xBFFF) {
    uint32_t bank = prgBank[2] & 0x3F;
    mapped_addr = ((bank * 8192) + (addr & 0x1FFF)) % prgRomSize;
    return true;
  }

  // $C000-$DFFF: Switchable 8KB PRG ROM
  if (addr >= 0xC000 && addr <= 0xDFFF) {
    uint32_t bank = prgBank[3] & 0x3F;
    mapped_addr = ((bank * 8192) + (addr & 0x1FFF)) % prgRomSize;
    return true;
  }

  // $E000-$FFFF: Fixed to last 8KB bank
  if (addr >= 0xE000) {
    uint32_t lastBank = (nPRGBanks * 2) - 1; // Last 8KB bank
    mapped_addr = ((lastBank * 8192) + (addr & 0x1FFF)) % prgRomSize;
    return true;
  }

  return false;
}

bool Mapper_069::cpuMapWrite(uint16_t addr, uint32_t &mapped_addr) {
  return false;
}

bool Mapper_069::cpuMapWrite(uint16_t addr, uint32_t &mapped_addr,
                             uint8_t data) {
  // $6000-$7FFF: PRG RAM write
  if (addr >= 0x6000 && addr <= 0x7FFF) {
    if (prgRamSelect && prgRamEnable) {
      mapped_addr = 0xFFFFFFFF;
      return true;
    }
    return false;
  }

  // $8000-$9FFF: Command Register
  if (addr >= 0x8000 && addr <= 0x9FFF) {
    commandRegister = data & 0x0F;
    return false;
  }

  // $A000-$BFFF: Parameter Register
  if (addr >= 0xA000 && addr <= 0xBFFF) {
    switch (commandRegister) {
    case 0x0:
    case 0x1:
    case 0x2:
    case 0x3:
    case 0x4:
    case 0x5:
    case 0x6:
    case 0x7:
      // CHR Bank 0-7
      chrBank[commandRegister] = data;
      if (bankChangeCount < 20) {
        std::ofstream debugLog("nes_debug.log", std::ios::app);
        debugLog << "CHR Bank " << (int)commandRegister << " = " << (int)data
                 << std::endl;
        debugLog.close();
        bankChangeCount++;
      }
      break;

    case 0x8:
      // PRG Bank at $6000-$7FFF
      prgRamEnable = (data & 0x80) != 0;
      prgRamSelect = (data & 0x40) != 0;
      prgBank[0] = data & 0x3F;
      break;

    case 0x9:
      // PRG Bank at $8000-$9FFF
      prgBank[1] = data & 0x3F;
      break;

    case 0xA:
      // PRG Bank at $A000-$BFFF
      prgBank[2] = data & 0x3F;
      break;

    case 0xB:
      // PRG Bank at $C000-$DFFF
      prgBank[3] = data & 0x3F;
      break;

    case 0xC:
      // Mirroring
      switch (data & 0x03) {
      case 0:
        mirrorMode = MIRROR::VERTICAL;
        break;
      case 1:
        mirrorMode = MIRROR::HORIZONTAL;
        break;
      case 2:
        mirrorMode = MIRROR::ONESCREEN_LO;
        break;
      case 3:
        mirrorMode = MIRROR::ONESCREEN_HI;
        break;
      }
      break;

    case 0xD:
      // IRQ Control
      bIRQEnable = (data & 0x01) != 0;
      bIRQCounterEnable = (data & 0x80) != 0;
      if (!bIRQEnable) {
        bIRQActive = false;
      }
      break;

    case 0xE:
      // IRQ Counter Low
      irqCounter = (irqCounter & 0xFF00) | data;
      break;

    case 0xF:
      // IRQ Counter High
      irqCounter = (irqCounter & 0x00FF) | (data << 8);
      break;
    }
    return false;
  }

  return false;
}

bool Mapper_069::ppuMapRead(uint16_t addr, uint32_t &mapped_addr) {
  if (addr < 0x2000) {
    uint32_t chrRomSize = nCHRBanks * 8192;
    if (chrRomSize == 0)
      chrRomSize = 8192; // CHR RAM

    int bankIndex = (addr >> 10) & 0x07;
    uint32_t bank = chrBank[bankIndex];
    mapped_addr = ((bank * 1024) + (addr & 0x03FF)) % chrRomSize;
    return true;
  }
  return false;
}

bool Mapper_069::ppuMapWrite(uint16_t addr, uint32_t &mapped_addr) {
  // CHR RAM writes
  if (addr < 0x2000 && nCHRBanks == 0) {
    mapped_addr = addr;
    return true;
  }
  return false;
}
