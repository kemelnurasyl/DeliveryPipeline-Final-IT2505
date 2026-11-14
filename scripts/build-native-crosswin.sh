#!/usr/bin/env bash
# Optional cross-build of the Windows x64 DLL from Linux using Clang/LLD.
# This is a no-CRT DLL: a deterministic integer function with no entry point.
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p native/runtime/win-x64
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
clang-cl /nologo /O2 /c /TC /Fo"$work/deliveryfees.obj" native/deliveryfees.c
lld-link /DLL /NOENTRY /NODEFAULTLIB /MACHINE:X64 \
  /OUT:"$(pwd)/native/runtime/win-x64/deliveryfees.dll" \
  /EXPORT:delivery_reserve_minor "$work/deliveryfees.obj"
echo 'Built native/runtime/win-x64/deliveryfees.dll for Windows x64'
