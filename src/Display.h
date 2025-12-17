#pragma once

#include "Config.h"
#include "PPU2C02.h"
#include "include/SDL2/SDL.h"
#include <cstdint>
#include <string>

class Display {
public:
  Display();
  ~Display();

  bool Init(const char *title, int width, int height, int scale);
  void Update(Pixel *screen);
  void HandleEvents(bool &running, uint8_t &controller, Config &config,
                    bool &loadNewRom, std::string &newRomPath,
                    int &menuCommand);
  void Close();
  void SetScale(int scale);

private:
  SDL_Window *window = nullptr;
  SDL_Renderer *renderer = nullptr;
  SDL_Texture *texture = nullptr;

  int screenWidth = 256;
  int screenHeight = 240;
  int currentScale = 3;
};
