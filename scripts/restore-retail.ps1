<#
.SYNOPSIS
    Restaura la base de Retail POS desde una copia .bak (recuperacion ante desastres).

.DESCRIPTION
    Envoltorio delgado de "Retail.Cli restaurar": toda la logica (validacion de la copia, verificacion de
    credenciales, intercambio de bases) vive en la herramienta, no en este script.

    Procedimiento de recuperacion ante desastres:
      1. Cierre Retail POS en TODAS las terminales (las conexiones abiertas se cortan al restaurar).
      2. Ejecute este script indicando el archivo .bak. El .json de la copia (manifiesto) debe estar en la misma carpeta.
      3. Revise el resumen de la copia e ingrese el usuario y la contrasena de un Gerente que exista EN ESA COPIA:
         las credenciales se verifican dentro de la copia, no contra la base actual (que puede no existir).
      4. Escriba RESTAURAR para confirmar.
      5. Lea las advertencias finales (en especial la conciliacion de numeracion con ARCA) y reinicie Retail POS.

    La base reemplazada no se borra: queda renombrada como <base>_Anterior_<fecha>.
    Este script no maneja credenciales: las pide la propia herramienta.
    Devuelve el codigo de salida de Retail.Cli (0 = exito, 1 = fallo o cancelado, 2 = uso incorrecto).

.PARAMETER Archivo
    Ruta del archivo .bak a restaurar.

.PARAMETER RutaCli
    Ruta de Retail.Cli.exe. Por defecto se busca junto al script o en su carpeta padre (instalacion publicada);
    si no se encuentra, se ejecuta desde el codigo fuente con "dotnet run --project src/Retail.Cli".

.EXAMPLE
    .\restore-retail.ps1 -Archivo "D:\Respaldos\Retail_20261013_020000.bak"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Archivo,

    [string]$RutaCli
)

$ErrorActionPreference = "Stop"

if (-not $RutaCli) {
    $candidatos = @(
        (Join-Path $PSScriptRoot "Retail.Cli.exe"),
        (Join-Path (Split-Path $PSScriptRoot -Parent) "Retail.Cli.exe")
    )
    $RutaCli = $candidatos | Where-Object { Test-Path $_ } | Select-Object -First 1
}

# La herramienta escribe avisos en stderr (por ejemplo, "Cancelando..."). Con "Stop", PowerShell 5.1 los convierte en
# errores terminantes si el host captura stderr: el resultado lo decide el código de salida, no stderr.
$ErrorActionPreference = "Continue"

if ($RutaCli) {
    & $RutaCli restaurar $Archivo
}
else {
    $proyecto = Join-Path (Split-Path $PSScriptRoot -Parent) "src\Retail.Cli"
    if (-not (Test-Path $proyecto)) {
        Write-Error "No se encontro Retail.Cli.exe ni el proyecto en: $proyecto"
        exit 1
    }

    & dotnet run --project $proyecto -c Release -- restaurar $Archivo
}

exit $LASTEXITCODE
