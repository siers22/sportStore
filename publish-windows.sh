#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
dotnet publish SportStore/SportStore.csproj -c Release -r win-x64 --self-contained true \
  -p:UseAppHost=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -o release/windows
cp run-portable-windows.cmd release/windows/start.cmd
(cd release && zip -r -q -FS sportstore-windows.zip windows -x '*.DS_Store')
echo "Ready: release/sportstore-windows.zip (send this archive to the Windows laptop)."
