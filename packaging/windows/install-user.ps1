$ErrorActionPreference = "Stop"

$packageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceDirectory = Join-Path $packageRoot "app"
$sourceExecutable = Join-Path $sourceDirectory "Tidverk.exe"
$uninstallerSource = Join-Path $packageRoot "uninstall-user.ps1"
$versionFile = Join-Path $packageRoot "version.txt"

if (-not (Test-Path $sourceExecutable)) {
    throw "Tidverk.exe was not found. Run this installer from the extracted Tidverk package."
}

$installDirectory = Join-Path $env:LOCALAPPDATA "Programs\Tidverk"
$startMenuDirectory = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$shortcutPath = Join-Path $startMenuDirectory "Tidverk.lnk"
$registryPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Tidverk"
$version = if (Test-Path $versionFile) { (Get-Content $versionFile -Raw).Trim() } else { "0.1.0" }

Get-Process -Name "Tidverk" -ErrorAction SilentlyContinue | Stop-Process -Force
New-Item -ItemType Directory -Force -Path $installDirectory, $startMenuDirectory | Out-Null
Copy-Item -Path (Join-Path $sourceDirectory "*") -Destination $installDirectory -Recurse -Force
Copy-Item -Path $uninstallerSource -Destination (Join-Path $installDirectory "uninstall.ps1") -Force

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $installDirectory "Tidverk.exe"
$shortcut.WorkingDirectory = $installDirectory
$shortcut.IconLocation = "$($shortcut.TargetPath),0"
$shortcut.Save()

$uninstallCommand = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$installDirectory\uninstall.ps1`""
$estimatedSize = [int][Math]::Ceiling(((Get-ChildItem $installDirectory -File -Recurse | Measure-Object Length -Sum).Sum) / 1KB)
New-Item -Path $registryPath -Force | Out-Null
New-ItemProperty -Path $registryPath -Name "DisplayName" -Value "Tidverk" -PropertyType String -Force | Out-Null
New-ItemProperty -Path $registryPath -Name "DisplayVersion" -Value $version -PropertyType String -Force | Out-Null
New-ItemProperty -Path $registryPath -Name "Publisher" -Value "agneswd" -PropertyType String -Force | Out-Null
New-ItemProperty -Path $registryPath -Name "DisplayIcon" -Value (Join-Path $installDirectory "Tidverk.exe") -PropertyType String -Force | Out-Null
New-ItemProperty -Path $registryPath -Name "InstallLocation" -Value $installDirectory -PropertyType String -Force | Out-Null
New-ItemProperty -Path $registryPath -Name "UninstallString" -Value $uninstallCommand -PropertyType String -Force | Out-Null
New-ItemProperty -Path $registryPath -Name "EstimatedSize" -Value $estimatedSize -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $registryPath -Name "NoModify" -Value 1 -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $registryPath -Name "NoRepair" -Value 1 -PropertyType DWord -Force | Out-Null

Write-Host "Tidverk $version installed. Open it from the Start menu."
