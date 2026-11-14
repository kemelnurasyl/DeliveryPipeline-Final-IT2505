#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
./scripts/build-native.sh
dotnet run --project DeliveryPipeline.App -- samples/report-orders.json reports/report.csv routine
