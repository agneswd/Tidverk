#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

dotnet restore Tidverk.sln --disable-parallel
dotnet build Tidverk.sln --configuration Release --no-restore -m:1 -nodeReuse:false
dotnet test Tidverk.sln --configuration Release --no-build -m:1 -nodeReuse:false
dotnet format Tidverk.sln --no-restore --verify-no-changes
