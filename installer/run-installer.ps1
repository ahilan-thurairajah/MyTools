param(
  [string]$Configuration = "Release",
  [switch]$Signed
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent
$builder = Join-Path $repo 'installer\build-installer.ps1'

function Find-ISCC {
  $candidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:LocalAppData}\Programs\Inno Setup 6\ISCC.exe"
  )
  foreach ($p in $candidates) { if ($p -and (Test-Path $p)) { return $p } }
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

$ISCC = if ($env:INNO_ISCC) { $env:INNO_ISCC } else { Find-ISCC }
if (-not $ISCC) { Write-Error "Inno Setup compiler (ISCC.exe) not found. Install Inno Setup or set INNO_ISCC to the full path." }
Write-Host "Using Inno Setup compiler: $ISCC" -ForegroundColor Cyan

$params = @{ Configuration = $Configuration; ISCCPath = $ISCC }
if ($Signed) {
  if ($env:SIGN_PFX -and $env:SIGN_PWD) { $params['PfxPath'] = $env:SIGN_PFX; $params['PfxPassword'] = $env:SIGN_PWD }
  else { Write-Warning "-Signed specified but SIGN_PFX/SIGN_PWD not set; proceeding unsigned." }
}

& $builder @params

$outputDir = Join-Path $repo 'installer\output'
$installerExe = Join-Path $outputDir 'MyTools-MyCalculator-Setup.exe'
if (Test-Path $installerExe) { Write-Host "Launching installer: $installerExe" -ForegroundColor Green; Start-Process -FilePath $installerExe }
else { Write-Warning "Installer not found at $installerExe" }
