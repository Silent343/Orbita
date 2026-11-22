param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$Publish,
    [switch]$Test
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$localSdk = Join-Path $projectRoot '.tooling\dotnet\dotnet.exe'
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
if (Test-Path -LiteralPath $localSdk) { $dotnetExe = (Resolve-Path -LiteralPath $localSdk).Path }
elseif ($dotnetCommand) { $dotnetExe = $dotnetCommand.Source }
else { throw 'Instala el SDK de .NET 10 o ejecuta scripts\setup.ps1.' }
$taskWorkspace = Join-Path $projectRoot 'artifacts'
$env:DOTNET_CLI_HOME = Join-Path $taskWorkspace 'dotnet-home'
$env:NUGET_PACKAGES = Join-Path $taskWorkspace 'nuget-packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_ROOT = Split-Path -Parent $dotnetExe
$env:ORBITA_TEST_ROOT = Join-Path $taskWorkspace 'test-files'
$solution = Join-Path $projectRoot 'Orbita.slnx'
& $dotnetExe restore $solution -p:Platform=x64 --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw 'La restauración de paquetes falló.' }
& $dotnetExe build $solution -c $Configuration -p:Platform=x64 -p:UseSharedCompilation=false -m:1 --no-restore --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw 'La compilación falló.' }
if ($Test) {
    & $dotnetExe test (Join-Path $projectRoot 'tests\Orbita.Tests\Orbita.Tests.csproj') -c $Configuration -p:Platform=x64 --no-build --no-restore --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Las pruebas fallaron.' }
}
if ($Publish) {
    $destination = Join-Path $projectRoot 'publish\win-x64'
    & $dotnetExe publish (Join-Path $projectRoot 'src\Orbita.Desktop\Orbita.Desktop.csproj') -c $Configuration -p:Platform=x64 -p:UseSharedCompilation=false -m:1 --no-restore -o $destination --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'La publicación falló.' }
    & (Join-Path $PSScriptRoot 'verify-publish.ps1') -PublishDirectory $destination
    Write-Output (Join-Path $destination 'Orbita.Desktop.exe')
}
