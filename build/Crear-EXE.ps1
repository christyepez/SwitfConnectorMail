param([string]$Runtime = 'win-x64')
$ErrorActionPreference='Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\SwiftHourlyWorker\SwiftHourlyWorker.csproj'
$dest = Join-Path $root "publicado\$Runtime"
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Se requiere SDK .NET 10. Instálalo desde https://dotnet.microsoft.com/download/dotnet/10.0 y vuelve a ejecutar este script.'
}
$version = & dotnet --list-sdks
if (-not ($version | Where-Object { $_ -match '^10\.' })) { throw 'No está instalado el SDK .NET 10.' }
& dotnet restore $project -r $Runtime
if ($LASTEXITCODE -ne 0) { throw 'Falló restauración de paquetes NuGet' }
& dotnet publish $project -c Release -r $Runtime --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false --no-restore -o $dest
if ($LASTEXITCODE -ne 0) { throw 'Falló la publicación' }
$exe = Join-Path $dest 'SwiftHourlyWorker.exe'
if (-not (Test-Path $exe)) { throw "No se encontró el EXE: $exe" }
Write-Host "EXE generado: $exe" -ForegroundColor Green
Write-Host 'IMPORTANTE: requiere Outlook clásico instalado y sesión de Windows iniciada.'
