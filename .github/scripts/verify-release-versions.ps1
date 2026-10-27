<#
.SYNOPSIS
  Checks that every exe in a staged release folder reports the release version.

.DESCRIPTION
  A mismatch means MinVer did not see the tag (the .NET apps would carry a pre-release version such
  as 1.7.2-alpha.0.354) or the Tauri version was not stamped (it would still say 0.1.0). Either way
  the zip must not be published.

  The version is the tag: 2.1.0 or a pre-release such as 2.1.0-beta.1.

  .NET apps (MinVer): ProductVersion is "<version>" optionally followed by "+<commit>", and
  FileVersion is the numeric "X.Y.Z.0" (a Windows file version cannot carry a pre-release label).
  Tauri app: Tauri writes the version from tauri.conf.json into both fields as "<version>".

.EXAMPLE
  ./.github/scripts/verify-release-versions.ps1 -Version 2.1.0 -Root artifacts/release/cde-2.1.0-win-x64
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)] [string] $Version,
  [Parameter(Mandatory)] [string] $Root
)

$ErrorActionPreference = 'Stop'

# Path under Root, then which stamping scheme the exe uses.
$checks = @(
  @{ Path = 'cde/cde.exe';             Kind = 'MinVer' }
  @{ Path = 'cdewin/cdewin.exe';       Kind = 'MinVer' }
  @{ Path = 'cde-tauri/cdeApi.exe';    Kind = 'MinVer' }
  @{ Path = 'cde-tauri/cde-tauri.exe'; Kind = 'Tauri' }
)

# 2.1.0-beta.1 -> 2.1.0
$numeric = ($Version -split '-')[0]

$failed = $false
foreach ($check in $checks) {
  $file = Join-Path $Root $check.Path
  if (-not (Test-Path -LiteralPath $file)) {
    Write-Host "FAIL  $($check.Path): file not found"
    $failed = $true
    continue
  }

  $info = (Get-Item -LiteralPath $file).VersionInfo
  $product = [string] $info.ProductVersion
  $fileVersion = [string] $info.FileVersion

  if ($check.Kind -eq 'MinVer') {
    # A pre-release tag (2.1.0-beta.1) is in ProductVersion in full; FileVersion is numeric only.
    $okProduct = ($product -split '\+')[0] -eq $Version
    $okFile = $fileVersion -eq "$numeric.0"
  }
  else {
    $okProduct = $product -eq $Version
    $okFile = $fileVersion -eq $Version
  }

  $status = if ($okProduct -and $okFile) { 'ok  ' } else { 'FAIL' }
  Write-Host ("{0}  {1}: ProductVersion={2} FileVersion={3} (expected {4})" -f
    $status, $check.Path, $product, $fileVersion, $Version)
  if (-not ($okProduct -and $okFile)) { $failed = $true }
}

if ($failed) { throw "Version check failed: not every exe reports $Version." }
Write-Host "All exes report $Version."
