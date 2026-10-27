<#
.SYNOPSIS
  Stages the built apps into the release folder layout and zips them.

.DESCRIPTION
  Layout (the folder name is cde-<version>-win-x64):

    cde-<version>-win-x64/
      cde/        cde.exe + appsettings.json
      cdewin/     cdewin.exe + appsettings.json
      cde-tauri/  cde-tauri.exe + cdeApi.exe (the sidecar)

  cde and cdewin come from the Fallout build's artifacts folder (`./build.ps1 PublishRelease
  --self-contained true`). cde-tauri.exe comes from `pnpm tauri build --no-bundle`; that build also
  copies the sidecar next to it as cdeApi.exe (tauri-build strips the target triple from
  binaries/cdeApi-<triple>.exe), which is the name the app spawns, so it is copied from there as is.

  Debug symbols (*.pdb) are left out of the zip.

  -Phase Stage  copies the files and stops, so the versions can be checked before anything is zipped.
  -Phase Zip    zips the staged folder and writes the .sha256 file.
  -Phase All    does both (default).

.EXAMPLE
  ./.github/scripts/stage-release.ps1 -Version 2.1.0
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)] [string] $Version,
  [ValidateSet('Stage', 'Zip', 'All')] [string] $Phase = 'All',
  [string] $ArtifactsDir = 'artifacts',
  [string] $TauriReleaseDir = 'src/cde-tauri/src-tauri/target/release',
  [string] $OutDir = 'artifacts/release'
)

$ErrorActionPreference = 'Stop'

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
  throw "Version '$Version' is not MAJOR.MINOR.PATCH (no 'v' prefix)."
}

$name = "cde-$Version-win-x64"
$stage = Join-Path $OutDir $name
$zip = Join-Path $OutDir "$name.zip"
$sha = "$zip.sha256"

function Copy-Required([string] $From, [string] $ToDir) {
  if (-not (Test-Path -LiteralPath $From -PathType Leaf)) { throw "Missing build output: $From" }
  New-Item -ItemType Directory -Force $ToDir | Out-Null
  Copy-Item -LiteralPath $From -Destination $ToDir
}

if ($Phase -in 'Stage', 'All') {
  if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
  New-Item -ItemType Directory -Force $stage | Out-Null

  foreach ($app in 'cde', 'cdewin') {
    $src = Join-Path $ArtifactsDir $app
    $exe = Join-Path $src "$app.exe"
    if (-not (Test-Path -LiteralPath $exe)) { throw "Missing build output: $exe" }
    $dest = Join-Path $stage $app
    New-Item -ItemType Directory -Force $dest | Out-Null
    Get-ChildItem -LiteralPath $src -File | Where-Object { $_.Extension -ne '.pdb' } |
      Copy-Item -Destination $dest
  }

  $tauriDest = Join-Path $stage 'cde-tauri'
  Copy-Required (Join-Path $TauriReleaseDir 'cde-tauri.exe') $tauriDest
  Copy-Required (Join-Path $TauriReleaseDir 'cdeApi.exe') $tauriDest

  Write-Host "Staged $stage"
}

if ($Phase -in 'Zip', 'All') {
  if (-not (Test-Path $stage)) { throw "Nothing staged at $stage - run with -Phase Stage first." }
  if (Test-Path $zip) { Remove-Item -Force $zip }

  Add-Type -AssemblyName System.IO.Compression.FileSystem
  [System.IO.Compression.ZipFile]::CreateFromDirectory(
    (Resolve-Path $stage).Path, (Join-Path (Resolve-Path $OutDir).Path "$name.zip"),
    [System.IO.Compression.CompressionLevel]::Optimal, $true)

  $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
  # `sha256sum -c` format: "<hash>  <file name>".
  [IO.File]::WriteAllText((Join-Path (Resolve-Path $OutDir).Path "$name.zip.sha256"), "$hash  $name.zip`n")

  Write-Host "Zipped $zip"
  Write-Host "SHA-256 $hash"

  $archive = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path $zip).Path)
  try {
    $archive.Entries | Where-Object { $_.Length -gt 0 } |
      ForEach-Object { '{0,14:N0}  {1}' -f $_.Length, $_.FullName } | Write-Host
  }
  finally { $archive.Dispose() }
}
