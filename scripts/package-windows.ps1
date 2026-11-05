$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $root '.tools/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) {
    $dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
    if (-not $dotnet) { throw 'Install .NET 10 SDK before packaging.' }
}
[xml]$project = Get-Content -LiteralPath (Join-Path $root 'src/GuitarSoul.App/GuitarSoul.App.csproj')
$version = [string]$project.Project.PropertyGroup.Version
$name = "GuitarSoul-$version-win-x64"
$package = Join-Path $root "dist/$name"
$zip = Join-Path $root "dist/$name.zip"
$evidence = Join-Path $root "artifacts/package-$version"
if ((Test-Path -LiteralPath $package) -or (Test-Path -LiteralPath $zip)) { throw 'Existing release output retained. Choose a new version before packaging again.' }
New-Item -ItemType Directory -Path $package,$evidence -Force | Out-Null
& $dotnet publish (Join-Path $root 'src/GuitarSoul.App/GuitarSoul.App.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugType=None -m:1 -p:BuildInParallel=false --configfile (Join-Path $root 'NuGet.Config') -o $package
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
$licenses = Join-Path $package 'licenses'
New-Item -ItemType Directory -Path $licenses -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $root '.tools/packages') -Directory -Filter 'naudio*' | ForEach-Object {
    $license = Join-Path $_.FullName '2.2.1/license.txt'
    if (Test-Path -LiteralPath $license) { Copy-Item -LiteralPath $license -Destination (Join-Path $licenses ($_.Name + '-LICENSE.txt')) }
}
foreach ($notice in @('LICENSE.TXT','THIRD-PARTY-NOTICES.TXT')) {
    $path = Join-Path $root ".tools/packages/microsoft.netcore.app.runtime.win-x64/10.0.12/$notice"
    if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination (Join-Path $licenses ('dotnet-' + $notice)) }
}
Copy-Item -LiteralPath (Join-Path $root 'USER-GUIDE.md') -Destination (Join-Path $package 'USER-GUIDE.md')
@"
GuitarSoul $version - Windows x64 Preview
Developer: Duoduo_127

Extract the entire ZIP into a writable folder, then double-click GuitarSoul.exe.
Keep all DLLs and the profiles folder beside the executable. No .NET installation is required.
No developer SDK, Python, training data, or AI model download is required.
Optional local language models are not included; rule-based responses remain available.

Select an input device and click Start Listening when ready. Startup does not begin capture.
Closing the window minimizes to the tray. Use the tray Exit command to quit.
Settings and symbolic drafts use the existing local Guitar Soul data directory.
User data is outside this folder; deleting the program folder does not remove that data.
Do not overwrite an older running copy. Extract this version into a separate folder.

Chord records are experimental drafts and can miss or misidentify chords.
This is a preview, with local startup validation; independent PC and live-guitar acceptance remain pending.
See USER-GUIDE.md for usage and licenses/ for third-party notices.
"@ | Set-Content -LiteralPath (Join-Path $package 'README.txt') -Encoding utf8
$exe = Join-Path $package 'GuitarSoul.exe'
$info = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
if ($info.ProductName -ne 'GuitarSoul' -or $info.CompanyName -ne 'Duoduo_127') { throw 'Executable branding mismatch.' }
foreach ($file in @('coreclr.dll','hostfxr.dll','PresentationFramework.dll','profiles/devices.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $package $file))) { throw "Missing runtime asset: $file" }
}
foreach ($check in @(@('--ui-check-report','ui.json'),@('--render-tab','3','settings.png'))) {
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $exe; $start.WorkingDirectory = $package; $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $target = Join-Path $evidence $check[-1]
    $start.Arguments = ($check[0..($check.Count-2)] -join ' ') + ' "' + $target + '"'
    $start.EnvironmentVariables['DOTNET_ROOT'] = Join-Path $evidence 'absent-runtime'
    $start.EnvironmentVariables['DOTNET_ROOT_X64'] = Join-Path $evidence 'absent-runtime'
    $start.EnvironmentVariables['PATH'] = "$env:SystemRoot\System32;$env:SystemRoot"
    $start.EnvironmentVariables['DOTNET_HOST_TRACE'] = '1'
    $start.EnvironmentVariables['DOTNET_HOST_TRACEFILE'] = Join-Path $evidence ($check[-1] + '.host-trace.txt')
    $process = [Diagnostics.Process]::Start($start)
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Packaged application check timed out.' }
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $target)) { throw 'Packaged application check failed.' }
}
$ui = Get-Content -LiteralPath (Join-Path $evidence 'ui.json') -Raw | ConvertFrom-Json
Get-ChildItem -LiteralPath $package -File -Recurse | ForEach-Object {
    [ordered]@{path=$_.FullName.Substring($package.Length+1);sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
} | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $package 'FILES-SHA256.json') -Encoding utf8
Compress-Archive -LiteralPath $package -DestinationPath $zip -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
($hash + '  ' + $name + '.zip') | Set-Content -LiteralPath ($zip + '.sha256') -Encoding ascii
[ordered]@{version=$version;product=$info.ProductName;developer=$info.CompanyName;uiChecks=$ui.checks.Count;selfContained=$true;runtime='win-x64';zip=$zip;sha256=$hash;cleanExternalMachineTested=$false;captureOpened=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $evidence 'result.json') -Encoding utf8
Write-Output $zip
