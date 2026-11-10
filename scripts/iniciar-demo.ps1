<#
.SYNOPSIS
    Prepara y abre Retail POS contra la base de demostracion RetailDemo, sin tocar la base de trabajo.

.DESCRIPTION
    Define la cadena de conexion y la carpeta de respaldos como variables de entorno SOLO de este proceso y de los que
    lanza: abrir la aplicacion de la forma habitual sigue usando la base de trabajo. Se ejecuta desde el repositorio.

    Con -Preparar crea la base: migrar, inicializar (pide los datos del Gerente), datos-demo, cargar-diccionarios e
    historial-demo. El orden importa: datos-demo antes que los diccionarios conserva los nombres "Bic" y no "BIC".

    Las copias van a una subcarpeta propia porque el nombre de un .bak no indica de que base es: mezcladas, una copia de
    demostracion se podria restaurar por error sobre la base de trabajo.

    Para rehacer la demostracion desde cero, elimine la base RetailDemo y vuelva a ejecutar con -Preparar. Para volver al
    estado recien sembrado despues de un ensayo, alcanza con restaurar una copia manual desde la aplicacion.

.PARAMETER Preparar
    Crea y puebla la base antes de abrir la aplicacion. Falla si la base ya tiene un Gerente o ventas.

.PARAMETER SoloPreparar
    Crea y puebla la base sin abrir la aplicacion.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\iniciar-demo.ps1 -Preparar
#>
[CmdletBinding()]
param(
    [switch]$Preparar,
    [switch]$SoloPreparar,
    [ValidatePattern('^[A-Za-z0-9_.\\-]+$')]
    [string]$Instancia = '.\SQLEXPRESS',
    [ValidatePattern('^[A-Za-z0-9_]+$')]
    [string]$BaseDeDatos = 'RetailDemo',
    [string]$DirectorioRespaldos = 'C:\RetailData\Backups\Demo'
)

$ErrorActionPreference = 'Stop'

if ($BaseDeDatos -ieq 'RetailDb')
{
    throw 'RetailDb es la base de trabajo: la demostracion necesita una base propia.'
}

$raiz = Split-Path -Parent $PSScriptRoot
$proyectoCli = Join-Path $raiz 'src\Retail.Cli'
$proyectoApp = Join-Path $raiz 'src\Retail.App'

$env:ConnectionStrings__DefaultConnection = "Server=$Instancia;Database=$BaseDeDatos;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
$env:BackupSettings__Directorio = $DirectorioRespaldos

function Invoke-RetailCli
{
    param([Parameter(Mandatory = $true)][string]$Comando)

    Write-Host "--- Retail.Cli $Comando"
    & dotnet run --project $proyectoCli -- $Comando
    if ($LASTEXITCODE -ne 0)
    {
        throw "Retail.Cli $Comando termino con codigo $LASTEXITCODE."
    }
}

if ($Preparar -or $SoloPreparar)
{
    New-Item -ItemType Directory -Force -Path $DirectorioRespaldos | Out-Null

    foreach ($comando in 'migrar', 'inicializar', 'datos-demo', 'cargar-diccionarios', 'historial-demo')
    {
        Invoke-RetailCli -Comando $comando
    }
}

if (-not $SoloPreparar)
{
    & dotnet run --project $proyectoApp
}
