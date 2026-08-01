#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
rid="${1:-}"

case "$rid" in
  linux-x64)
    channel="linux"
    directive="[linux]"
    executable="Tidverk"
    icon="$repo_root/src/Tidverk.App/Assets/Brand/tidverk-app-icon.png"
    ;;
  win-x64)
    channel="win"
    directive="[win]"
    executable="Tidverk.exe"
    icon="$repo_root/src/Tidverk.App/Assets/Brand/tidverk-app-icon.ico"
    ;;
  *)
    echo "Usage: $0 <linux-x64|win-x64>" >&2
    exit 2
    ;;
esac

"$repo_root/scripts/publish-rid.sh" "$rid"

version="$(dotnet msbuild "$repo_root/src/Tidverk.App/Tidverk.App.csproj" -getProperty:Version -nologo)"
release_dir="$repo_root/artifacts/releases/$channel"
publish_dir="$repo_root/artifacts/publish/$rid"
release_notes="$repo_root/docs/release-notes/$version.md"

rm -rf -- "$release_dir"
mkdir -p "$release_dir"

if [[ "${TIDVERK_SKIP_RELEASE_DOWNLOAD:-0}" != "1" ]]; then
  dotnet dnx vpk --version 1.2.0 --yes -- --legacyConsole download github \
    --repoUrl https://github.com/agneswd/Tidverk \
    --channel "$channel" \
    --outputDir "$release_dir"
fi

pack_arguments=(
  --packId Tidverk
  --packVersion "$version"
  --packDir "$publish_dir"
  --mainExe "$executable"
  --packTitle Tidverk
  --packAuthors agneswd
  --runtime "$rid"
  --channel "$channel"
  --icon "$icon"
  --outputDir "$release_dir"
)

if [[ -f "$release_notes" ]]; then
  pack_arguments+=(--releaseNotes "$release_notes")
fi

if [[ "$rid" == "linux-x64" ]]; then
  pack_arguments+=(--categories "Office;Utility")
fi

dotnet dnx vpk --version 1.2.0 --yes -- "$directive" --legacyConsole pack "${pack_arguments[@]}"

if [[ "$rid" == "linux-x64" ]]; then
  extras_dir="$repo_root/artifacts/extras/linux"
  bundle_name="Tidverk-$version-linux-x64"
  bundle_dir="$extras_dir/$bundle_name"
  appimage="$(find "$release_dir" -maxdepth 1 -type f -name '*.AppImage' -print -quit)"
  rm -rf -- "$extras_dir"
  mkdir -p "$bundle_dir"
  install -m 0755 "$appimage" "$bundle_dir/Tidverk.AppImage"
  install -m 0755 "$repo_root/packaging/linux/install-user.sh" "$bundle_dir/install.sh"
  install -m 0755 "$repo_root/packaging/linux/uninstall-user.sh" "$bundle_dir/uninstall.sh"
  install -m 0644 "$repo_root/packaging/linux/tidverk.desktop" "$bundle_dir/tidverk.desktop"
  install -m 0644 "$repo_root/packaging/linux/tidverk.png" "$bundle_dir/tidverk.png"
  tar -C "$extras_dir" -czf "$extras_dir/$bundle_name.tar.gz" "$bundle_name"
  rm -rf -- "$bundle_dir"
fi

echo "Packaged Tidverk $version for $rid in $release_dir"
