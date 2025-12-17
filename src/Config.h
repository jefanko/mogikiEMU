#pragma once
#include "include/SDL2/SDL.h"
#include <cstdint>
#include <fstream>
#include <string>


struct KeyBindings {
  SDL_Keycode up = SDLK_UP;
  SDL_Keycode down = SDLK_DOWN;
  SDL_Keycode left = SDLK_LEFT;
  SDL_Keycode right = SDLK_RIGHT;
  SDL_Keycode a = SDLK_x;
  SDL_Keycode b = SDLK_z;
  SDL_Keycode start = SDLK_s;
  SDL_Keycode select = SDLK_a;
};

class Config {
public:
  Config();

  void Load(const std::string &filename = "config.ini");
  void Save(const std::string &filename = "config.ini");

  KeyBindings keys;
  int windowScale = 3;
  std::string lastRomPath = "";
};
