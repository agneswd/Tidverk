$ErrorActionPreference = "Stop"

$installDirectory = Join-Path $env:LOCALAPPDATA "Programs\Tidverk"
$shortcutPath = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Tidverk.lnk"
$registryPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Tidverk"

Get-Process -Name "Tidverk" -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item -Path $shortcutPath -Force -ErrorAction SilentlyContinue
Remove-Item -Path $registryPath -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path $installDirectory -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "Tidverk was removed. Your local reports and database were kept."
