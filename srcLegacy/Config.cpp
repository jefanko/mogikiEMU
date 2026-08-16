#include "Config.h"
#include <sstream>

Config::Config() {}

void Config::Load(const std::string &filename) {
  std::ifstream file(filename);
  if (!file.is_open())
    return;

  std::string line;
  while (std::getline(file, line)) {
    if (line.empty() || line[0] == '#' || line[0] == '[')
      continue;

    size_t eq = line.find('=');
    if (eq == std::string::npos)
      continue;

    std::string key = line.substr(0, eq);
    std::string value = line.substr(eq + 1);

    // Trim whitespace
    while (!key.empty() && key.back() == ' ')
      key.pop_back();
    while (!value.empty() && value.front() == ' ')
      value.erase(0, 1);

    if (key == "up")
      keys.up = (SDL_Keycode)std::stoi(value);
    else if (key == "down")
      keys.down = (SDL_Keycode)std::stoi(value);
    else if (key == "left")
      keys.left = (SDL_Keycode)std::stoi(value);
    else if (key == "right")
      keys.right = (SDL_Keycode)std::stoi(value);
    else if (key == "a")
      keys.a = (SDL_Keycode)std::stoi(value);
    else if (key == "b")
      keys.b = (SDL_Keycode)std::stoi(value);
    else if (key == "start")
      keys.start = (SDL_Keycode)std::stoi(value);
    else if (key == "select")
      keys.select = (SDL_Keycode)std::stoi(value);
    else if (key == "scale")
      windowScale = std::stoi(value);
    else if (key == "lastrom")
      lastRomPath = value;
  }
  file.close();
}

void Config::Save(const std::string &filename) {
  std::ofstream file(filename);
  if (!file.is_open())
    return;

  file << "# NES Emulator Config\n";
  file << "[Controls]\n";
  file << "up=" << keys.up << "\n";
  file << "down=" << keys.down << "\n";
  file << "left=" << keys.left << "\n";
  file << "right=" << keys.right << "\n";
  file << "a=" << keys.a << "\n";
  file << "b=" << keys.b << "\n";
  file << "start=" << keys.start << "\n";
  file << "select=" << keys.select << "\n";
  file << "\n[Display]\n";
  file << "scale=" << windowScale << "\n";
  file << "\n[Misc]\n";
  file << "lastrom=" << lastRomPath << "\n";

  file.close();
}
