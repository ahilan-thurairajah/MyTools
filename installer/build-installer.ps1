param(
  [string]$Configuration = "Release",
  [string]$PfxPath,
  [string]$PfxPassword,
  [string]$TimestampUrl = "http://timestamp.digicert.com",
  [string]$ISCCPath
)

$ErrorActionPreference = 'Stop'

# Paths
$repo = Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent
$proj = Join-Path $repo 'src\MyTools.MyCalculator\MyTools.MyCalculator.csproj'
$publishDir = Join-Path $repo 'installer\app'
$iss = Join-Path $repo 'installer\MyCalculator.iss'

# Clean publish folder
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
New-Item -ItemType Directory -Path $publishDir | Out-Null

# Publish self-contained x64 build
& dotnet publish $proj -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $publishDir

# Optional code signing for the app executable
function Find-SignTool {
  $candidates = @(
    "${env:ProgramFiles(x86)}\Windows Kits\10\bin\x64\signtool.exe",
    "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe",
    "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.0.22000.0\x64\signtool.exe",
    "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe",
    "${env:ProgramFiles(x86)}\Windows Kits\8.1\bin\x64\signtool.exe"
  )
  foreach ($p in $candidates) { if (Test-Path $p) { return $p } }
  $found = Get-ChildItem "$Env:ProgramFiles(x86)\Windows Kits" -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
  if ($found) { return $found.FullName }
  return $null
}

function Sign-FileIfPossible([string]$file) {
  if (-not (Test-Path $file)) { return }
  if ([string]::IsNullOrWhiteSpace($PfxPath) -or [string]::IsNullOrWhiteSpace($PfxPassword)) { return }
  $signtool = Find-SignTool
  if (-not $signtool) { Write-Host "signtool.exe not found; skipping signing for $file" -ForegroundColor Yellow; return }
  & $signtool sign /fd sha256 /f "$PfxPath" /p "$PfxPassword" /tr "$TimestampUrl" /td sha256 "$file"
}

$appExe = Join-Path $publishDir 'MyTools.MyCalculator.exe'
Sign-FileIfPossible $appExe

function Find-ISCC {
  $candidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:LocalAppData}\Programs\Inno Setup 6\ISCC.exe"
  )
  foreach ($p in $candidates) { if (Test-Path $p) { return $p } }
  $regPaths = @(
    'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\ISCC.exe',
    'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\ISCC.exe'
  )
  foreach ($rp in $regPaths) {
    try {
      $reg = Get-ItemProperty -Path $rp -ErrorAction SilentlyContinue
      if ($reg -and $reg.'(default)') { $val = $reg.'(default)'; if (Test-Path $val) { return $val } }
      if ($reg -and $reg.Path) { $path = Join-Path $reg.Path 'ISCC.exe'; if (Test-Path $path) { return $path } }
    } catch {}
  }
  $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
  if ($cmd) { return $cmd.Source }
  $roots = @("${env:ProgramFiles}", "${env:ProgramFiles(x86)}") | Where-Object { $_ -and (Test-Path $_) }
  foreach ($r in $roots) {
    $found = Get-ChildItem -Path $r -Recurse -Filter ISCC.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
    if ($found) { return $found }
  }
  return $null
}

# Try to find or use provided Inno Setup compiler
$ISCC = if ($ISCCPath) { $ISCCPath } elseif ($env:INNO_ISCC) { $env:INNO_ISCC } else { Find-ISCC }
if (-not $ISCC) {
  Write-Host "Inno Setup compiler not found. Please install from https://jrsoftware.org/isinfo.php (or set INNO_ISCC/ISCCPath)" -ForegroundColor Yellow
  exit 1
}

# Compile installer
& "$ISCC" "$iss" /Qp

# Sign the installer executable if possible
$outputDir = Join-Path $repo 'installer\output'
$installerExe = Join-Path $outputDir 'MyTools-MyCalculator-Setup.exe'
Sign-FileIfPossible $installerExe

Write-Host "Installer built. See installer\\output folder." -ForegroundColor Green
