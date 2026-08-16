@echo off
echo Building legacy C++ mgkEMU...
g++ -O3 -IC:/msys64/ucrt64/include -LC:/msys64/ucrt64/lib -o mgkEMU_legacy.exe srcLegacy/*.cpp -lmingw32 -lSDL2main -lSDL2 -mwindows

if %errorlevel% neq 0 (
    echo.
    echo Build failed!
    exit /b %errorlevel%
)

echo.
echo Build successful! (mgkEMU_legacy.exe)
