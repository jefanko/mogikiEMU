#include "Mapper_001.h"

Mapper_001::Mapper_001(uint8_t prgBanks, uint8_t chrBanks)
    : Mapper(prgBanks, chrBanks) {
  // Initialize 8KB PRG RAM (for save data)
  vRAMStatic.resize(8192, 0x00);
  reset();
}

Mapper_001::~Mapper_001() {}

void Mapper_001::reset() {
  nControlRegister = 0x1C;
  nLoadRegister = 0x00;
  nLoadRegisterCount = 0;

  nCHRBankSelect4Lo = 0;
  nCHRBankSelect4Hi = 0;
  nCHRBankSelect8 = 0;

  nPRGBankSelect16Lo = 0;
  nPRGBankSelect16Hi = nPRGBanks - 1;
  nPRGBankSelect32 = 0;

  mirrorMode = MIRROR::HORIZONTAL;
}

MIRROR Mapper_001::mirror() { return mirrorMode; }

bool Mapper_001::cpuMapRead(uint16_t addr, uint32_t &mapped_addr) {
  if (addr >= 0x6000 && addr <= 0x7FFF) {
    // PRG RAM region - return special marker and handle in cartridge
    mapped_addr = 0xFFFFFFFF;
    return true;
  }

  if (addr >= 0x8000) {
    if (nControlRegister & 0x08) {
      // 16KB PRG mode
      if (addr <= 0xBFFF) {
        mapped_addr = nPRGBankSelect16Lo * 0x4000 + (addr & 0x3FFF);
        return true;
      } else {
        mapped_addr = nPRGBankSelect16Hi * 0x4000 + (addr & 0x3FFF);
        return true;
      }
    } else {
      // 32KB PRG mode
      mapped_addr = nPRGBankSelect32 * 0x8000 + (addr & 0x7FFF);
      return true;
    }
  }

  return false;
}

bool Mapper_001::cpuMapWrite(uint16_t addr, uint32_t &mapped_addr,
                             uint8_t data) {
  if (addr >= 0x6000 && addr <= 0x7FFF) {
    // PRG RAM write
    mapped_addr = 0xFFFFFFFF;
    vRAMStatic[addr & 0x1FFF] = data;
    return true;
  }

  if (addr >= 0x8000) {
    if (data & 0x80) {
      // Reset shift register
      nLoadRegister = 0x00;
      nLoadRegisterCount = 0;
      nControlRegister |= 0x0C;
    } else {
      // Load data into shift register
      nLoadRegister >>= 1;
      nLoadRegister |= (data & 0x01) << 4;
      nLoadRegisterCount++;

      if (nLoadRegisterCount == 5) {
        // Determine target register based on address
        uint8_t targetReg = (addr >> 13) & 0x03;

        if (targetReg == 0) {
          // Control register (0x8000-0x9FFF)
          nControlRegister = nLoadRegister & 0x1F;

          // Update mirroring mode (bits 0-1)
          switch (nControlRegister & 0x03) {
          case 0:
            mirrorMode = MIRROR::ONESCREEN_LO;
            break;
          case 1:
            mirrorMode = MIRROR::ONESCREEN_HI;
            break;
          case 2:
            mirrorMode = MIRROR::VERTICAL;
            break;
          case 3:
            mirrorMode = MIRROR::HORIZONTAL;
            break;
          }
        } else if (targetReg == 1) {
          // CHR bank 0 (0xA000-0xBFFF)
          if (nControlRegister & 0x10) {
            // 4KB CHR mode
            nCHRBankSelect4Lo = nLoadRegister & 0x1F;
          } else {
            // 8KB CHR mode
            nCHRBankSelect8 = nLoadRegister & 0x1E;
          }
        } else if (targetReg == 2) {
          // CHR bank 1 (0xC000-0xDFFF)
          if (nControlRegister & 0x10) {
            // 4KB CHR mode only
            nCHRBankSelect4Hi = nLoadRegister & 0x1F;
          }
        } else if (targetReg == 3) {
          // PRG bank (0xE000-0xFFFF)
          uint8_t prgMode = (nControlRegister >> 2) & 0x03;

          if (prgMode == 0 || prgMode == 1) {
            // 32KB mode - ignore low bit
            nPRGBankSelect32 = (nLoadRegister & 0x0E) >> 1;
          } else if (prgMode == 2) {
            // Fix first bank at $8000, switch $C000
            nPRGBankSelect16Lo = 0;
            nPRGBankSelect16Hi = nLoadRegister & 0x0F;
          } else if (prgMode == 3) {
            // Switch $8000, fix last bank at $C000
            nPRGBankSelect16Lo = nLoadRegister & 0x0F;
            nPRGBankSelect16Hi = nPRGBanks - 1;
          }
        }

        // Reset shift register
        nLoadRegister = 0x00;
        nLoadRegisterCount = 0;
      }
    }
    return false; // Don't write to PRG ROM
  }

  return false;
}

bool Mapper_001::cpuMapWrite(uint16_t addr, uint32_t &mapped_addr) {
  return false;
}

bool Mapper_001::ppuMapRead(uint16_t addr, uint32_t &mapped_addr) {
  if (addr < 0x2000) {
    if (nCHRBanks == 0) {
      // CHR RAM - direct mapping
      mapped_addr = addr;
      return true;
    } else {
      if (nControlRegister & 0x10) {
        // 4KB CHR mode
        if (addr <= 0x0FFF) {
          mapped_addr = nCHRBankSelect4Lo * 0x1000 + (addr & 0x0FFF);
          return true;
        } else {
          mapped_addr = nCHRBankSelect4Hi * 0x1000 + (addr & 0x0FFF);
          return true;
        }
      } else {
        // 8KB CHR mode
        mapped_addr = nCHRBankSelect8 * 0x1000 + addr;
        return true;
      }
    }
  }
  return false;
}

bool Mapper_001::ppuMapWrite(uint16_t addr, uint32_t &mapped_addr) {
  if (addr < 0x2000) {
    if (nCHRBanks == 0) {
      // CHR RAM - writable
      mapped_addr = addr;
      return true;
    }
  }
  return false;
}
