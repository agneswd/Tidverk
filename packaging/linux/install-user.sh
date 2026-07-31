#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "$script_dir/../.." && pwd)"
if [[ -x "$script_dir/app/Tidverk" ]]; then
  publish_dir="$script_dir/app"
  desktop_source="$script_dir/tidverk.desktop"
  icon_source="$script_dir/tidverk.png"
else
  publish_dir="$repo_root/artifacts/publish/linux-x64"
  desktop_source="$repo_root/packaging/linux/tidverk.desktop"
  icon_source="$repo_root/packaging/linux/tidverk.png"
fi
install_dir="$HOME/.local/opt/tidverk"
desktop_target="$HOME/.local/share/applications/tidverk.desktop"
icon_dir="$HOME/.local/share/icons/hicolor/256x256/apps"
old_scalable_icon="$HOME/.local/share/icons/hicolor/scalable/apps/tidverk.svg"

if [[ ! -x "$publish_dir/Tidverk" ]]; then
  "$repo_root/scripts/publish-linux-x64.sh"
fi

install -d "$install_dir" "$HOME/.local/share/applications" "$icon_dir"
cp -a --remove-destination "$publish_dir/." "$install_dir/"
desktop_temp="$(mktemp)"
trap 'rm -f -- "$desktop_temp"' EXIT
sed "s|@EXECUTABLE@|$install_dir/Tidverk|g" "$desktop_source" > "$desktop_temp"
install -m 0644 "$desktop_temp" "$desktop_target"
install -m 0644 "$icon_source" "$icon_dir/tidverk.png"
rm -f "$old_scalable_icon"
gtk-update-icon-cache -f -t "$HOME/.local/share/icons/hicolor" 2>/dev/null || true
update-desktop-database "$HOME/.local/share/applications" 2>/dev/null || true
echo "Tidverk installed for the current user."
