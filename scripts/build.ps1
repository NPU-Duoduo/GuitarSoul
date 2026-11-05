param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release', [switch]$RunChecks)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) {
    $dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
    if (-not $dotnet) { throw '未找到 .NET SDK。请安装 .NET 10 SDK。' }
}
& $dotnet restore (Join-Path $projectRoot 'src\GuitarSoul.App\GuitarSoul.App.csproj') --configfile (Join-Path $projectRoot 'NuGet.Config') -m:1 -p:BuildInParallel=false
if ($LASTEXITCODE -ne 0) { throw '应用依赖恢复失败。' }
& $dotnet build (Join-Path $projectRoot 'src\GuitarSoul.App\GuitarSoul.App.csproj') -c $Configuration --no-restore -m:1 -p:BuildInParallel=false
if ($LASTEXITCODE -ne 0) { throw '应用构建失败。' }
if ($RunChecks) {
    & $dotnet build (Join-Path $projectRoot 'tests\GuitarSoul.Core.Tests\GuitarSoul.Core.Tests.csproj') -c $Configuration -m:1 -p:BuildInParallel=false
    if ($LASTEXITCODE -ne 0) { throw '核心检查构建失败。' }
    & $dotnet (Join-Path $projectRoot "tests\GuitarSoul.Core.Tests\bin\$Configuration\net10.0\GuitarSoul.Core.Tests.dll")
    if ($LASTEXITCODE -ne 0) { throw '核心检查未通过。' }
}
