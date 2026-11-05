$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) {
    $dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
    if (-not $dotnet) { throw '未找到 .NET SDK。请安装 .NET 10 SDK。' }
}
$projectFile = Join-Path $projectRoot 'src\GuitarSoul.App\GuitarSoul.App.csproj'
[xml]$appProject = Get-Content -LiteralPath $projectFile
$appVersion = [string]$appProject.Project.PropertyGroup.Version
$app = Join-Path $projectRoot "src\GuitarSoul.App\bin\Release\$appVersion\net10.0-windows\GuitarSoul.dll"
if (-not (Test-Path -LiteralPath $app)) { & (Join-Path $PSScriptRoot 'build.ps1') }
else {
    # Incremental build ensures the normal entry point matches the local source.
    & $dotnet build $projectFile -c Release --no-restore -m:1 -p:BuildInParallel=false
    if ($LASTEXITCODE -ne 0) { throw '未能更新当前构建。请保留当前会话，查看构建错误后再启动。' }
}
& $dotnet $app
if ($LASTEXITCODE -ne 0) { throw 'Guitar Soul 启动失败。' }
