#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
rid="${1:-}"

case "$rid" in
  linux-x64|win-x64) ;;
  *)
    echo "Usage: $0 <linux-x64|win-x64>" >&2
    exit 2
    ;;
esac

"$repo_root/scripts/publish-rid.sh" "$rid"

version="$(dotnet msbuild "$repo_root/src/Tidverk.App/Tidverk.App.csproj" -getProperty:Version -nologo)"
package_name="Tidverk-$version-$rid"
packages_dir="$repo_root/artifacts/packages"
stage="$packages_dir/.staging/$package_name"

rm -rf -- "$stage"
mkdir -p "$stage/app"
cp -a "$repo_root/artifacts/publish/$rid/." "$stage/app/"
printf '%s\n' "$version" > "$stage/version.txt"

case "$rid" in
  linux-x64)
    cp "$repo_root/packaging/linux/install-user.sh" "$stage/install.sh"
    cp "$repo_root/packaging/linux/uninstall-user.sh" "$stage/uninstall.sh"
    cp "$repo_root/packaging/linux/tidverk.desktop" "$stage/tidverk.desktop"
    cp "$repo_root/packaging/linux/tidverk.png" "$stage/tidverk.png"
    chmod +x "$stage/install.sh" "$stage/uninstall.sh"
    archive="$packages_dir/$package_name.tar.gz"
    rm -f -- "$archive" "$archive.sha256"
    tar -C "$packages_dir/.staging" -czf "$archive" "$package_name"
    ;;
  win-x64)
    cp "$repo_root/packaging/windows/install-user.ps1" "$stage/install.ps1"
    cp "$repo_root/packaging/windows/uninstall-user.ps1" "$stage/uninstall-user.ps1"
    archive="$packages_dir/$package_name.zip"
    rm -f -- "$archive" "$archive.sha256"
    (
      cd "$packages_dir/.staging"
      7z a -tzip -mx=9 "$archive" "$package_name" >/dev/null
    )
    ;;
esac

sha256sum "$archive" > "$archive.sha256"
echo "Packaged Tidverk to $archive"
