param(
  [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$installerProject = Join-Path $repoRoot "Installer\DontTouchMyMic.Installer.wixproj"
$appProject = Join-Path $repoRoot "DontTouchMyMic\DontTouchMyMic.csproj"
$distDir = Join-Path $repoRoot "artifacts\dist"
$portablePublishDir = Join-Path $repoRoot "artifacts\publish\win-x64-portable"

New-Item -ItemType Directory -Path $distDir -Force | Out-Null

function Assert-PathExists {
  param([string]$Path)
  if (-not (Test-Path -LiteralPath $Path)) {
    throw "Expected file was not found: $Path"
  }
}

Write-Host "==> Building MSI (x64)"
dotnet build $installerProject -c $Configuration -p:InstallerPlatform=x64 -p:AppRuntimeIdentifier=win-x64 -p:AppPublishPlatform=x64
if ($LASTEXITCODE -ne 0) { throw "x64 MSI build failed." }

$x64MsiSource = Join-Path $repoRoot "Installer\bin\x64\$Configuration\DontTouchMyMicInstaller-x64.msi"
Assert-PathExists -Path $x64MsiSource
$x64MsiOutput = Join-Path $distDir "DontTouchMyMic-x64.msi"
Copy-Item -LiteralPath $x64MsiSource -Destination $x64MsiOutput -Force

Write-Host "==> Building MSI (ARM64)"
dotnet build $installerProject -c $Configuration -p:InstallerPlatform=arm64 -p:AppRuntimeIdentifier=win-arm64 -p:AppPublishPlatform=ARM64
if ($LASTEXITCODE -ne 0) { throw "ARM64 MSI build failed." }

$arm64MsiSource = Join-Path $repoRoot "Installer\bin\arm64\$Configuration\DontTouchMyMicInstaller-arm64.msi"
if (-not (Test-Path -LiteralPath $arm64MsiSource)) {
  $arm64MsiSource = Join-Path $repoRoot "Installer\bin\ARM64\$Configuration\DontTouchMyMicInstaller-arm64.msi"
}
Assert-PathExists -Path $arm64MsiSource
$arm64MsiOutput = Join-Path $distDir "DontTouchMyMic-arm64.msi"
Copy-Item -LiteralPath $arm64MsiSource -Destination $arm64MsiOutput -Force

Write-Host "==> Building portable zip (x64)"
if (Test-Path -LiteralPath $portablePublishDir) {
  Remove-Item -LiteralPath $portablePublishDir -Recurse -Force
}
dotnet publish $appProject -c $Configuration -r win-x64 -p:Platform=x64 -p:PublishProfile= -p:WindowsPackageType=None -p:PublishDir="$portablePublishDir\"
if ($LASTEXITCODE -ne 0) { throw "Portable publish failed." }

$portableZip = Join-Path $distDir "DontTouchMyMic-portable-win-x64.zip"
if (Test-Path -LiteralPath $portableZip) {
  Remove-Item -LiteralPath $portableZip -Force
}
Compress-Archive -Path (Join-Path $portablePublishDir "*") -DestinationPath $portableZip -Force

Write-Host ""
Write-Host "Build artifacts:"
Write-Host "  $x64MsiOutput"
Write-Host "  $arm64MsiOutput"
Write-Host "  $portableZip"
