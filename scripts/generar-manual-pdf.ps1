# ==============================================================================
# Retail POS - Generador Automático del Manual de Usuario en PDF
# ==============================================================================
# Convierte docs/MANUAL_DE_USUARIO.md a HTML estilizado con la identidad de marca
# y utiliza el motor headless de Microsoft Edge para exportar a PDF en:
# src/Retail.App/Assets/Manual/Manual_Usuario_Retail_POS.pdf
#
# Requisitos: Node.js (conversor scripts/convertir-md-a-html.js) y Microsoft Edge.
# Solo hacen falta para regenerar el manual: el build y el CI usan el PDF versionado.
# El HTML intermedio (docs/MANUAL_DE_USUARIO.html) no se versiona.
# Ejecutar cada vez que cambie docs/MANUAL_DE_USUARIO.md y commitear el PDF resultante.
# ==============================================================================

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " RETAIL POS - GENERACIÓN DE MANUAL DE USUARIO EN PDF" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

$root = Resolve-Path "$PSScriptRoot\.."
$mdPath = Join-Path $root "docs\MANUAL_DE_USUARIO.md"
$htmlPath = Join-Path $root "docs\MANUAL_DE_USUARIO.html"
$destDir = Join-Path $root "src\Retail.App\Assets\Manual"
$destPdf = Join-Path $destDir "Manual_Usuario_Retail_POS.pdf"

if (-not (Test-Path $mdPath)) {
    Write-Error "No se encontró el archivo origen: $mdPath"
    exit 1
}

# 1. Convertir Markdown a HTML con estilo comercial
Write-Host "1. Transformando Markdown a HTML con estilos de marca..." -ForegroundColor Yellow
$converterScript = Join-Path $root "scripts\convertir-md-a-html.js"

if (Get-Command node -ErrorAction SilentlyContinue) {
    node $converterScript
} else {
    Write-Error "Se requiere Node.js para ejecutar el conversor de Markdown a HTML."
    exit 1
}

if (-not (Test-Path $htmlPath)) {
    Write-Error "No se pudo generar el archivo HTML intermedio: $htmlPath"
    exit 1
}

# 2. Localizar Microsoft Edge en el sistema operativo
Write-Host "2. Localizando Microsoft Edge para renderizado headless..." -ForegroundColor Yellow
$edgeCandidates = @(
    "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    "C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
    "${env:ProgramFiles}\Microsoft\Edge\Application\msedge.exe"
)

$edgePath = $edgeCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $edgePath) {
    Write-Warning "No se encontró Microsoft Edge en las rutas estándar. El HTML está disponible en: $htmlPath"
    Write-Host "Puede abrir el archivo HTML en cualquier navegador y seleccionar 'Imprimir > Guardar como PDF'." -ForegroundColor Cyan
    exit 0
}

# 3. Crear directorio de destino si no existe
if (-not (Test-Path $destDir)) {
    New-Item -ItemType Directory -Force -Path $destDir | Out-Null
}

# 4. Generar PDF mediante Edge headless
Write-Host "3. Exportando a PDF mediante Microsoft Edge..." -ForegroundColor Yellow
$processArgs = @(
    "--headless",
    "--disable-gpu",
    "--run-all-compositor-stages-before-draw",
    "--no-pdf-header-footer",
    "--print-to-pdf=`"$destPdf`"",
    "`"$htmlPath`""
)

Start-Process -FilePath $edgePath -ArgumentList $processArgs -Wait -NoNewWindow

if (Test-Path $destPdf) {
    $info = Get-Item $destPdf
    $tamKb = [math]::Round($info.Length / 1KB, 2)
    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Green
    Write-Host " MANUAL PDF GENERADO CON ÉXITO: $destPdf ($tamKb KB)" -ForegroundColor Green
    Write-Host "============================================================" -ForegroundColor Green
} else {
    Write-Error "Fallo la generación del archivo PDF en destino."
    exit 1
}

