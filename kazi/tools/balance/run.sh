#!/bin/bash
# Kullanım: tools/balance/run.sh [Sim argümanları]   (örn. --seeds 8 --hours 40)
# JSON'ları params.json'dan üretir, sonra denge simülasyonunu çalıştırır.
export DOTNET_ROOT=${DOTNET_ROOT:-/opt/dotnet}; export PATH=$PATH:$DOTNET_ROOT
cd "$(dirname "$0")"
python3 gen_upgrades.py && python3 gen_cards.py x >/dev/null && cd ../.. && dotnet run --project Sim -c Release -- "$@"
