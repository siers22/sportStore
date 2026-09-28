@echo off
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo ASP.NET Core Runtime 8 is not installed.
  echo Install it from https://dotnet.microsoft.com/download/dotnet/8.0
  pause
  exit /b 1
)
dotnet --list-runtimes | findstr /C:"Microsoft.AspNetCore.App 8." >nul
if errorlevel 1 (
  echo ASP.NET Core Runtime 8 is not installed.
  echo Install it from https://dotnet.microsoft.com/download/dotnet/8.0
  pause
  exit /b 1
)
echo Starting SportStore at http://localhost:5080
echo Close this window to stop the store.
start "" cmd /c "timeout /t 4 /nobreak >nul & start http://localhost:5080"
dotnet SportStore.dll --urls http://127.0.0.1:5080
if errorlevel 1 (
  echo.
  echo The store could not start. See the error above.
  pause
)
