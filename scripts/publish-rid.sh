#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
rid="${1:-}"

case "$rid" in
  linux-x64|win-x64|osx-x64|osx-arm64) ;;
  *)
    echo "Usage: $0 <linux-x64|win-x64|osx-x64|osx-arm64>" >&2
    exit 2
    ;;
esac

output="$repo_root/artifacts/publish/$rid"

dotnet restore "$repo_root/src/Tidverk.App/Tidverk.App.csproj" \
  --runtime "$rid" \
  --disable-parallel

dotnet publish "$repo_root/src/Tidverk.App/Tidverk.App.csproj" \
  --configuration Release \
  --runtime "$rid" \
  --self-contained true \
  --no-restore \
  --output "$output" \
  -p:PublishSingleFile=false \
  -m:1 \
  -nodeReuse:false

echo "Published Tidverk to $output"
