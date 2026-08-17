# Mogiki NES Emulator (mgkEMU)

<div align="center">
  <img src="mgkEMU.png" alt="Mogiki NES Emulator Icon" width="128" height="128" />
  <h3>A cycle-accurate, hardware-sensitive Nintendo Entertainment System (NES) emulator with a modern Avalonia UI.</h3>

  <p>
    <b>English</b> |
    <a href="README.ja.md">日本語</a> |
    <a href="README.zh-TW.md">繁體中文 (台灣)</a>
  </p>
</div>

---

## 🌟 Overview

**Mogiki** (模擬器 - meaning *"simulator"* / *"emulator"*) is a cycle-sensitive Nintendo Entertainment System / Famicom emulator re-engineered in **C# (.NET 10)** featuring a modern, hardware-accelerated **Avalonia UI 11.x** interface.

The core follows an **accuracy-first, bus-centric architecture**, simulating the internal bus interactions of the NES hardware cycle-by-cycle to ensure authentic gameplay, accurate audio synthesis, and split-screen raster timing.

---

## ✨ Features

### 🖥️ Modern Avalonia UI
- **Dark Fluent Theme**: Clean modern aesthetics powered by `Avalonia.Themes.Fluent` with Inter typography and custom status pills.
- **Hardware-Accelerated Viewport**: High-performance 60.1 FPS direct memory blitting via `WriteableBitmap`.
- **Dedicated Game Window**: Games open in their own window, with `F11` fullscreen and `Escape` to stop and return to the library.
- **SDL3 Audio Output**: Stream-based, batched float audio with explicit pause/resume and graceful fallback when SDL3 is unavailable.
- **Unified Settings**: A dedicated Settings window groups graphics backend, scale, aspect ratio, filtering, fullscreen-on-launch, audio, controller, and library options.
- **Graphics Backend Selection**: Choose Automatic, Vulkan, OpenGL, Direct3D 11, Direct3D 12, or the Avalonia software presentation fallback.
- **Drag & Drop**: Drag any `.nes` ROM file from Windows Explorer directly onto the emulator window to launch immediately.
- **Recent ROMs Menu**: Automatically tracks and remembers your last 10 games.
- **Aspect Ratio Controls**:
  - `4:3 Standard TV`
  - `8:7 Authentic NTSC PAR`
  - `1:1 Pixel Perfect`
- **Video Filters**: Nearest-Neighbor (Crisp Pixels) or Smooth Bilinear filtering.
- **In-App Screenshots**: Save PNG frame captures with `F12`.
- **Interactive Modals**:
  - **Controller Configuration**: Rebind buttons with live keyboard capture and default presets.
  - **Pattern Table Viewer**: Real-time CHR ROM tile bank inspector with dynamic palette selection.
  - **About Dialog**: Overview of the emulator architecture and hardware specifications.

### 🎮 Emulation Hardware Core (`Mogiki.Core`)
- **Ricoh 2A03 CPU**: Full 6502 core with illegal/undocumented opcode support and unmanaged function pointer instruction dispatch.
- **Ricoh 2C02 PPU**: Cycle-accurate pixel rendering pipeline, 8x8 / 8x16 sprites, sprite evaluation, PPUMASK left-clipping, and odd-frame cycle skip.
- **APU 2A03**: Complete audio synthesis with 2 Pulse channels, Triangle channel, Noise channel (LFSR), DMC samples, and low-latency ring buffer.
- **Supported Mappers**:
  - **Mapper 0 (NROM)**: *Super Mario Bros., Donkey Kong, Pac-Man*
  - **Mapper 1 (MMC1)**: *The Legend of Zelda, Metroid, Mega Man 2*
  - **Mapper 2 (UxROM)**: *Castlevania, Mega Man, Duck Tales*
  - **Mapper 4 (MMC3)**: *Super Mario Bros. 3, Kirby's Adventure* (Accurate scanline reload state machine & IRQ timing)
  - **Mapper 5 (MMC5)**: *Castlevania III: Dracula's Curse* (Dual-bank 8x16 CHR, 1KB scanline IRQ, hardware multiplier)
  - **Mapper 69 (Sunsoft FME-7)**: *Gimmick!, Batman: Return of the Joker*

---

## 🕹️ Default Keyboard Controls

| NES Button | Keyboard Key | Description |
|:---|:---|:---|
| **D-Pad Up** | `Up Arrow` | Move Up / Climb |
| **D-Pad Down** | `Down Arrow` | Move Down / Crouch |
| **D-Pad Left** | `Left Arrow` | Move Left |
| **D-Pad Right** | `Right Arrow` | Move Right |
| **A Button** | `X` | Jump / Action |
| **B Button** | `Z` | Attack / Run |
| **Start** | `S` | Start / Menu |
| **Select** | `A` | Select / Item |
| **Open ROM** | `Ctrl + O` / `F1` | Open file picker dialog |
| **Pause / Resume** | `Space` / `P` | Pause emulation |
| **Stop / Return to Library** | `Escape` | Close the game window and return to the launcher |
| **Fullscreen** | `F11` | Toggle fullscreen for the dedicated game window |
| **Reset** | `Ctrl + R` | Reset system |
| **Fast Forward** | `Tab` | Turbo speed |
| **Screenshot** | `F12` | Save screenshot PNG |

> *All controller keys can be customized via **Config $\rightarrow$ Controller Configuration...***

---

## Runtime and rendering architecture

The launcher owns the UI only. `EmulatorSession` owns the NES bus and
cartridge lifetime, while `EmulationRunner` clocks it on a dedicated thread.
Completed PPU frames are exchanged through a two-slot ownership pipeline, so
the presenter never reads a buffer while the emulator is writing it.

The dedicated game window prefers `Sdl3GpuRenderer`. It uploads the 256x240
frame to a streaming SDL3 texture and lets SDL choose an accelerated backend
(Vulkan, OpenGL, Direct3D, or the available fallback). Set
`renderer=opengl`, `renderer=vulkan`, or `renderer=auto` in `config.ini`, or
choose the backend from **Settings -> Graphics**. The Settings window also
persists scale, aspect ratio, bilinear filtering, fullscreen-on-launch, audio,
controller, and library preferences. Avalonia remains the fallback when SDL3
or the requested backend is unavailable.

## ROM regression tests

`dotnet test` runs the canonical `nestest` CPU trace, official instruction and
PPU VBL ROM smoke tests, controller strobe timing tests, and frame-pipeline
ownership tests. The ROM inputs and SHA-256 records live under `tests/Roms`.

## 🚀 Getting Started & Building

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Run with .NET CLI
```powershell
# Run the emulator
dotnet run --project src/Mogiki.App

# Or pass a ROM directly
dotnet run --project src/Mogiki.App "path/to/game.nes"
```

### Build & Publish Standalone Release
```powershell
dotnet publish src/Mogiki.App/Mogiki.App.csproj -c Release -o ./dist
```
The compiled executable will be located at `dist/Mogiki.App.exe`.

### Run Automated Unit Tests
```powershell
dotnet test
```

---

## 🏛️ Project Architecture

```
mogikiEMU/
├── src/
│   ├── Mogiki.Core/          # Cross-platform emulation core (.NET 10)
│   │   ├── Cpu/              # Ricoh 2A03 6502 CPU
│   │   ├── Ppu/              # Ricoh 2C02 PPU & Loopy registers
│   │   ├── Apu/              # 2A03 APU audio synthesis
│   │   ├── Bus/              # Main interconnect & DMA
│   │   ├── Cartridge/        # iNES loader
│   │   └── Mappers/          # Mappers 0, 1, 2, 4, 5, 69
│   └── Mogiki.App/           # Modern Avalonia UI frontend
│       ├── Views/            # MainWindow, ControllerConfig, PatternTable, About
│       ├── Audio/            # AudioEngine with low-pass filtering
│       ├── Config/           # AppConfig & keybindings
│       └── Assets/           # Application icons & branding
├── tests/
│   └── Mogiki.Tests/         # xUnit automated test suite
├── srcLegacy/                # Original C++ reference implementation
└── build.bat                 # Legacy C++ build script (mgkEMU_legacy.exe)
```

---

## 📜 License
This project is open-source under the MIT License.
