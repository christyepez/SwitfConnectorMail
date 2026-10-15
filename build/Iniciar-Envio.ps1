param([string]$Runtime='win-x64')
$ErrorActionPreference='Stop'
$root = Split-Path -Parent $PSScriptRoot
$folder = Join-Path $root "publicado\$Runtime"
$exe = Join-Path $folder 'SwiftHourlyWorker.exe'
if (-not (Test-Path $exe)) { throw 'Primero ejecuta build\Crear-EXE.ps1' }
$env:Report__DryRun='false'
$env:Report__RunOnStartup='false'
$env:Report__SenderUserId='christianye@iadb.org'
$env:Report__Recipients__0='christianye@iadb.org'
$env:Report__StateFile='state/checkpoint-live.json'
Push-Location $folder
try { & $exe } finally { Pop-Location }
