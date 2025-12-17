#pragma once
#include "Mapper.h"
#include <vector>

class Mapper_001 : public Mapper {
public:
  Mapper_001(uint8_t prgBanks, uint8_t chrBanks);
  ~Mapper_001();

  bool cpuMapRead(uint16_t addr, uint32_t &mapped_addr) override;
  bool cpuMapWrite(uint16_t addr, uint32_t &mapped_addr, uint8_t data) override;
  bool cpuMapWrite(uint16_t addr, uint32_t &mapped_addr) override;
  bool ppuMapRead(uint16_t addr, uint32_t &mapped_addr) override;
  bool ppuMapWrite(uint16_t addr, uint32_t &mapped_addr) override;

  void reset() override;
  MIRROR mirror() override;

  // PRG RAM access
  uint8_t *GetPRGRAM() { return vRAMStatic.data(); }

private:
  uint8_t nLoadRegister = 0x00;
  uint8_t nLoadRegisterCount = 0;
  uint8_t nControlRegister = 0;

  uint8_t nCHRBankSelect4Lo = 0;
  uint8_t nCHRBankSelect4Hi = 0;
  uint8_t nCHRBankSelect8 = 0;

  uint8_t nPRGBankSelect16Lo = 0;
  uint8_t nPRGBankSelect16Hi = 0;
  uint8_t nPRGBankSelect32 = 0;

  // Mirroring mode
  MIRROR mirrorMode = MIRROR::HORIZONTAL;

  // 8KB PRG RAM
  std::vector<uint8_t> vRAMStatic;
};
