# mgkEMU

![mgkEMU Icon](mgkEMU.png)

**mgkEMU** (derived from **Mogiki** / 模擬器 - meaning "simulator") is a robust, cycle-accurate Nintendo Entertainment System (NES) emulator written in C++ using SDL2.

## Philosophy & Methodology
The logic behind this project is to **emulate the hardware, not just the behavior**. By simulating the NES internal bus and component interactions cycle-by-cycle, we ensure that even hardware quirks and timing-sensitive tricks work natively.

This project provides a deep understanding of the NES hardware architecture, including the CPU (6502), PPU (2C02), APU (2A03), and various cartridge mappers.


## Features

- **CPU**: Complete Ricoh 2A03 (6502 w/o decimal mode) emulation with cycle-accurate timing and full instruction set.
- **PPU**: Cycle-accurate pixel rendering (2C02), supporting background scrolling, sprites (8x8 and 8x16), palette scanning, and accurate scanline timing.
- **APU**: Full audio implementation featuring:
  - 2 Pulse Channels (Square waves with sweep and envelope)
  - 1 Triangle Channel (Linear counter)
  - 1 Noise Channel (LFSR with envelope)
  - 1 DMC Channel (Delta Modulation Channel for samples)
  - Synchronization with CPU cycles.
- **Mappers**: Support for the most common cartridge hardware:
  - **Mapper 0 (NROM)**: *Super Mario Bros., Donkey Kong, Ice Climber*
  - **Mapper 1 (MMC1)**: *Metroid, The Legend of Zelda* (Basic support)
  - **Mapper 2 (UxROM)**: *Castlevania, Mega Man*
  - **Mapper 4 (MMC3)**: *Super Mario Bros. 3, Kirby's Adventure* (Advanced IRQ support)
- **Controls**: Keyboard input with configurable bindings.
- **Save/Load**: Basic configuration saving.

## Compatibility Status

| Game | Status | Notes |
|------|--------|-------|
| **Super Mario Bros. (NROM)** | ⭐ Perfect | Runs at 60fps with correct audio and physics. |
| **Donkey Kong (NROM)** | ⭐ Perfect | Fully playable. |
| **Super Mario Bros. 3 (MMC3)** | ⭐ Playable | Status bar works (IRQ), scrolling is smooth. |
| **Kirby's Adventure (MMC3)** | ⚠️ Playable* | Playable, but has minor graphical artifacts (split screen timing issues). Uses complex pattern table switching that mimics edge cases. |
| **Castlevania (UxROM)** | ⭐ Perfect | Fully playable. |

## Build Instructions

### Prerequisites
- **SDL2**: Development libraries are required.
- **C++ Compiler**: C++17 capable compiler (g++, clang, MSVC).

### Building (Command Line)
To build with `g++` (e.g., MinGW on Windows or Linux):

```bash
# Ensure SDL2 include/lib paths are set if not in standard locations
g++ -o mgkEMU src/*.cpp -lmingw32 -lSDL2main -lSDL2
```

## Technical Architecture

The emulator follows a bus-centric architecture similar to the real hardware:

- **Bus**: The central communication hub. Connects CPU, PPU, APU, and Cartridge. Handles memory mapping ($0000-$FFFF) and redirecting reads/writes.
- **CPU6502**: Implements the fetch-decode-execute cycle. Handles official opcodes and mimics cycle counts.
- **PPU2C02**: Renders the screen scanline by scanline. It runs at 3x the speed of the CPU (NTSC). Implements background fetch cycles, sprite evaluation, and pattern table lookups.
- **APU2A03**: Generates audio samples. Runs at CPU speed. Uses a lock-free ring buffer to feed samples to SDL2's audio callback to prevent clicking/popping.
- **Cartridge/Mappers**: Handling PRG/CHR bank switching. 
  - *MMC3 Implementation*: Uses A12 line monitoring to clock the IRQ counter, essential for split-screen effects in games like SMB3 and Kirby.

## Controls

| NES Button | Keyboard Key |
|------------|--------------|
| **D-Pad** | Arrow Keys |
| **A** | `X` |
| **B** | `Z` |
| **Start** | `S` |
| **Select** | `A` |
| **Open ROM** | `F1` |
| **Pause** | `P` |
| **Quit** | `ESC` |



## Known Issues
- **Kirby's Adventure**: The split screen status bar sometimes glitches or shakes. This is due to the extreme precision required for A12-based IRQ timing when the game swaps pattern tables mid-frame. This is a common "holy grail" test for MMC3 accuracy.

