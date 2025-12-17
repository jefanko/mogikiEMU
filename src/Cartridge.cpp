#include "Cartridge.h"
#include "Mapper_000.h"
#include "Mapper_001.h"
#include "Mapper_002.h"
#include "Mapper_004.h"
#include <fstream>
#include <iostream>

Cartridge::Cartridge(const std::string &sFileName) {
  struct sHeader {
    char name[4];
    uint8_t prg_rom_chunks;
    uint8_t chr_rom_chunks;
    uint8_t mapper1;
    uint8_t mapper2;
    uint8_t prg_ram_size;
    uint8_t tv_system1;
    uint8_t tv_system2;
    char unused[5];
  } header;

  bImageValid = false;

  std::ifstream ifs(sFileName, std::ifstream::binary);
  if (ifs.is_open()) {
    ifs.read((char *)&header, sizeof(sHeader));

    if (header.mapper1 & 0x04)
      ifs.seekg(512, std::ios_base::cur);

    nMapperID = ((header.mapper2 >> 4) << 4) | (header.mapper1 >> 4);
    nPRGBanks = header.prg_rom_chunks;
    nCHRBanks = header.chr_rom_chunks;

    // Read hardware mirroring from ROM header (bit 0 of mapper1)
    MIRROR hwMirror =
        (header.mapper1 & 0x01) ? MIRROR::VERTICAL : MIRROR::HORIZONTAL;

    std::cout << "Mapper ID: " << (int)nMapperID << std::endl;
    std::cout << "PRG Banks: " << (int)nPRGBanks << std::endl;
    std::cout << "CHR Banks: " << (int)nCHRBanks << std::endl;
    std::cout << "Mirroring: "
              << (hwMirror == MIRROR::VERTICAL ? "Vertical" : "Horizontal")
              << std::endl;

    vPRGMemory.resize(nPRGBanks * 16384);
    ifs.read((char *)vPRGMemory.data(), vPRGMemory.size());

    if (nCHRBanks == 0) {
      // CHR RAM - allocate 8KB
      vCHRMemory.resize(8192);
    } else {
      vCHRMemory.resize(nCHRBanks * 8192);
      ifs.read((char *)vCHRMemory.data(), vCHRMemory.size());
    }

    switch (nMapperID) {
    case 0:
      pMapper = std::make_shared<Mapper_000>(nPRGBanks, nCHRBanks, hwMirror);
      break;
    case 1:
      pMapper = std::make_shared<Mapper_001>(nPRGBanks, nCHRBanks);
      break;
    case 2:
      pMapper = std::make_shared<Mapper_002>(nPRGBanks, nCHRBanks, hwMirror);
      break;
    case 4:
      pMapper = std::make_shared<Mapper_004>(nPRGBanks, nCHRBanks);
      break;
    default:
      std::cerr << "Unsupported Mapper: " << (int)nMapperID << std::endl;
      pMapper = nullptr;
      bImageValid = false;
      ifs.close();
      return;
    }

    bImageValid = true;
    ifs.close();
  }
}

Cartridge::~Cartridge() {}

void Cartridge::reset() {
  if (pMapper)
    pMapper->reset();
}

bool Cartridge::ImageValid() { return bImageValid; }

bool Cartridge::cpuRead(uint16_t addr, uint8_t &data) {
  uint32_t mapped_addr = 0;
  if (pMapper && pMapper->cpuMapRead(addr, mapped_addr)) {
    if (mapped_addr == 0xFFFFFFFF) {
      // PRG RAM access
      Mapper_001 *m1 = dynamic_cast<Mapper_001 *>(pMapper.get());
      if (m1) {
        data = m1->GetPRGRAM()[addr & 0x1FFF];
        return true;
      }
      Mapper_004 *m4 = dynamic_cast<Mapper_004 *>(pMapper.get());
      if (m4) {
        data = m4->GetPRGRAM()[addr & 0x1FFF];
        return true;
      }
      return true;
    }
    if (mapped_addr < vPRGMemory.size()) {
      data = vPRGMemory[mapped_addr];
    }
    return true;
  }
  return false;
}

bool Cartridge::cpuWrite(uint16_t addr, uint8_t data) {
  uint32_t mapped_addr = 0;
  if (pMapper && pMapper->cpuMapWrite(addr, mapped_addr, data)) {
    if (mapped_addr == 0xFFFFFFFF) {
      // PRG RAM handled inside mapper
      return true;
    }
    if (mapped_addr < vPRGMemory.size()) {
      vPRGMemory[mapped_addr] = data;
    }
    return true;
  }
  return false;
}

bool Cartridge::ppuRead(uint16_t addr, uint8_t &data) {
  uint32_t mapped_addr = 0;
  if (pMapper && pMapper->ppuMapRead(addr, mapped_addr)) {
    if (mapped_addr < vCHRMemory.size()) {
      data = vCHRMemory[mapped_addr];
    }
    return true;
  }
  return false;
}

bool Cartridge::ppuWrite(uint16_t addr, uint8_t data) {
  uint32_t mapped_addr = 0;
  if (pMapper && pMapper->ppuMapWrite(addr, mapped_addr)) {
    if (mapped_addr < vCHRMemory.size()) {
      vCHRMemory[mapped_addr] = data;
    }
    return true;
  }
  return false;
}
