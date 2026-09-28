#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
dotnet publish SportStore/SportStore.csproj -c Release -r win-x64 --self-contained false \
  -p:UseAppHost=false -o release/windows-dotnet
cp run-dotnet-windows.cmd release/windows-dotnet/start.cmd
(cd release && zip -r -q -FS sportstore-windows-dotnet.zip windows-dotnet -x '*.DS_Store')
echo "Ready: release/sportstore-windows-dotnet.zip"
