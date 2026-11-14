# Run in PowerShell (Windows x64) after installing MinGW-w64 GCC (e.g., MSYS2 UCRT64).
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
New-Item -Force -ItemType Directory 'native/runtime/win-x64' | Out-Null
& gcc -std=c11 -O2 -Wall -Wextra -Werror -shared native/deliveryfees.c -o native/runtime/win-x64/deliveryfees.dll
if ($LASTEXITCODE -ne 0) { throw 'GCC native compilation failed.' }
Write-Host 'Built native/runtime/win-x64/deliveryfees.dll for Windows x64'
