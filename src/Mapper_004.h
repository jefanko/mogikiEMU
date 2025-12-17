#pragma once
#include "Mapper.h"
#include <vector>

class Mapper_004 : public Mapper {
public:
  Mapper_004(uint8_t prgBanks, uint8_t chrBanks);
  ~Mapper_004();

  bool cpuMapRead(uint16_t addr, uint32_t &mapped_addr) override;
  bool cpuMapWrite(uint16_t addr, uint32_t &mapped_addr) override;
  bool cpuMapWrite(uint16_t addr, uint32_t &mapped_addr, uint8_t data) override;
  bool ppuMapRead(uint16_t addr, uint32_t &mapped_addr) override;
  bool ppuMapWrite(uint16_t addr, uint32_t &mapped_addr) override;

  void reset() override;
  MIRROR mirror() override { return mirrorMode; }

  // IRQ interface
  bool irqState() override { return bIRQActive; }
  void irqClear() override { bIRQActive = false; }

  // Scanline counter
  void scanline() override;

  // PRG RAM access
  std::vector<uint8_t> &GetPRGRAM() { return vRAMStatic; }

private:
  // Bank registers
  uint8_t nTargetRegister = 0;
  bool bPRGBankMode = false;
  bool bCHRInversion = false;
  MIRROR mirrorMode = MIRROR::HORIZONTAL;

  uint32_t pRegister[8];
  uint32_t pCHRBank[8];
  uint32_t pPRGBank[4];

  // IRQ
  bool bIRQActive = false;
  bool bIRQEnable = false;
  bool bIRQUpdate = false;
  uint16_t nIRQCounter = 0;
  uint16_t nIRQReload = 0;

  // PRG RAM
  std::vector<uint8_t> vRAMStatic;
};
