#pragma once
#include "Mapper.h"
#include <vector>

class Mapper_069 : public Mapper {
public:
  Mapper_069(uint8_t prgBanks, uint8_t chrBanks);
  ~Mapper_069();

  bool cpuMapRead(uint16_t addr, uint32_t &mapped_addr) override;
  bool cpuMapWrite(uint16_t addr, uint32_t &mapped_addr) override;
  bool cpuMapWrite(uint16_t addr, uint32_t &mapped_addr, uint8_t data) override;
  bool ppuMapRead(uint16_t addr, uint32_t &mapped_addr) override;
  bool ppuMapWrite(uint16_t addr, uint32_t &mapped_addr) override;

  void reset() override;
  MIRROR mirror() override { return mirrorMode; }

  // IRQ interface - FME-7 uses cycle-counting IRQ
  bool irqState() override { return bIRQActive; }
  void irqClear() override { bIRQActive = false; }

  // Called every CPU cycle (for IRQ counting)
  void CountIRQ();

  // PRG RAM access
  std::vector<uint8_t> &GetPRGRAM() { return vPRGRAM; }

private:
  // Command register ($8000-$9FFF)
  uint8_t commandRegister = 0;

  // PRG bank registers (4 banks: $6000, $8000, $A000, $C000)
  // Index 0 = $6000-$7FFF (can be RAM or ROM)
  // Index 1 = $8000-$9FFF
  // Index 2 = $A000-$BFFF
  // Index 3 = $C000-$DFFF
  // $E000-$FFFF is fixed to last bank
  uint8_t prgBank[4] = {0};
  bool prgRamEnable = false;
  bool prgRamSelect = false;

  // CHR bank registers (8 x 1KB banks)
  uint8_t chrBank[8] = {0};

  // Mirroring
  MIRROR mirrorMode = MIRROR::VERTICAL;

  // IRQ
  bool bIRQEnable = false;
  bool bIRQCounterEnable = false;
  bool bIRQActive = false;
  uint16_t irqCounter = 0;

  // PRG RAM (8KB)
  std::vector<uint8_t> vPRGRAM;
};
