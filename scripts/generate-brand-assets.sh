#!/bin/sh
set -eu

repo_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
source_svg="$repo_root/assets/brand/tidverk-app-icon.svg"
target_dir="$repo_root/src/Tidverk.App/Assets/Brand"
target_png="$target_dir/tidverk-app-icon.png"
target_ico="$target_dir/tidverk-app-icon.ico"
target_linux_png="$repo_root/packaging/linux/tidverk.png"

command -v rsvg-convert >/dev/null
command -v magick >/dev/null

rsvg-convert --width 1024 --height 1024 "$source_svg" --output "$target_png"
rsvg-convert --width 256 --height 256 "$source_svg" --output "$target_linux_png"
magick "$target_png" -define icon:auto-resize=256,128,64,48,32,16 "$target_ico"

echo "Generated $target_png, $target_ico, and $target_linux_png"
