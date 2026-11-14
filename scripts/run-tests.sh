#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
./scripts/build-native.sh
dotnet run --project DeliveryPipeline.Tests
