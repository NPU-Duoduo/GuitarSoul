$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
[xml]$project = Get-Content -LiteralPath (Join-Path $root 'src/GuitarSoul.App/GuitarSoul.App.csproj')
$version = [string]$project.Project.PropertyGroup.Version
$payload = Join-Path $root "dist/GuitarSoul-$version-win-x64"
$compiler = Join-Path $root '.tools/setup-builder/compiler/ISCC.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Install the official Inno Setup compiler under .tools/setup-builder/compiler first.' }
if (-not (Test-Path -LiteralPath (Join-Path $payload 'GuitarSoul.exe'))) { throw 'Build and verify the Windows payload first using scripts/package-windows.ps1.' }
$manifest = Get-Content -LiteralPath (Join-Path $payload 'FILES-SHA256.json') -Raw | ConvertFrom-Json
foreach ($file in $manifest) {
    if ((Get-FileHash -LiteralPath (Join-Path $payload $file.path) -Algorithm SHA256).Hash -ne $file.sha256) { throw "Payload hash mismatch: $($file.path)" }
}
& $compiler "/DAppVersion=$version" (Join-Path $root 'installer/GuitarSoul.iss')
if ($LASTEXITCODE -ne 0) { throw 'Setup compilation failed.' }
$setup = Join-Path $root "dist/GuitarSoul-$version-Setup-x64.exe"
((Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash + '  ' + [IO.Path]::GetFileName($setup)) | Set-Content -LiteralPath ($setup + '.sha256') -Encoding ascii
Write-Output $setup
