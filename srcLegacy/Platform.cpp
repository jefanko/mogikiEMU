#include "Platform.h"

#ifdef _WIN32
// Optimize Windows headers and avoid conflicts
#define WIN32_LEAN_AND_MEAN
#include <windows.h>

// Explicitly include commdlg after windows
#include <commdlg.h>

std::string Platform_OpenFileDialog() {
  OPENFILENAMEA ofn;
  char filename[MAX_PATH] = "";

  ZeroMemory(&ofn, sizeof(ofn));
  ofn.lStructSize = sizeof(ofn);
  ofn.hwndOwner = NULL; // Use NULL or GetActiveWindow()
  ofn.lpstrFilter = "NES ROMs (*.nes)\0*.nes\0All Files (*.*)\0*.*\0";
  ofn.lpstrFile = filename;
  ofn.nMaxFile = MAX_PATH;
  ofn.lpstrTitle = "Select a NES ROM";
  ofn.Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST;

  if (GetOpenFileNameA(&ofn)) {
    return std::string(filename);
  }
  return "";
}

void Platform_CreateMenu(void *nativeWindowHandle) {
  HWND hwnd = (HWND)nativeWindowHandle;
  HMENU hMenu = CreateMenu();

  // File Menu
  HMENU hFileMenu = CreatePopupMenu();
  AppendMenu(hFileMenu, MF_STRING, ID_FILE_OPEN, "Open ROM...\tF1");
  AppendMenu(hFileMenu, MF_SEPARATOR, 0, NULL);
  AppendMenu(hFileMenu, MF_STRING, ID_FILE_EXIT, "Exit\tESC");
  AppendMenu(hMenu, MF_POPUP, (UINT_PTR)hFileMenu, "File");

  // Emulation Menu
  HMENU hEmuMenu = CreatePopupMenu();
  AppendMenu(hEmuMenu, MF_STRING, ID_EMU_RESET, "Reset");
  AppendMenu(hEmuMenu, MF_STRING, ID_EMU_PAUSE, "Pause/Resume\tP");
  AppendMenu(hMenu, MF_POPUP, (UINT_PTR)hEmuMenu, "Emulation");

  // Scale Menu
  HMENU hScaleMenu = CreatePopupMenu();
  AppendMenu(hScaleMenu, MF_STRING, ID_SCALE_1X, "1x");
  AppendMenu(hScaleMenu, MF_STRING, ID_SCALE_2X, "2x");
  AppendMenu(hScaleMenu, MF_STRING, ID_SCALE_3X, "3x");
  AppendMenu(hScaleMenu, MF_STRING, ID_SCALE_4X, "4x");
  AppendMenu(hMenu, MF_POPUP, (UINT_PTR)hScaleMenu, "Scale");

  SetMenu(hwnd, hMenu);
}

// NOTE: SDL handles the message loop, so we might not need to manually handle
// messages if we use SDL_Event with SDL_SYSWMEVENT. However, for standard menus
// attached to window, Windows sends WM_COMMAND. SDL exposes this via
// SDL_SYSWMEVENT.

#else
// Fallback for non-Windows
std::string Platform_OpenFileDialog() { return ""; }
void Platform_CreateMenu(void *nativeWindowHandle) {}
#endif
