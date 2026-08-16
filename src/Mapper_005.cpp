#include "Mapper_005.h"
#include <cstring>

Mapper_005::Mapper_005(uint8_t prgBanks, uint8_t chrBanks)
    : Mapper(prgBanks, chrBanks) {
  // PRG RAM - 64KB for maximum compatibility
  vPRGRAM.resize(64 * 1024, 0);
  // Internal Extended RAM - 1KB
  vExRAM.resize(1024, 0);
  // Internal Nametable RAM - 2KB (replaces NES internal CIRAM)
  internalNametable.resize(2048, 0);
  reset();
}

Mapper_005::~Mapper_005() {}

void Mapper_005::reset() {
  prgMode = 3;
  chrMode = 3; // 1KB CHR pages - what most MMC5 games use
  prgRamProtect1 = 0;
  prgRamProtect2 = 0;
  exRamMode = 0;
  ntMapping = 0;
  fillTile = 0;
  fillColor = 0;
  chrUpperBits = 0;
  multiplierA = 0xFF;
  multiplierB = 0xFF;

  bSprite8x16Mode = false;
  bRenderEnable = false;
  lastCHRBankWriteIsUpperHalf = false;

  irqScanline = 0;
  bIRQEnable = false;
  bIRQActive = false;
  bInFrame = false;
  scanlineCounter = 0;
  lastPPUAddr = 0;
  matchCount = 0;
  lastBgTileAddr = 0;
  lastBgTileExRam = 0;
  bg_fetches_remaining = 0;

  mirrorMode = MIRROR::VERTICAL;

  // Initialize PRG bank registers
  // Per nesdev: $5117 has a reliable power-on value of $FF
  // This maps the last 8KB bank to $E000-$FFFF
  prgBankReg[0] = 0x00; // $5113: PRG RAM bank 0
  prgBankReg[1] = 0x80; // $5114: ROM bank 0 (for mode 3)
  prgBankReg[2] = 0x80; // $5115: ROM bank 0 (for mode 2/3)
  prgBankReg[3] = 0x80; // $5116: ROM bank 0
  prgBankReg[4] =
      0xFF; // $5117: last 8KB ROM bank (0x7F gives bank 127 → wraps)

  // Initialize CHR bank registers
  for (int i = 0; i < 12; i++) {
    chrBankReg[i] = i; // Map 1:1 initially
  }
}

bool Mapper_005::IsPRGRAMEnabled() {
  // PRG RAM is writable when $5102 = 0x02 and $5103 = 0x01
  return (prgRamProtect1 == 0x02) && (prgRamProtect2 == 0x01);
}

uint32_t Mapper_005::GetPRGBankOffset(int bank, int bankSize) {
  // Bank value with RAM/ROM bit stripped
  uint32_t bankVal = bank & 0x7F;
  uint32_t offset = bankVal * bankSize;
  // Wrap to available PRG ROM
  uint32_t prgSize = nPRGBanks * 16384;
  if (prgSize > 0) {
    offset = offset % prgSize;
  }
  return offset;
}

uint32_t Mapper_005::GetCHRBankOffset(int bank, int bankSize) {
  return bank * bankSize;
}

MIRROR Mapper_005::mirror() { return mirrorMode; }

uint8_t Mapper_005::ReadPRGRAM(uint16_t addr) {
  uint32_t ramAddr = 0;
  if (addr >= 0x6000 && addr <= 0x7FFF) {
    uint8_t bank = prgBankReg[0] & 0x07;
    ramAddr = (bank * 8192) + (addr & 0x1FFF);
  } else if (addr >= 0x8000 && addr < 0xE000) {
    int regIndex = -1;
    if (prgMode == 1 || prgMode == 2) {
      if (addr < 0xC000) {
        uint8_t bank = ((prgBankReg[2] & 0x06) | ((addr >> 13) & 1)) & 0x07;
        ramAddr = (bank * 8192) + (addr & 0x1FFF);
        if (ramAddr < vPRGRAM.size()) return vPRGRAM[ramAddr];
      } else if (addr < 0xE000 && prgMode == 2) {
        regIndex = 3;
      }
    } else if (prgMode == 3) {
      if (addr < 0xA000) regIndex = 1;
      else if (addr < 0xC000) regIndex = 2;
      else if (addr < 0xE000) regIndex = 3;
    }
    if (regIndex >= 0) {
      uint8_t bank = prgBankReg[regIndex] & 0x07;
      ramAddr = (bank * 8192) + (addr & 0x1FFF);
    }
  }
  if (ramAddr < vPRGRAM.size()) {
    return vPRGRAM[ramAddr];
  }
  return 0;
}

uint8_t Mapper_005::ReadRegister(uint16_t addr) {
  if (addr == 0x5204) {
    // IRQ Status
    // Bit 7: IRQ pending flag
    // Bit 6: In-frame flag
    uint8_t status = 0;
    if (bInFrame)
      status |= 0x40;
    if (bIRQActive)
      status |= 0x80;
    // Reading clears IRQ pending
    bIRQActive = false;
    return status;
  } else if (addr == 0x5205) {
    // Multiplier result low byte
    return (uint8_t)((multiplierA * multiplierB) & 0xFF);
  } else if (addr == 0x5206) {
    // Multiplier result high byte
    return (uint8_t)(((multiplierA * multiplierB) >> 8) & 0xFF);
  } else if (addr >= 0x5C00 && addr <= 0x5FFF) {
    // ExRAM read (only readable directly if exRamMode >= 2, open bus otherwise)
    if (exRamMode >= 2) {
      return vExRAM[addr & 0x03FF];
    }
    return 0;
  }
  return 0;
}

void Mapper_005::cpuSnoopWrite(uint16_t addr, uint8_t data) {
  if (addr >= 0x2000 && addr <= 0x3FFF) {
    uint16_t reg = addr & 0x0007;
    if (reg == 0) {
      // PPUCTRL ($2000) Bit 5: Sprite size (0: 8x8 pixels; 1: 8x16 pixels)
      bSprite8x16Mode = (data & 0x20) != 0;
    } else if (reg == 1) {
      // PPUMASK ($2001) Bits 3,4: Render enable (substitutions enabled if either is set)
      bool prevRender = bRenderEnable;
      bRenderEnable = (data & 0x18) != 0;
      if (!bRenderEnable && prevRender) {
        bInFrame = false;
        scanlineCounter = 0;
      }
    }
  }
}

void Mapper_005::cpuSnoopRead(uint16_t addr) {
  if (addr == 0xFFFA || addr == 0xFFFB) {
    // NMI vector fetch: clears in-frame flag, acknowledges IRQ, resets counter
    bInFrame = false;
    scanlineCounter = 0;
    bIRQActive = false;
  }
}

bool Mapper_005::cpuMapRead(uint16_t addr, uint32_t &mapped_addr) {
  // $5000-$5FFF: Internal registers/ExRAM
  if (addr >= 0x5000 && addr <= 0x5FFF) {
    mapped_addr = 0xFFFFFFFF; // Special marker for register read
    return true;
  }

  // $6000-$7FFF: PRG RAM (always 8KB bank via $5113)
  if (addr >= 0x6000 && addr <= 0x7FFF) {
    mapped_addr = 0xFFFFFFFF; // PRG RAM handled via ReadPRGRAM
    return true;
  }

  // $8000-$FFFF: PRG ROM/RAM based on mode
  if (addr >= 0x8000) {
    uint32_t prgRomSize = nPRGBanks * 16384;

    switch (prgMode) {
    case 0: // One 32KB bank
    {
      // $5117 bits 6-2 select 32KB bank
      uint32_t bank = (prgBankReg[4] >> 2) & 0x1F;
      mapped_addr = ((bank * 32768) + (addr & 0x7FFF)) % prgRomSize;
    } break;

    case 1: // Two 16KB banks
      if (addr < 0xC000) {
        // $8000-$BFFF: $5115 (can be RAM if bit 7 clear)
        bool isRAM = !(prgBankReg[2] & 0x80);
        if (isRAM) {
          mapped_addr = 0xFFFFFFFF;
          return true;
        }
        uint32_t bank = (prgBankReg[2] >> 1) & 0x3F;
        mapped_addr = ((bank * 16384) + (addr & 0x3FFF)) % prgRomSize;
      } else {
        // $C000-$FFFF: $5117
        uint32_t bank = (prgBankReg[4] >> 1) & 0x3F;
        mapped_addr = ((bank * 16384) + (addr & 0x3FFF)) % prgRomSize;
      }
      break;

    case 2: // 16KB + 8KB + 8KB (Castlevania 3)
      if (addr < 0xC000) {
        // $8000-$BFFF: 16KB via $5115
        bool isRAM = !(prgBankReg[2] & 0x80);
        if (isRAM) {
          mapped_addr = 0xFFFFFFFF;
          return true;
        }
        uint32_t bank = (prgBankReg[2] >> 1) & 0x3F;
        mapped_addr = ((bank * 16384) + (addr & 0x3FFF)) % prgRomSize;
      } else if (addr < 0xE000) {
        // $C000-$DFFF: 8KB via $5116
        bool isRAM = !(prgBankReg[3] & 0x80);
        if (isRAM) {
          mapped_addr = 0xFFFFFFFF;
          return true;
        }
        uint32_t bank = prgBankReg[3] & 0x7F;
        mapped_addr = ((bank * 8192) + (addr & 0x1FFF)) % prgRomSize;
      } else {
        // $E000-$FFFF: 8KB via $5117 (always ROM)
        uint32_t bank = prgBankReg[4] & 0x7F;
        mapped_addr = ((bank * 8192) + (addr & 0x1FFF)) % prgRomSize;
      }
      break;

    case 3: // Four 8KB banks (default at power-on)
    default:
      if (addr < 0xA000) {
        // $8000-$9FFF: $5114
        bool isRAM = !(prgBankReg[1] & 0x80);
        if (isRAM) {
          mapped_addr = 0xFFFFFFFF;
          return true;
        }
        uint32_t bank = prgBankReg[1] & 0x7F;
        mapped_addr = ((bank * 8192) + (addr & 0x1FFF)) % prgRomSize;
      } else if (addr < 0xC000) {
        // $A000-$BFFF: $5115
        bool isRAM = !(prgBankReg[2] & 0x80);
        if (isRAM) {
          mapped_addr = 0xFFFFFFFF;
          return true;
        }
        uint32_t bank = prgBankReg[2] & 0x7F;
        mapped_addr = ((bank * 8192) + (addr & 0x1FFF)) % prgRomSize;
      } else if (addr < 0xE000) {
        // $C000-$DFFF: $5116
        bool isRAM = !(prgBankReg[3] & 0x80);
        if (isRAM) {
          mapped_addr = 0xFFFFFFFF;
          return true;
        }
        uint32_t bank = prgBankReg[3] & 0x7F;
        mapped_addr = ((bank * 8192) + (addr & 0x1FFF)) % prgRomSize;
      } else {
        // $E000-$FFFF: $5117 (always ROM)
        uint32_t bank = prgBankReg[4] & 0x7F;
        mapped_addr = ((bank * 8192) + (addr & 0x1FFF)) % prgRomSize;
      }
      break;
    }
    return true;
  }

  return false;
}

bool Mapper_005::cpuMapWrite(uint16_t addr, uint32_t &mapped_addr) {
  return false;
}

bool Mapper_005::cpuMapWrite(uint16_t addr, uint32_t &mapped_addr,
                             uint8_t data) {
  // $5000-$5FFF: Registers and ExRAM
  if (addr >= 0x5000 && addr <= 0x5FFF) {
    mapped_addr = 0xFFFFFFFF;

    // Configuration registers
    if (addr == 0x5100) {
      prgMode = data & 0x03;
    } else if (addr == 0x5101) {
      chrMode = data & 0x03;
    } else if (addr == 0x5102) {
      prgRamProtect1 = data & 0x03;
    } else if (addr == 0x5103) {
      prgRamProtect2 = data & 0x03;
    } else if (addr == 0x5104) {
      exRamMode = data & 0x03;
    } else if (addr == 0x5105) {
      // Nametable mapping
      ntMapping = data;
      // Determine mirroring mode from mapping
      // 0=CIRAM page 0, 1=CIRAM page 1, 2=ExRAM, 3=Fill
      uint8_t nt0 = (data >> 0) & 0x03;
      uint8_t nt1 = (data >> 2) & 0x03;
      uint8_t nt2 = (data >> 4) & 0x03;
      uint8_t nt3 = (data >> 6) & 0x03;

      // Simple heuristic for common patterns
      if (nt0 == 0 && nt1 == 0 && nt2 == 1 && nt3 == 1) {
        mirrorMode = MIRROR::HORIZONTAL;
      } else if (nt0 == 0 && nt1 == 1 && nt2 == 0 && nt3 == 1) {
        mirrorMode = MIRROR::VERTICAL;
      } else if (nt0 == nt1 && nt1 == nt2 && nt2 == nt3) {
        mirrorMode = (nt0 == 0) ? MIRROR::ONESCREEN_LO : MIRROR::ONESCREEN_HI;
      }
    } else if (addr == 0x5106) {
      fillTile = data;
    } else if (addr == 0x5107) {
      fillColor = data & 0x03;
    }
    // PRG bank registers $5113-$5117
    else if (addr >= 0x5113 && addr <= 0x5117) {
      prgBankReg[addr - 0x5113] = data;
    }
    // CHR bank registers $5120-$512B
    else if (addr >= 0x5120 && addr <= 0x512B) {
      chrBankReg[addr - 0x5120] = data | (chrUpperBits << 8);
      // Track which half was written (for 8x8 sprite mode / PPUDATA)
      // $5120-$5127 = sprite banks, $5128-$512B = background banks
      lastCHRBankWriteIsUpperHalf = (addr >= 0x5128);
    } else if (addr == 0x5130) {
      chrUpperBits = data & 0x03;
    }
    // Vertical split mode $5200-$5202 (stub)
    else if (addr >= 0x5200 && addr <= 0x5202) {
      // Not implemented - not used by Castlevania 3
    }
    // IRQ registers
    else if (addr == 0x5203) {
      irqScanline = data;
      if (irqScanline > 0 && scanlineCounter == irqScanline) {
        bIRQActive = true;
      }
    } else if (addr == 0x5204) {
      bIRQEnable = (data & 0x80) != 0;
    }
    // Multiplier
    else if (addr == 0x5205) {
      multiplierA = data;
    } else if (addr == 0x5206) {
      multiplierB = data;
    }
    // ExRAM writes $5C00-$5FFF
    else if (addr >= 0x5C00 && addr <= 0x5FFF) {
      if (exRamMode <= 2) {
        vExRAM[addr & 0x03FF] = data;
      }
      // Mode 3: read-only
    }

    return true;
  }

  // $6000-$7FFF: PRG RAM write
  if (addr >= 0x6000 && addr <= 0x7FFF) {
    if (IsPRGRAMEnabled()) {
      uint8_t bank = prgBankReg[0] & 0x07;
      uint32_t ramAddr = (bank * 8192) + (addr & 0x1FFF);
      if (ramAddr < vPRGRAM.size()) {
        vPRGRAM[ramAddr] = data;
      }
    }
    mapped_addr = 0xFFFFFFFF;
    return true;
  }

  // $8000-$DFFF: Can write to PRG RAM if mapped
  if (addr >= 0x8000 && addr < 0xE000) {
    if (IsPRGRAMEnabled()) {
      if (prgMode == 1 || prgMode == 2) {
        if (addr < 0xC000 && !(prgBankReg[2] & 0x80)) {
          uint8_t bank = ((prgBankReg[2] & 0x06) | ((addr >> 13) & 1)) & 0x07;
          uint32_t ramAddr = (bank * 8192) + (addr & 0x1FFF);
          if (ramAddr < vPRGRAM.size()) {
            vPRGRAM[ramAddr] = data;
          }
          mapped_addr = 0xFFFFFFFF;
          return true;
        }
      }
      int regIndex = -1;
      if (prgMode == 2) {
        if (addr >= 0xC000 && addr < 0xE000) {
          regIndex = 3; // $5116
        }
      } else if (prgMode == 3) {
        if (addr < 0xA000)
          regIndex = 1;
        else if (addr < 0xC000)
          regIndex = 2;
        else if (addr < 0xE000)
          regIndex = 3;
      }

      if (regIndex >= 0 && !(prgBankReg[regIndex] & 0x80)) {
        // It's RAM
        uint8_t bank = prgBankReg[regIndex] & 0x07;
        uint32_t ramAddr = (bank * 8192) + (addr & 0x1FFF);
        if (ramAddr < vPRGRAM.size()) {
          vPRGRAM[ramAddr] = data;
        }
        mapped_addr = 0xFFFFFFFF;
        return true;
      }
    }
  }

  return false;
}

bool Mapper_005::ppuMapRead(uint16_t addr, uint32_t &mapped_addr) {
  // Pattern table $0000-$1FFF
  if (addr < 0x2000) {
    uint32_t bank = 0;
    uint32_t chrRomSize = nCHRBanks * 8192;
    if (chrRomSize == 0)
      chrRomSize = 8192; // CHR RAM

    // NESdev: Independent 8x16 sprite / background CHR banks are ONLY active when
    // 8x16 sprite mode is enabled ($2000 bit 5) AND rendering substitutions are enabled ($2001 bits 3,4 non-zero).
    bool bDualBankActive = bSprite8x16Mode && bRenderEnable;

    switch (chrMode) {
    case 0: // 8KB
      if (bDualBankActive && bg_fetches_remaining > 0) {
        bank = chrBankReg[11];
        bg_fetches_remaining--;
      } else if (!bRenderEnable && lastCHRBankWriteIsUpperHalf) {
        bank = chrBankReg[11];
      } else {
        bank = chrBankReg[7];
        if (bg_fetches_remaining > 0) bg_fetches_remaining--;
      }
      mapped_addr = ((bank * 8192) + addr) % chrRomSize;
      break;

    case 1: // 4KB
      if (bDualBankActive && bg_fetches_remaining > 0) {
        if (exRamMode == 1) {
          bank = (lastBgTileExRam & 0x3F) | (chrUpperBits << 6);
        } else {
          bank = chrBankReg[11];
        }
        bg_fetches_remaining--;
      } else if (!bRenderEnable && lastCHRBankWriteIsUpperHalf) {
        bank = chrBankReg[11];
      } else {
        bank = (addr < 0x1000) ? chrBankReg[3] : chrBankReg[7];
        if (bg_fetches_remaining > 0) bg_fetches_remaining--;
      }
      mapped_addr = ((bank * 4096) + (addr & 0x0FFF)) % chrRomSize;
      break;

    case 2: // 2KB
      if (bDualBankActive && bg_fetches_remaining > 0) {
        if (exRamMode == 1) {
          bank = ((lastBgTileExRam & 0x3F) | (chrUpperBits << 6)) * 2 + ((addr >> 11) & 0x01);
        } else {
          bank = (addr < 0x0800 || (addr >= 0x1000 && addr < 0x1800)) ? chrBankReg[9] : chrBankReg[11];
        }
        bg_fetches_remaining--;
      } else if (!bRenderEnable && lastCHRBankWriteIsUpperHalf) {
        bank = (addr < 0x0800 || (addr >= 0x1000 && addr < 0x1800)) ? chrBankReg[9] : chrBankReg[11];
      } else {
        if (addr < 0x0800) bank = chrBankReg[1];
        else if (addr < 0x1000) bank = chrBankReg[3];
        else if (addr < 0x1800) bank = chrBankReg[5];
        else bank = chrBankReg[7];
        if (bg_fetches_remaining > 0) bg_fetches_remaining--;
      }
      mapped_addr = ((bank * 2048) + (addr & 0x07FF)) % chrRomSize;
      break;

    case 3: // 1KB (most common for games, used by Castlevania 3)
    default: {
      int bankIndex = (addr >> 10) & 0x03; // 0-3 within the 4KB page

      if (bDualBankActive && bg_fetches_remaining > 0) {
        // Background fetch in 8x16 sprite mode -> use $5128-$512B
        if (exRamMode == 1) {
          // Extended attribute mode: ignore standard CHR banking, use ExRAM bits and $5130
          bank = ((lastBgTileExRam & 0x3F) | (chrUpperBits << 6)) * 4 + bankIndex;
        } else {
          bank = chrBankReg[8 + bankIndex];
        }
        bg_fetches_remaining--;
      } else if (!bRenderEnable && lastCHRBankWriteIsUpperHalf) {
        // PPUDATA access outside rendering after writing to upper half ($5128-$512B)
        bank = chrBankReg[8 + bankIndex];
      } else {
        // 8x8 sprite mode (both BG and sprites), OR sprite fetch in 8x16 mode, OR standard PPUDATA access
        int wideIndex = (addr >> 10) & 0x07;
        bank = chrBankReg[wideIndex];
        if (bg_fetches_remaining > 0) {
          bg_fetches_remaining--;
        }
      }

      mapped_addr = ((bank * 1024) + (addr & 0x03FF)) % chrRomSize;
    } break;
    }

    return true;
  }

  return false;
}

bool Mapper_005::ppuMapWrite(uint16_t addr, uint32_t &mapped_addr) {
  if (addr >= 0x2000 && addr <= 0x3EFF) {
    return true;
  }
  // CHR RAM writes (if using CHR RAM)
  if (addr < 0x2000 && nCHRBanks == 0) {
    mapped_addr = addr;
    return true;
  }
  return false;
}

// Custom PPU Write to shadow nametables
bool Mapper_005::ppuWriteCustom(uint16_t addr, uint8_t data) {
  if (addr >= 0x2000 && addr <= 0x3EFF) {
    uint16_t tempAddr = addr & 0x0FFF;
    uint8_t quadrant = (tempAddr >> 10) & 0x03;
    uint8_t mode = (ntMapping >> (quadrant * 2)) & 0x03;
    uint16_t offset = tempAddr & 0x03FF;

    switch (mode) {
    case 0: // CIRAM Page 0
      internalNametable[0 + offset] = data;
      return true;
    case 1: // CIRAM Page 1
      internalNametable[1024 + offset] = data;
      return true;
    case 2: // Extended RAM
      vExRAM[offset] = data;
      return true;
    case 3: // Fill Mode (Writes ignored)
      return true;
    }
  }
  return false;
}

// Custom PPU Read for Fill Mode / Complex Mirroring
bool Mapper_005::ppuReadCustom(uint16_t addr, uint8_t &data) {
  if (addr >= 0x2000 && addr <= 0x3EFF) {
    // Trigger BG Mode on AT Read (Essential for correct CHR Banking)
    // Since ppuReadCustom handles logic before ppuMapRead, we must trigger here
    if ((addr & 0x03FF) >= 0x03C0) {
      bg_fetches_remaining = 2;
    }

    uint16_t tempAddr = addr & 0x0FFF;
    uint8_t quadrant = (tempAddr >> 10) & 0x03;
    uint8_t mode = (ntMapping >> (quadrant * 2)) & 0x03;
    uint16_t offset = tempAddr & 0x03FF;

    if (exRamMode == 1) {
      if (offset < 0x03C0) {
        lastBgTileAddr = offset;
        lastBgTileExRam = vExRAM[offset];
      } else {
        uint8_t palette = lastBgTileExRam & 0x03;
        data = palette | (palette << 2) | (palette << 4) | (palette << 6);
        return true;
      }
    }

    switch (mode) {
    case 0: // CIRAM Page 0
      data = internalNametable[0 + offset];
      return true;
    case 1: // CIRAM Page 1
      data = internalNametable[1024 + offset];
      return true;
    case 2: // Extended RAM
      data = (exRamMode >= 2) ? 0 : vExRAM[offset];
      return true;
    case 3: // Fill Mode
      if (offset >= 0x03C0) {
        // Attribute Table
        uint8_t palette = fillColor & 0x03;
        data = palette | (palette << 2) | (palette << 4) | (palette << 6);
      } else {
        // Name Table
        data = fillTile;
      }
      return true;
    }
  }
  return false;
}

void Mapper_005::scanline() {
  // Handled cycle-accurately in scanline(int scanline, int cycle)
}

void Mapper_005::scanline(int scanline, int cycle) {
  // NESdev: MMC5 scanline detection happens at cycle 4 of visible scanlines (0..239)
  // when rendering is enabled (bRenderEnable).
  if (cycle == 4 && scanline >= 0 && scanline < 240 && bRenderEnable) {
    if (scanline == 0 || !bInFrame) {
      bInFrame = true;
      scanlineCounter = 0;
      bIRQActive = false; // Scanline 0 acknowledges IRQ
    } else {
      scanlineCounter++;
      if (scanlineCounter == irqScanline && irqScanline > 0) {
        bIRQActive = true;
      }
    }
  }

  // End of visible frame / VBlank at scanline 241
  if (scanline == 241 && cycle == 1) {
    bInFrame = false;
    scanlineCounter = 0;
    bIRQActive = false;
  }
}
