#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
publish_dir="$repo_root/artifacts/publish/linux-x64"
install_dir="$HOME/.local/opt/tidverk"

if [[ ! -x "$publish_dir/Tidverk" ]]; then
  "$repo_root/scripts/publish-linux-x64.sh"
fi

install -d "$install_dir" "$HOME/.local/share/applications" "$HOME/.local/share/icons/hicolor/scalable/apps"
cp -a "$publish_dir/." "$install_dir/"
install -m 0644 "$repo_root/packaging/linux/tidverk.desktop" "$HOME/.local/share/applications/tidverk.desktop"
install -m 0644 "$repo_root/assets/brand/tidverk-app-icon-glass.svg" "$HOME/.local/share/icons/hicolor/scalable/apps/tidverk.svg"
update-desktop-database "$HOME/.local/share/applications" 2>/dev/null || true
echo "Tidverk installed for the current user."
