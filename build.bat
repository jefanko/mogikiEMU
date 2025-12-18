@echo off
echo Compiling resources...
windres resources.rc -o resources.o

echo Building mgkEMU...
g++ -o mgkEMU src/*.cpp resources.o -lmingw32 -lSDL2main -lSDL2 -mwindows -lsetupapi -luser32 -lgdi32 -lwinmm -limm32 -lole32 -loleaut32 -lshell32 -lversion -luuid

if %errorlevel% neq 0 (
    echo.
    echo Build failed!
    pause
    exit /b %errorlevel%
)

echo.
echo Build successful!
del resources.o
pause
