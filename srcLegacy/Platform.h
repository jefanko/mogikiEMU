#pragma once
#include <string>

// Menu Command IDs
#define ID_FILE_OPEN 1001
#define ID_FILE_EXIT 1002
#define ID_EMU_RESET 2001
#define ID_EMU_PAUSE 2002
#define ID_SCALE_1X 3001
#define ID_SCALE_2X 3002
#define ID_SCALE_3X 3003
#define ID_SCALE_4X 3004

// Cross-platform helper functions
std::string Platform_OpenFileDialog();
void Platform_CreateMenu(void *nativeWindowHandle);
int Platform_HandleMenuMessage(void *msg);
