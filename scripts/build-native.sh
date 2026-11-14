#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p native/runtime/linux-x64
gcc -std=c11 -O2 -Wall -Wextra -Werror -fPIC -shared \
  native/deliveryfees.c -o native/runtime/linux-x64/libdeliveryfees.so
printf 'Built native/runtime/linux-x64/libdeliveryfees.so for Linux x64\n'
