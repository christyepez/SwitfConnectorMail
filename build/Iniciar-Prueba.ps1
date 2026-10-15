param([string]$Runtime='win-x64')
$ErrorActionPreference='Stop'
$root = Split-Path -Parent $PSScriptRoot
$folder = Join-Path $root "publicado\$Runtime"
$exe = Join-Path $folder 'SwiftHourlyWorker.exe'
if (-not (Test-Path $exe)) { throw "Ejecuta antes build\Crear-EXE.ps1: $exe" }
$env:Report__DryRun = 'true'
$env:Report__RunOnStartup = 'true'
$env:Report__SenderUserId = 'christianye@iadb.org'
$env:Report__Recipients__0 = 'christianye@iadb.org'
$env:Report__StateFile = 'state/checkpoint-test.json'
Push-Location $folder
try { & $exe } finally { Pop-Location }
