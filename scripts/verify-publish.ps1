param([string]$PublishDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'publish\win-x64'))
$ErrorActionPreference = 'Stop'
$requiredFiles = @(
    'Orbita.Desktop.exe',
    'Orbita.Desktop.dll',
    'Orbita.Desktop.deps.json',
    'Orbita.Desktop.runtimeconfig.json',
    'Orbita.Desktop.pri',
    'tessdata\eng.traineddata',
    'tessdata\spa.traineddata',
    'x64\tesseract55.dll',
    'x64\leptonica-1.85.0.dll'
)
foreach ($requiredFile in $requiredFiles) {
    $path = Join-Path $PublishDirectory $requiredFile
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Publicación incompleta: falta $requiredFile."
    }
    if ((Get-Item -LiteralPath $path).Length -eq 0) {
        throw "Publicación incompleta: $requiredFile está vacío."
    }
}
Write-Output 'Publicación verificada: ejecutable y recursos WinUI presentes.'
