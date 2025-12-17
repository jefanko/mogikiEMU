#include "Bus.h"
#include "Cartridge.h"
#include "Config.h"
#include "Display.h"
#include "Platform.h"
#include "include/SDL2/SDL.h"
#include <atomic>
#include <iostream>
#include <memory>
#include <string>
#include <vector>

// Ring buffer for audio - lock-free single producer/consumer
static const size_t AUDIO_BUFFER_SIZE =
    16384; // Larger buffer to prevent underruns
static float audioBuffer[AUDIO_BUFFER_SIZE];
static std::atomic<size_t> audioWritePos{0};
static std::atomic<size_t> audioReadPos{0};

// SDL Audio callback
void AudioCallback(void *userdata, Uint8 *stream, int len) {
  float *output = (float *)stream;
  int samples = len / sizeof(float);

  size_t readPos = audioReadPos.load(std::memory_order_relaxed);
  size_t writePos = audioWritePos.load(std::memory_order_acquire);

  for (int i = 0; i < samples; i++) {
    if (readPos < writePos) {
      output[i] = audioBuffer[readPos % AUDIO_BUFFER_SIZE];
      readPos++;
    } else {
      // Buffer underrun - output last sample or silence to reduce pops
      if (i > 0) {
        output[i] = output[i - 1] * 0.9f; // Fade out to reduce clicks
      } else {
        output[i] = 0.0f;
      }
    }
  }

  audioReadPos.store(readPos, std::memory_order_release);
}

int main(int argc, char *argv[]) {
  Config config;
  config.Load("config.ini");

  std::string romPath = "";
  if (argc > 1) {
    romPath = argv[1];
  }

  Bus nes;
  Display display;

  if (!display.Init("NES Emulator", 256, 240, config.windowScale)) {
    std::cerr << "Failed to initialize display!" << std::endl;
    return 1;
  }

  // Initialize audio buffer
  for (size_t i = 0; i < AUDIO_BUFFER_SIZE; i++) {
    audioBuffer[i] = 0.0f;
  }

  // Initialize SDL Audio with larger buffer
  SDL_AudioSpec audioSpec;
  SDL_zero(audioSpec);
  audioSpec.freq = 44100;
  audioSpec.format = AUDIO_F32SYS;
  audioSpec.channels = 1;
  audioSpec.samples = 1024; // Larger buffer to reduce underruns
  audioSpec.callback = AudioCallback;
  audioSpec.userdata = nullptr;

  SDL_AudioDeviceID audioDevice =
      SDL_OpenAudioDevice(nullptr, 0, &audioSpec, nullptr, 0);
  if (audioDevice == 0) {
    std::cerr << "Failed to open audio: " << SDL_GetError() << std::endl;
  } else {
    SDL_PauseAudioDevice(audioDevice, 0);
    std::cout << "Audio initialized at 44100 Hz" << std::endl;
  }

  std::cout << "NES Emulator Started" << std::endl;
  std::cout << "Use File menu or press F1 to load a ROM" << std::endl;
  std::cout << "Press P to pause/resume" << std::endl;

  bool running = true;
  bool romLoaded = false;
  bool paused = false;

  if (!romPath.empty()) {
    std::shared_ptr<Cartridge> cart = std::make_shared<Cartridge>(romPath);
    if (cart->ImageValid()) {
      nes.insertCartridge(cart);
      nes.reset();
      romLoaded = true;
      std::cout << "Loaded: " << romPath << std::endl;
    } else {
      std::cerr << "Failed to load ROM: " << romPath << std::endl;
    }
  }

  bool loadNewRom = false;
  std::string newRomPath = "";
  int menuCommand = 0;

  // Audio timing - NES CPU runs at 1.789773 MHz (NTSC)
  // We sample at 44100 Hz
  // That means we sample every ~40.58 CPU cycles
  const double CPU_FREQ = 1789773.0;
  const double SAMPLE_FREQ = 44100.0;
  const double CYCLES_PER_SAMPLE = CPU_FREQ / SAMPLE_FREQ;
  double audioSampleCounter = 0.0;

  // Low-pass filter for anti-aliasing
  double lastSample = 0.0;
  const double FILTER_ALPHA = 0.4; // Simple low-pass filter coefficient

  while (running) {
    display.HandleEvents(running, nes.controller[0], config, loadNewRom,
                         newRomPath, menuCommand);

    if (menuCommand == ID_EMU_RESET && romLoaded) {
      nes.reset();
      std::cout << "Emulator reset" << std::endl;
    } else if (menuCommand == ID_EMU_PAUSE) {
      paused = !paused;
      SDL_PauseAudioDevice(audioDevice, paused ? 1 : 0);
      std::cout << (paused ? "Paused" : "Resumed") << std::endl;
    }

    if (loadNewRom && !newRomPath.empty()) {
      loadNewRom = false;
      std::shared_ptr<Cartridge> cart = std::make_shared<Cartridge>(newRomPath);
      if (cart->ImageValid()) {
        nes.insertCartridge(cart);
        nes.reset();
        romLoaded = true;
        paused = false;
        audioWritePos = 0;
        audioReadPos = 0;
        std::cout << "Loaded: " << newRomPath << std::endl;
      } else {
        std::cerr << "Failed to load ROM: " << newRomPath << std::endl;
      }
      newRomPath = "";
    }

    if (romLoaded && !paused) {
      do {
        nes.clock();

        // Sample audio at correct rate
        // Master clock is 3x CPU clock, so we need to account for that
        audioSampleCounter += 1.0 / 3.0; // Convert PPU clocks to CPU cycles

        if (audioSampleCounter >= CYCLES_PER_SAMPLE) {
          audioSampleCounter -= CYCLES_PER_SAMPLE;

          double rawSample = nes.GetAudioSample();

          // Simple low-pass filter to reduce high-frequency noise
          double filteredSample =
              lastSample + FILTER_ALPHA * (rawSample - lastSample);
          lastSample = filteredSample;

          // Write to ring buffer (lock-free)
          size_t writePos = audioWritePos.load(std::memory_order_relaxed);
          size_t readPos = audioReadPos.load(std::memory_order_acquire);

          // Only write if buffer not full
          if (writePos - readPos < AUDIO_BUFFER_SIZE - 1) {
            audioBuffer[writePos % AUDIO_BUFFER_SIZE] =
                (float)(filteredSample * 0.5);
            audioWritePos.store(writePos + 1, std::memory_order_release);
          }
        }
      } while (!nes.ppu.frame_complete);
      nes.ppu.frame_complete = false;
    }

    display.Update(nes.ppu.screen);
  }

  if (audioDevice != 0) {
    SDL_CloseAudioDevice(audioDevice);
  }

  config.Save("config.ini");
  display.Close();
  return 0;
}
