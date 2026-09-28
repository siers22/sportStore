@echo off
cd /d "%~dp0"
echo Starting SportStore at http://localhost:5080
echo Close this window to stop the store.
start "" cmd /c "timeout /t 4 /nobreak >nul & start http://localhost:5080"
SportStore.exe --urls http://127.0.0.1:5080
if errorlevel 1 (
  echo.
  echo The store could not start. See the error above.
  pause
)
