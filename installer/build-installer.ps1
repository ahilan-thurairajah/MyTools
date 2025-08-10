param(
  [string]$Configuration = "Release"
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

# Try to find Inno Setup compiler
$possible = @(
  "$Env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
  "$Env:ProgramFiles\Inno Setup 6\ISCC.exe"
)
$ISCC = $possible | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $ISCC) {
  Write-Host "Inno Setup compiler not found. Please install from https://jrsoftware.org/isinfo.php" -ForegroundColor Yellow
  exit 1
}

# Compile installer
& "$ISCC" "$iss" /Qp

Write-Host "Installer built. See installer\\output folder." -ForegroundColor Green
