#include "Display.h"
#include "Platform.h"
#include "include/SDL2/SDL_syswm.h"
#include <iostream>

#ifdef _WIN32
#include <windows.h>
#endif

Display::Display() {}

Display::~Display() { Close(); }

bool Display::Init(const char *title, int width, int height, int scale) {
  if (SDL_Init(SDL_INIT_VIDEO | SDL_INIT_AUDIO) < 0) {
    std::cerr << "SDL Init failed: " << SDL_GetError() << std::endl;
    return false;
  }

  screenWidth = width;
  screenHeight = height;
  currentScale = scale;

  window = SDL_CreateWindow(
      title, SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED, width * scale,
      height * scale, SDL_WINDOW_SHOWN | SDL_WINDOW_RESIZABLE);

  if (!window) {
    std::cerr << "Window creation failed: " << SDL_GetError() << std::endl;
    return false;
  }

  // Attach Menu Bar (Windows only)
  SDL_SysWMinfo wmInfo;
  SDL_VERSION(&wmInfo.version);
  if (SDL_GetWindowWMInfo(window, &wmInfo)) {
    if (wmInfo.subsystem == SDL_SYSWM_WINDOWS) {
      Platform_CreateMenu(wmInfo.info.win.window);
    }
  }

  // Enable syswm events
  SDL_EventState(SDL_SYSWMEVENT, SDL_ENABLE);

  renderer = SDL_CreateRenderer(
      window, -1, SDL_RENDERER_ACCELERATED | SDL_RENDERER_PRESENTVSYNC);
  if (!renderer) {
    std::cerr << "Renderer creation failed: " << SDL_GetError() << std::endl;
    return false;
  }

  // Set render draw color for letterbox bars
  SDL_SetRenderDrawColor(renderer, 0, 0, 0, 255);

  texture = SDL_CreateTexture(renderer, SDL_PIXELFORMAT_RGBA32,
                              SDL_TEXTUREACCESS_STREAMING, width, height);

  if (!texture) {
    std::cerr << "Texture creation failed: " << SDL_GetError() << std::endl;
    return false;
  }

  return true;
}

void Display::SetScale(int scale) {
  if (window && scale >= 1 && scale <= 4) {
    currentScale = scale;
    SDL_SetWindowSize(window, screenWidth * scale, screenHeight * scale);
    SDL_SetWindowPosition(window, SDL_WINDOWPOS_CENTERED,
                          SDL_WINDOWPOS_CENTERED);
  }
}

void Display::Update(Pixel *screen) {
  SDL_UpdateTexture(texture, nullptr, screen, screenWidth * sizeof(Pixel));

  // Clear with black for letterbox bars
  SDL_SetRenderDrawColor(renderer, 0, 0, 0, 255);
  SDL_RenderClear(renderer);

  // Get window size
  int windowW, windowH;
  SDL_GetWindowSize(window, &windowW, &windowH);

  // Calculate aspect-ratio preserving destination rectangle
  float nesAspect =
      (float)screenWidth / (float)screenHeight; // 256/240 = 1.0667
  float windowAspect = (float)windowW / (float)windowH;

  SDL_Rect destRect;

  if (windowAspect > nesAspect) {
    // Window is wider than NES - pillarbox (black bars on sides)
    destRect.h = windowH;
    destRect.w = (int)(windowH * nesAspect);
    destRect.x = (windowW - destRect.w) / 2;
    destRect.y = 0;
  } else {
    // Window is taller than NES - letterbox (black bars on top/bottom)
    destRect.w = windowW;
    destRect.h = (int)(windowW / nesAspect);
    destRect.x = 0;
    destRect.y = (windowH - destRect.h) / 2;
  }

  SDL_RenderCopy(renderer, texture, nullptr, &destRect);
  SDL_RenderPresent(renderer);
}

void Display::HandleEvents(bool &running, uint8_t &controller, Config &config,
                           bool &loadNewRom, std::string &newRomPath,
                           int &menuCommand) {
  menuCommand = 0;
  SDL_Event event;
  while (SDL_PollEvent(&event)) {
    if (event.type == SDL_QUIT) {
      running = false;
    }

#ifdef _WIN32
    // Handle Windows Menu Commands
    if (event.type == SDL_SYSWMEVENT) {
      if (event.syswm.msg->msg.win.msg == WM_COMMAND) {
        int cmdId = LOWORD(event.syswm.msg->msg.win.wParam);
        menuCommand = cmdId;

        // Handle Open ROM directly
        if (cmdId == ID_FILE_OPEN) {
          std::string path = Platform_OpenFileDialog();
          if (!path.empty()) {
            newRomPath = path;
            loadNewRom = true;
          }
        } else if (cmdId == ID_FILE_EXIT) {
          running = false;
        }
        // Handle Scale commands directly
        else if (cmdId >= ID_SCALE_1X && cmdId <= ID_SCALE_4X) {
          int newScale = cmdId - ID_SCALE_1X + 1;
          SetScale(newScale);
          config.windowScale = newScale;
        }
      }
    }
#endif

    if (event.type == SDL_KEYDOWN) {
      SDL_Keycode key = event.key.keysym.sym;

      if (key == SDLK_ESCAPE) {
        running = false;
      } else if (key == SDLK_F1) {
        std::string path = Platform_OpenFileDialog();
        if (!path.empty()) {
          newRomPath = path;
          loadNewRom = true;
        }
      } else if (key == SDLK_p) {
        // P key for pause - pass command to main
        menuCommand = ID_EMU_PAUSE;
      } else if (key == config.keys.a)
        controller |= 0x80;
      else if (key == config.keys.b)
        controller |= 0x40;
      else if (key == config.keys.select)
        controller |= 0x20;
      else if (key == config.keys.start)
        controller |= 0x10;
      else if (key == config.keys.up)
        controller |= 0x08;
      else if (key == config.keys.down)
        controller |= 0x04;
      else if (key == config.keys.left)
        controller |= 0x02;
      else if (key == config.keys.right)
        controller |= 0x01;
    }

    if (event.type == SDL_KEYUP) {
      SDL_Keycode key = event.key.keysym.sym;

      if (key == config.keys.a)
        controller &= ~0x80;
      else if (key == config.keys.b)
        controller &= ~0x40;
      else if (key == config.keys.select)
        controller &= ~0x20;
      else if (key == config.keys.start)
        controller &= ~0x10;
      else if (key == config.keys.up)
        controller &= ~0x08;
      else if (key == config.keys.down)
        controller &= ~0x04;
      else if (key == config.keys.left)
        controller &= ~0x02;
      else if (key == config.keys.right)
        controller &= ~0x01;
    }
  }
}

void Display::Close() {
  if (texture) {
    SDL_DestroyTexture(texture);
    texture = nullptr;
  }
  if (renderer) {
    SDL_DestroyRenderer(renderer);
    renderer = nullptr;
  }
  if (window) {
    SDL_DestroyWindow(window);
    window = nullptr;
  }
  SDL_Quit();
}
