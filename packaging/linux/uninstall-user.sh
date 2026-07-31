#!/usr/bin/env bash
set -euo pipefail

install_dir="$HOME/.local/opt/tidverk"
desktop_file="$HOME/.local/share/applications/tidverk.desktop"
icon_file="$HOME/.local/share/icons/hicolor/256x256/apps/tidverk.png"

rm -rf -- "$install_dir"
rm -f -- "$desktop_file" "$icon_file"
gtk-update-icon-cache -f -t "$HOME/.local/share/icons/hicolor" 2>/dev/null || true
update-desktop-database "$HOME/.local/share/applications" 2>/dev/null || true
echo "Tidverk application files removed. Local reports and database were kept."
