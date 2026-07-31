#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
publish_dir="$repo_root/artifacts/publish/linux-x64"
install_dir="$HOME/.local/opt/tidverk"
icon_dir="$HOME/.local/share/icons/hicolor/256x256/apps"
old_scalable_icon="$HOME/.local/share/icons/hicolor/scalable/apps/tidverk.svg"

if [[ ! -x "$publish_dir/Tidverk" ]]; then
  "$repo_root/scripts/publish-linux-x64.sh"
fi

install -d "$install_dir" "$HOME/.local/share/applications" "$icon_dir"
cp -a --remove-destination "$publish_dir/." "$install_dir/"
install -m 0644 "$repo_root/packaging/linux/tidverk.desktop" "$HOME/.local/share/applications/tidverk.desktop"
install -m 0644 "$repo_root/packaging/linux/tidverk.png" "$icon_dir/tidverk.png"
rm -f "$old_scalable_icon"
gtk-update-icon-cache -f -t "$HOME/.local/share/icons/hicolor" 2>/dev/null || true
update-desktop-database "$HOME/.local/share/applications" 2>/dev/null || true
echo "Tidverk installed for the current user."
