#pragma once
#include "Mapper.h"
#include <cstdint>
#include <memory>
#include <string>
#include <vector>

class Cartridge {
public:
  Cartridge(const std::string &sFileName);
  ~Cartridge();

  bool cpuRead(uint16_t addr, uint8_t &data);
  bool cpuWrite(uint16_t addr, uint8_t data);

  bool ppuRead(uint16_t addr, uint8_t &data);
  bool ppuWrite(uint16_t addr, uint8_t data);

  void reset();
  bool ImageValid();

  // Get current mirroring mode from mapper
  MIRROR GetMirror() {
    if (pMapper)
      return pMapper->mirror();
    return MIRROR::HORIZONTAL;
  }

  // IRQ interface (forwarded to mapper)
  bool GetIRQState() {
    if (pMapper)
      return pMapper->irqState();
    return false;
  }
  void ClearIRQ() {
    if (pMapper)
      pMapper->irqClear();
  }
  void Scanline() {
    if (pMapper)
      pMapper->scanline();
  }

private:
  bool bImageValid = false;
  std::vector<uint8_t> vPRGMemory;
  std::vector<uint8_t> vCHRMemory;

  uint8_t nMapperID = 0;
  uint8_t nPRGBanks = 0;
  uint8_t nCHRBanks = 0;

  std::shared_ptr<Mapper> pMapper;
};
