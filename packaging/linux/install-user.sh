#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "$script_dir/../.." && pwd)"
appimage_source="${1:-}"

if [[ -z "$appimage_source" ]]; then
  appimage_source="$(find "$script_dir" -maxdepth 1 -type f -name '*.AppImage' -print -quit)"
fi
if [[ -z "$appimage_source" && -d "$repo_root/artifacts/releases/linux" ]]; then
  appimage_source="$(find "$repo_root/artifacts/releases/linux" -maxdepth 1 -type f -name '*.AppImage' -print -quit)"
fi
if [[ -z "$appimage_source" || ! -f "$appimage_source" ]]; then
  echo "Usage: $0 [path-to-Tidverk.AppImage]" >&2
  exit 2
fi

install_dir="$HOME/.local/opt/tidverk"
appimage_target="$install_dir/Tidverk.AppImage"
desktop_target="$HOME/.local/share/applications/tidverk.desktop"
icon_dir="$HOME/.local/share/icons/hicolor/256x256/apps"
desktop_source="$script_dir/tidverk.desktop"
icon_source="$script_dir/tidverk.png"

install -d "$install_dir" "$HOME/.local/share/applications" "$icon_dir"
appimage_temp="$(mktemp "$install_dir/.Tidverk.AppImage.XXXXXX")"
desktop_temp="$(mktemp)"
trap 'rm -f -- "$appimage_temp" "$desktop_temp"' EXIT
install -m 0755 "$appimage_source" "$appimage_temp"
mv -f -- "$appimage_temp" "$appimage_target"
sed "s|@EXECUTABLE@|$appimage_target|g" "$desktop_source" > "$desktop_temp"
install -m 0644 "$desktop_temp" "$desktop_target"
install -m 0644 "$icon_source" "$icon_dir/tidverk.png"
rm -f "$HOME/.local/share/icons/hicolor/scalable/apps/tidverk.svg"
gtk-update-icon-cache -f -t "$HOME/.local/share/icons/hicolor" 2>/dev/null || true
update-desktop-database "$HOME/.local/share/applications" 2>/dev/null || true
echo "Tidverk installed for the current user."
