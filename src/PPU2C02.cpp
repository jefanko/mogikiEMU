#include "PPU2C02.h"
#include <cstring>

PPU2C02::PPU2C02() {
  memset(tblName, 0, sizeof(tblName));
  memset(tblPalette, 0, sizeof(tblPalette));
  memset(OAM, 0, sizeof(OAM));
  memset(screen, 0, sizeof(screen));

  // Initialize NES Color Palette (NTSC)
  palScreen[0x00] = {84, 84, 84, 255};
  palScreen[0x01] = {0, 30, 116, 255};
  palScreen[0x02] = {8, 16, 144, 255};
  palScreen[0x03] = {48, 0, 136, 255};
  palScreen[0x04] = {68, 0, 100, 255};
  palScreen[0x05] = {92, 0, 48, 255};
  palScreen[0x06] = {84, 4, 0, 255};
  palScreen[0x07] = {60, 24, 0, 255};
  palScreen[0x08] = {32, 42, 0, 255};
  palScreen[0x09] = {8, 58, 0, 255};
  palScreen[0x0A] = {0, 64, 0, 255};
  palScreen[0x0B] = {0, 60, 0, 255};
  palScreen[0x0C] = {0, 50, 60, 255};
  palScreen[0x0D] = {0, 0, 0, 255};
  palScreen[0x0E] = {0, 0, 0, 255};
  palScreen[0x0F] = {0, 0, 0, 255};

  palScreen[0x10] = {152, 150, 152, 255};
  palScreen[0x11] = {8, 76, 196, 255};
  palScreen[0x12] = {48, 50, 236, 255};
  palScreen[0x13] = {92, 30, 228, 255};
  palScreen[0x14] = {136, 20, 176, 255};
  palScreen[0x15] = {160, 20, 100, 255};
  palScreen[0x16] = {152, 34, 32, 255};
  palScreen[0x17] = {120, 60, 0, 255};
  palScreen[0x18] = {84, 90, 0, 255};
  palScreen[0x19] = {40, 114, 0, 255};
  palScreen[0x1A] = {8, 124, 0, 255};
  palScreen[0x1B] = {0, 118, 40, 255};
  palScreen[0x1C] = {0, 102, 120, 255};
  palScreen[0x1D] = {0, 0, 0, 255};
  palScreen[0x1E] = {0, 0, 0, 255};
  palScreen[0x1F] = {0, 0, 0, 255};

  palScreen[0x20] = {236, 238, 236, 255};
  palScreen[0x21] = {76, 154, 236, 255};
  palScreen[0x22] = {120, 124, 236, 255};
  palScreen[0x23] = {176, 98, 236, 255};
  palScreen[0x24] = {228, 84, 236, 255};
  palScreen[0x25] = {236, 88, 180, 255};
  palScreen[0x26] = {236, 106, 100, 255};
  palScreen[0x27] = {212, 136, 32, 255};
  palScreen[0x28] = {160, 170, 0, 255};
  palScreen[0x29] = {116, 196, 0, 255};
  palScreen[0x2A] = {76, 208, 32, 255};
  palScreen[0x2B] = {56, 204, 108, 255};
  palScreen[0x2C] = {56, 180, 204, 255};
  palScreen[0x2D] = {60, 60, 60, 255};
  palScreen[0x2E] = {0, 0, 0, 255};
  palScreen[0x2F] = {0, 0, 0, 255};

  palScreen[0x30] = {236, 238, 236, 255};
  palScreen[0x31] = {168, 204, 236, 255};
  palScreen[0x32] = {188, 188, 236, 255};
  palScreen[0x33] = {212, 178, 236, 255};
  palScreen[0x34] = {236, 174, 236, 255};
  palScreen[0x35] = {236, 174, 212, 255};
  palScreen[0x36] = {236, 180, 176, 255};
  palScreen[0x37] = {228, 196, 144, 255};
  palScreen[0x38] = {204, 210, 120, 255};
  palScreen[0x39] = {180, 222, 120, 255};
  palScreen[0x3A] = {168, 226, 144, 255};
  palScreen[0x3B] = {152, 226, 180, 255};
  palScreen[0x3C] = {160, 214, 228, 255};
  palScreen[0x3D] = {160, 162, 160, 255};
  palScreen[0x3E] = {0, 0, 0, 255};
  palScreen[0x3F] = {0, 0, 0, 255};
}

PPU2C02::~PPU2C02() {}

void PPU2C02::reset() {
  fine_x = 0x00;
  address_latch = 0x00;
  ppu_data_buffer = 0x00;
  scanline = 0;
  cycle = 0;
  bg_next_tile_id = 0x00;
  bg_next_tile_attrib = 0x00;
  bg_next_tile_lsb = 0x00;
  bg_next_tile_msb = 0x00;
  bg_shifter_pattern_lo = 0x0000;
  bg_shifter_pattern_hi = 0x0000;
  bg_shifter_attrib_lo = 0x0000;
  bg_shifter_attrib_hi = 0x0000;
  status.reg = 0x00;
  mask.reg = 0x00;
  control.reg = 0x00;
  vram_addr.reg = 0x0000;
  tram_addr.reg = 0x0000;
}

void PPU2C02::ConnectCartridge(const std::shared_ptr<Cartridge> &cartridge) {
  this->cart = cartridge;
}

Pixel &PPU2C02::GetColorFromPaletteRam(uint8_t palette, uint8_t pixel) {
  return palScreen[ppuRead(0x3F00 + (palette << 2) + pixel) & 0x3F];
}

uint8_t PPU2C02::cpuRead(uint16_t addr, bool rdonly) {
  uint8_t data = 0x00;

  if (rdonly) {
    switch (addr) {
    case 0x0000:
      data = control.reg;
      break;
    case 0x0001:
      data = mask.reg;
      break;
    case 0x0002:
      data = status.reg;
      break;
    case 0x0003:
      break;
    case 0x0004:
      break;
    case 0x0005:
      break;
    case 0x0006:
      break;
    case 0x0007:
      break;
    }
  } else {
    switch (addr) {
    case 0x0000:
      break;
    case 0x0001:
      break;
    case 0x0002:
      data = (status.reg & 0xE0) | (ppu_data_buffer & 0x1F);
      status.vertical_blank = 0;
      address_latch = 0;
      break;
    case 0x0003:
      break;
    case 0x0004:
      data = OAM[oam_addr];
      break;
    case 0x0005:
      break;
    case 0x0006:
      break;
    case 0x0007:
      data = ppu_data_buffer;
      ppu_data_buffer = ppuRead(vram_addr.reg);
      if (vram_addr.reg >= 0x3F00)
        data = ppu_data_buffer;
      vram_addr.reg += (control.increment_mode ? 32 : 1);
      break;
    }
  }
  return data;
}

void PPU2C02::cpuWrite(uint16_t addr, uint8_t data) {
  switch (addr) {
  case 0x0000: // Control
    control.reg = data;
    tram_addr.nametable_x = control.nametable_x;
    tram_addr.nametable_y = control.nametable_y;
    break;
  case 0x0001: // Mask
    mask.reg = data;
    break;
  case 0x0002: // Status
    break;
  case 0x0003: // OAM Address
    oam_addr = data;
    break;
  case 0x0004: // OAM Data
    OAM[oam_addr] = data;
    break;
  case 0x0005: // Scroll
    if (address_latch == 0) {
      fine_x = data & 0x07;
      tram_addr.coarse_x = data >> 3;
      address_latch = 1;
    } else {
      tram_addr.fine_y = data & 0x07;
      tram_addr.coarse_y = data >> 3;
      address_latch = 0;
    }
    break;
  case 0x0006: // PPU Address
    if (address_latch == 0) {
      tram_addr.reg = (uint16_t)((data & 0x3F) << 8) | (tram_addr.reg & 0x00FF);
      address_latch = 1;
    } else {
      tram_addr.reg = (tram_addr.reg & 0xFF00) | data;
      vram_addr = tram_addr;
      address_latch = 0;
    }
    break;
  case 0x0007: // PPU Data
    ppuWrite(vram_addr.reg, data);
    vram_addr.reg += (control.increment_mode ? 32 : 1);
    break;
  }
}

uint8_t PPU2C02::ppuRead(uint16_t addr, bool rdonly) {
  uint8_t data = 0x00;
  addr &= 0x3FFF;

  if (cart->ppuRead(addr, data)) {
  } else if (addr >= 0x0000 && addr <= 0x1FFF) {
    // Pattern memory
  } else if (addr >= 0x2000 && addr <= 0x3EFF) {
    addr &= 0x0FFF;

    // Dynamic mirroring based on mapper
    MIRROR mirrorMode = cart->GetMirror();

    if (mirrorMode == MIRROR::VERTICAL) {
      if (addr >= 0x0000 && addr <= 0x03FF)
        data = tblName[0][addr & 0x03FF];
      else if (addr >= 0x0400 && addr <= 0x07FF)
        data = tblName[1][addr & 0x03FF];
      else if (addr >= 0x0800 && addr <= 0x0BFF)
        data = tblName[0][addr & 0x03FF];
      else if (addr >= 0x0C00 && addr <= 0x0FFF)
        data = tblName[1][addr & 0x03FF];
    } else if (mirrorMode == MIRROR::HORIZONTAL) {
      if (addr >= 0x0000 && addr <= 0x03FF)
        data = tblName[0][addr & 0x03FF];
      else if (addr >= 0x0400 && addr <= 0x07FF)
        data = tblName[0][addr & 0x03FF];
      else if (addr >= 0x0800 && addr <= 0x0BFF)
        data = tblName[1][addr & 0x03FF];
      else if (addr >= 0x0C00 && addr <= 0x0FFF)
        data = tblName[1][addr & 0x03FF];
    } else if (mirrorMode == MIRROR::ONESCREEN_LO) {
      data = tblName[0][addr & 0x03FF];
    } else if (mirrorMode == MIRROR::ONESCREEN_HI) {
      data = tblName[1][addr & 0x03FF];
    }
  } else if (addr >= 0x3F00 && addr <= 0x3FFF) {
    addr &= 0x001F;
    if (addr == 0x0010)
      addr = 0x0000;
    if (addr == 0x0014)
      addr = 0x0004;
    if (addr == 0x0018)
      addr = 0x0008;
    if (addr == 0x001C)
      addr = 0x000C;
    data = tblPalette[addr] & (mask.grayscale ? 0x30 : 0x3F);
  }
  return data;
}

void PPU2C02::ppuWrite(uint16_t addr, uint8_t data) {
  addr &= 0x3FFF;

  if (cart->ppuWrite(addr, data)) {
  } else if (addr >= 0x0000 && addr <= 0x1FFF) {
  } else if (addr >= 0x2000 && addr <= 0x3EFF) {
    addr &= 0x0FFF;

    // Dynamic mirroring based on mapper
    MIRROR mirrorMode = cart->GetMirror();

    if (mirrorMode == MIRROR::VERTICAL) {
      if (addr >= 0x0000 && addr <= 0x03FF)
        tblName[0][addr & 0x03FF] = data;
      else if (addr >= 0x0400 && addr <= 0x07FF)
        tblName[1][addr & 0x03FF] = data;
      else if (addr >= 0x0800 && addr <= 0x0BFF)
        tblName[0][addr & 0x03FF] = data;
      else if (addr >= 0x0C00 && addr <= 0x0FFF)
        tblName[1][addr & 0x03FF] = data;
    } else if (mirrorMode == MIRROR::HORIZONTAL) {
      if (addr >= 0x0000 && addr <= 0x03FF)
        tblName[0][addr & 0x03FF] = data;
      else if (addr >= 0x0400 && addr <= 0x07FF)
        tblName[0][addr & 0x03FF] = data;
      else if (addr >= 0x0800 && addr <= 0x0BFF)
        tblName[1][addr & 0x03FF] = data;
      else if (addr >= 0x0C00 && addr <= 0x0FFF)
        tblName[1][addr & 0x03FF] = data;
    } else if (mirrorMode == MIRROR::ONESCREEN_LO) {
      tblName[0][addr & 0x03FF] = data;
    } else if (mirrorMode == MIRROR::ONESCREEN_HI) {
      tblName[1][addr & 0x03FF] = data;
    }
  } else if (addr >= 0x3F00 && addr <= 0x3FFF) {
    addr &= 0x001F;
    if (addr == 0x0010)
      addr = 0x0000;
    if (addr == 0x0014)
      addr = 0x0004;
    if (addr == 0x0018)
      addr = 0x0008;
    if (addr == 0x001C)
      addr = 0x000C;
    tblPalette[addr] = data;
  }
}

void PPU2C02::clock() {
  auto IncrementScrollX = [&]() {
    if (mask.render_background || mask.render_sprites) {
      if (vram_addr.coarse_x == 31) {
        vram_addr.coarse_x = 0;
        vram_addr.nametable_x = ~vram_addr.nametable_x;
      } else {
        vram_addr.coarse_x++;
      }
    }
  };

  auto IncrementScrollY = [&]() {
    if (mask.render_background || mask.render_sprites) {
      if (vram_addr.fine_y < 7) {
        vram_addr.fine_y++;
      } else {
        vram_addr.fine_y = 0;
        if (vram_addr.coarse_y == 29) {
          vram_addr.coarse_y = 0;
          vram_addr.nametable_y = ~vram_addr.nametable_y;
        } else if (vram_addr.coarse_y == 31) {
          vram_addr.coarse_y = 0;
        } else {
          vram_addr.coarse_y++;
        }
      }
    }
  };

  auto TransferAddressX = [&]() {
    if (mask.render_background || mask.render_sprites) {
      vram_addr.nametable_x = tram_addr.nametable_x;
      vram_addr.coarse_x = tram_addr.coarse_x;
    }
  };

  auto TransferAddressY = [&]() {
    if (mask.render_background || mask.render_sprites) {
      vram_addr.fine_y = tram_addr.fine_y;
      vram_addr.nametable_y = tram_addr.nametable_y;
      vram_addr.coarse_y = tram_addr.coarse_y;
    }
  };

  auto LoadBackgroundShifters = [&]() {
    bg_shifter_pattern_lo = (bg_shifter_pattern_lo & 0xFF00) | bg_next_tile_lsb;
    bg_shifter_pattern_hi = (bg_shifter_pattern_hi & 0xFF00) | bg_next_tile_msb;
    bg_shifter_attrib_lo = (bg_shifter_attrib_lo & 0xFF00) |
                           ((bg_next_tile_attrib & 0b01) ? 0xFF : 0x00);
    bg_shifter_attrib_hi = (bg_shifter_attrib_hi & 0xFF00) |
                           ((bg_next_tile_attrib & 0b10) ? 0xFF : 0x00);
  };

  auto UpdateShifters = [&]() {
    if (mask.render_background) {
      bg_shifter_pattern_lo <<= 1;
      bg_shifter_pattern_hi <<= 1;
      bg_shifter_attrib_lo <<= 1;
      bg_shifter_attrib_hi <<= 1;
    }
    if (mask.render_sprites && cycle >= 1 && cycle < 258) {
      for (int i = 0; i < sprite_count; i++) {
        if (spriteScanline[i].x > 0) {
          spriteScanline[i].x--;
        } else {
          sprite_shifter_pattern_lo[i] <<= 1;
          sprite_shifter_pattern_hi[i] <<= 1;
        }
      }
    }
  };

  if (scanline >= -1 && scanline < 240) {
    if (scanline == 0 && cycle == 0) {
      cycle = 1;
    }

    if (scanline == -1 && cycle == 1) {
      status.vertical_blank = 0;
      status.sprite_zero_hit = 0;
      status.sprite_overflow = 0;
      for (int i = 0; i < 8; i++) {
        sprite_shifter_pattern_lo[i] = 0;
        sprite_shifter_pattern_hi[i] = 0;
      }
    }

    if ((cycle >= 2 && cycle < 258) || (cycle >= 321 && cycle < 338)) {
      UpdateShifters();

      switch ((cycle - 1) % 8) {
      case 0:
        LoadBackgroundShifters();
        bg_next_tile_id = ppuRead(0x2000 | (vram_addr.reg & 0x0FFF));
        break;
      case 2:
        bg_next_tile_attrib = ppuRead(0x23C0 | (vram_addr.nametable_y << 11) |
                                      (vram_addr.nametable_x << 10) |
                                      ((vram_addr.coarse_y >> 2) << 3) |
                                      (vram_addr.coarse_x >> 2));
        if (vram_addr.coarse_y & 0x02)
          bg_next_tile_attrib >>= 4;
        if (vram_addr.coarse_x & 0x02)
          bg_next_tile_attrib >>= 2;
        bg_next_tile_attrib &= 0x03;
        break;
      case 4:
        bg_next_tile_lsb =
            ppuRead((control.pattern_background << 12) +
                    ((uint16_t)bg_next_tile_id << 4) + (vram_addr.fine_y) + 0);
        break;
      case 6:
        bg_next_tile_msb =
            ppuRead((control.pattern_background << 12) +
                    ((uint16_t)bg_next_tile_id << 4) + (vram_addr.fine_y) + 8);
        break;
      case 7:
        IncrementScrollX();
        break;
      }
    }

    if (cycle == 256) {
      IncrementScrollY();
    }

    if (cycle == 257) {
      LoadBackgroundShifters();
      TransferAddressX();
    }

    // MMC3 scanline counter - clocked when A12 rises from 0 to 1
    // MUST include pre-render scanline (-1) for correct counter behavior!
    if (scanline >= -1 && scanline < 240 &&
        (mask.render_background || mask.render_sprites)) {

      // Determine clock cycle based on pattern table configuration
      int clockCycle = 260; // Default for standard setup

      // If BG is at $1000 (A12 high during BG fetch), use later cycle
      if (control.pattern_background == 1) {
        clockCycle = 324;
      }

      if (cycle == clockCycle) {
        cart->Scanline();
      }
    }

    if (cycle == 338 || cycle == 340) {
      bg_next_tile_id = ppuRead(0x2000 | (vram_addr.reg & 0x0FFF));
    }

    if (scanline == -1 && cycle >= 280 && cycle < 305) {
      TransferAddressY();
    }

    // Sprite evaluation
    if (cycle == 257 && scanline >= 0) {
      memset(spriteScanline, 0xFF, 8 * sizeof(sObjectAttributeEntry));
      sprite_count = 0;

      for (int i = 0; i < 8; i++) {
        sprite_shifter_pattern_lo[i] = 0;
        sprite_shifter_pattern_hi[i] = 0;
      }

      uint8_t nOAMEntry = 0;
      bSpriteZeroHitPossible = false;

      while (nOAMEntry < 64 && sprite_count < 9) {
        int16_t diff = ((int16_t)scanline - (int16_t)OAM[nOAMEntry * 4 + 0]);
        if (diff >= 0 && diff < (control.sprite_size ? 16 : 8)) {
          if (sprite_count < 8) {
            if (nOAMEntry == 0) {
              bSpriteZeroHitPossible = true;
            }
            memcpy(&spriteScanline[sprite_count], &OAM[nOAMEntry * 4],
                   sizeof(sObjectAttributeEntry));
            sprite_count++;
          }
        }
        nOAMEntry++;
      }
      status.sprite_overflow = (sprite_count > 8);
    }

    if (cycle == 340) {
      for (uint8_t i = 0; i < sprite_count; i++) {
        uint8_t sprite_pattern_bits_lo, sprite_pattern_bits_hi;
        uint16_t sprite_pattern_addr_lo, sprite_pattern_addr_hi;

        if (!control.sprite_size) {
          // 8x8 Sprite
          if (!(spriteScanline[i].attribute & 0x80)) {
            sprite_pattern_addr_lo = (control.pattern_sprite << 12) |
                                     (spriteScanline[i].id << 4) |
                                     (scanline - spriteScanline[i].y);
          } else {
            sprite_pattern_addr_lo = (control.pattern_sprite << 12) |
                                     (spriteScanline[i].id << 4) |
                                     (7 - (scanline - spriteScanline[i].y));
          }
        } else {
          // 8x16 Sprite
          if (!(spriteScanline[i].attribute & 0x80)) {
            if (scanline - spriteScanline[i].y < 8) {
              sprite_pattern_addr_lo =
                  ((spriteScanline[i].id & 0x01) << 12) |
                  ((spriteScanline[i].id & 0xFE) << 4) |
                  ((scanline - spriteScanline[i].y) & 0x07);
            } else {
              sprite_pattern_addr_lo =
                  ((spriteScanline[i].id & 0x01) << 12) |
                  (((spriteScanline[i].id & 0xFE) + 1) << 4) |
                  ((scanline - spriteScanline[i].y) & 0x07);
            }
          } else {
            if (scanline - spriteScanline[i].y < 8) {
              sprite_pattern_addr_lo =
                  ((spriteScanline[i].id & 0x01) << 12) |
                  (((spriteScanline[i].id & 0xFE) + 1) << 4) |
                  (7 - (scanline - spriteScanline[i].y) & 0x07);
            } else {
              sprite_pattern_addr_lo =
                  ((spriteScanline[i].id & 0x01) << 12) |
                  ((spriteScanline[i].id & 0xFE) << 4) |
                  (7 - (scanline - spriteScanline[i].y) & 0x07);
            }
          }
        }

        sprite_pattern_addr_hi = sprite_pattern_addr_lo + 8;
        sprite_pattern_bits_lo = ppuRead(sprite_pattern_addr_lo);
        sprite_pattern_bits_hi = ppuRead(sprite_pattern_addr_hi);

        if (spriteScanline[i].attribute & 0x40) {
          auto flipbyte = [](uint8_t b) {
            b = (b & 0xF0) >> 4 | (b & 0x0F) << 4;
            b = (b & 0xCC) >> 2 | (b & 0x33) << 2;
            b = (b & 0xAA) >> 1 | (b & 0x55) << 1;
            return b;
          };
          sprite_pattern_bits_lo = flipbyte(sprite_pattern_bits_lo);
          sprite_pattern_bits_hi = flipbyte(sprite_pattern_bits_hi);
        }

        sprite_shifter_pattern_lo[i] = sprite_pattern_bits_lo;
        sprite_shifter_pattern_hi[i] = sprite_pattern_bits_hi;
      }
    }
  }

  if (scanline == 240) {
    // Post render scanline - do nothing
  }

  if (scanline >= 241 && scanline < 261) {
    if (scanline == 241 && cycle == 1) {
      status.vertical_blank = 1;
      if (control.enable_nmi)
        nmi = true;
    }
  }

  // Compose pixel
  uint8_t bg_pixel = 0x00;
  uint8_t bg_palette = 0x00;

  if (mask.render_background) {
    uint16_t bit_mux = 0x8000 >> fine_x;
    uint8_t p0_pixel = (bg_shifter_pattern_lo & bit_mux) > 0;
    uint8_t p1_pixel = (bg_shifter_pattern_hi & bit_mux) > 0;
    bg_pixel = (p1_pixel << 1) | p0_pixel;

    uint8_t bg_pal0 = (bg_shifter_attrib_lo & bit_mux) > 0;
    uint8_t bg_pal1 = (bg_shifter_attrib_hi & bit_mux) > 0;
    bg_palette = (bg_pal1 << 1) | bg_pal0;
  }

  uint8_t fg_pixel = 0x00;
  uint8_t fg_palette = 0x00;
  uint8_t fg_priority = 0x00;

  if (mask.render_sprites) {
    bSpriteZeroBeingRendered = false;

    for (uint8_t i = 0; i < sprite_count; i++) {
      if (spriteScanline[i].x == 0) {
        uint8_t fg_pixel_lo = (sprite_shifter_pattern_lo[i] & 0x80) > 0;
        uint8_t fg_pixel_hi = (sprite_shifter_pattern_hi[i] & 0x80) > 0;
        fg_pixel = (fg_pixel_hi << 1) | fg_pixel_lo;

        fg_palette = (spriteScanline[i].attribute & 0x03) + 0x04;
        fg_priority = (spriteScanline[i].attribute & 0x20) == 0;

        if (fg_pixel != 0) {
          if (i == 0) {
            bSpriteZeroBeingRendered = true;
          }
          break;
        }
      }
    }
  }

  uint8_t pixel = 0x00;
  uint8_t palette = 0x00;

  if (bg_pixel == 0 && fg_pixel == 0) {
    pixel = 0x00;
    palette = 0x00;
  } else if (bg_pixel == 0 && fg_pixel > 0) {
    pixel = fg_pixel;
    palette = fg_palette;
  } else if (bg_pixel > 0 && fg_pixel == 0) {
    pixel = bg_pixel;
    palette = bg_palette;
  } else if (bg_pixel > 0 && fg_pixel > 0) {
    if (fg_priority) {
      pixel = fg_pixel;
      palette = fg_palette;
    } else {
      pixel = bg_pixel;
      palette = bg_palette;
    }

    if (bSpriteZeroHitPossible && bSpriteZeroBeingRendered) {
      if (mask.render_background & mask.render_sprites) {
        if (~(mask.render_background_left | mask.render_sprites_left)) {
          if (cycle >= 9 && cycle < 258) {
            status.sprite_zero_hit = 1;
          }
        } else {
          if (cycle >= 1 && cycle < 258) {
            status.sprite_zero_hit = 1;
          }
        }
      }
    }
  }

  if (scanline >= 0 && scanline < 240 && cycle >= 1 && cycle <= 256) {
    // Edge clipping - hide leftmost and rightmost 8 pixels to simulate TV
    // overscan and hide scrolling artifacts common in NES games (like SMB3/FF1)
    if (cycle <= 8 || cycle >= 249) {
      pixel = 0;
      palette = 0;
    } else if (cycle <= 8) {
      // Fallback to hardware mask logic if we weren't forcing (sanity check)
      if (!mask.render_background_left && bg_pixel > 0) {
        bg_pixel = 0;
        bg_palette = 0;
      }
      if (!mask.render_sprites_left && fg_pixel > 0) {
        fg_pixel = 0;
        fg_palette = 0;
      }
      // Re-evaluate pixel if we modified components (only if not forced zero
      // above)
      if (bg_pixel == 0 && fg_pixel == 0) {
        pixel = 0;
        palette = 0;
      }
    }

    screen[scanline * 256 + (cycle - 1)] =
        GetColorFromPaletteRam(palette, pixel);
  }

  cycle++;
  if (cycle >= 341) {
    cycle = 0;
    scanline++;
    if (scanline >= 261) {
      scanline = -1;
      frame_complete = true;
    }
  }
}
