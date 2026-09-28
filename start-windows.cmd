@echo off
cd /d "%~dp0"
if exist "release\windows\start.cmd" (
  call "release\windows\start.cmd"
  exit /b %errorlevel%
)
echo Portable build is missing.
echo Run publish-windows.sh on the developer computer and copy release\windows to this laptop.
pause
exit /b 1
