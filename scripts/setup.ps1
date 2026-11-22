$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$toolingRoot = Join-Path $projectRoot '.tooling'
New-Item -ItemType Directory -Force -Path $toolingRoot | Out-Null
$metadata = Invoke-RestMethod 'https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/10.0/releases.json'
$package = $metadata.releases[0].sdk.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -like '*.zip' } | Select-Object -First 1
$archive = Join-Path $toolingRoot 'dotnet-sdk.zip'
Invoke-WebRequest -Uri $package.url -OutFile $archive
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash -ne $package.hash) { throw 'La verificación SHA512 del SDK falló.' }
Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $toolingRoot 'dotnet') -Force
Write-Output 'SDK local preparado. Ejecuta scripts\build.ps1 -Test -Publish.'
