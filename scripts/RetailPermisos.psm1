<#
.SYNOPSIS
    Funciones de permisos y de configuracion de registrar-tarea-backup.ps1, separadas del script para poder probarlas.

.DESCRIPTION
    Las usa registrar-tarea-backup.ps1 (Fase 7) y las ejercita la prueba de integracion
    PermisosCarpetaRespaldosIntegracionTests, que las importa en un powershell.exe real y lee despues la lista de acceso
    (ACL) resultante. Las cuentas conocidas se nombran por SID (no por nombre) para que funcionen igual en Windows en
    espanol que en ingles.
#>

Set-StrictMode -Version Latest
# Un error en cualquier funcion debe detener el script que las importa, no seguir con valores vacios.
$ErrorActionPreference = "Stop"

$script:SidAdministradores = "S-1-5-32-544"
$script:SidSistema = "S-1-5-18"
# Grupos amplios: "Todos", "Usuarios autenticados" y "Usuarios". Ninguno debe poder escribir donde corre SYSTEM ni leer respaldos.
$script:SidsAmplios = @("S-1-1-0", "S-1-5-11", "S-1-5-32-545")

# Derechos que permiten cambiar, reemplazar o borrar lo que hay en una carpeta (se ignoran los de solo lectura y ejecucion).
$script:DerechosDeEscritura =
    [System.Security.AccessControl.FileSystemRights]::WriteData -bor
    [System.Security.AccessControl.FileSystemRights]::AppendData -bor
    [System.Security.AccessControl.FileSystemRights]::WriteExtendedAttributes -bor
    [System.Security.AccessControl.FileSystemRights]::WriteAttributes -bor
    [System.Security.AccessControl.FileSystemRights]::DeleteSubdirectoriesAndFiles -bor
    [System.Security.AccessControl.FileSystemRights]::Delete -bor
    [System.Security.AccessControl.FileSystemRights]::ChangePermissions -bor
    [System.Security.AccessControl.FileSystemRights]::TakeOwnership

function Get-SidDeCuenta
{
    <#
    .SYNOPSIS
        Devuelve el SID de una cuenta (nombre de Windows o SID en texto). Lanza un error claro si no existe.
    #>
    [CmdletBinding()]
    [OutputType([System.Security.Principal.SecurityIdentifier])]
    param([Parameter(Mandatory = $true)][string]$Cuenta)

    try
    {
        if ($Cuenta -match '^S-\d-\d+(-\d+)+$')
        {
            return New-Object System.Security.Principal.SecurityIdentifier $Cuenta
        }

        return (New-Object System.Security.Principal.NTAccount($Cuenta)).Translate([System.Security.Principal.SecurityIdentifier])
    }
    catch
    {
        throw "La cuenta de Windows '$Cuenta' no existe en este equipo."
    }
}

function New-ReglaDeCarpeta
{
    param(
        [Parameter(Mandatory = $true)][System.Security.Principal.SecurityIdentifier]$Sid,
        [Parameter(Mandatory = $true)][System.Security.AccessControl.FileSystemRights]$Derechos
    )

    return New-Object System.Security.AccessControl.FileSystemAccessRule(
        $Sid,
        $Derechos,
        ([System.Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [System.Security.AccessControl.InheritanceFlags]::ObjectInherit),
        [System.Security.AccessControl.PropagationFlags]::None,
        [System.Security.AccessControl.AccessControlType]::Allow)
}

function Set-PermisoDeCarpeta
{
    <#
    .SYNOPSIS
        Deja la carpeta accesible SOLO para Administradores y SYSTEM (control total) y para las cuentas indicadas (modificar).

    .DESCRIPTION
        Corta la herencia SIN copiar las reglas heredadas (SetAccessRuleProtection($true, $false)): copiarlas las deja en
        la lista como reglas explicitas que ya no se pueden distinguir de las propias, y quitarlas con RemoveAccessRule
        no funciona porque Windows las sigue marcando como heredadas en memoria. Despues agrega de forma explicita las
        cuentas permitidas y purga cualquier regla explicita de los grupos amplios (Todos, Usuarios autenticados,
        Usuarios) que haya dejado una ejecucion anterior. Es idempotente.

        Solo se lee y se escribe la lista de acceso (DACL): Set-Acl intenta reescribir tambien el propietario y la
        auditoria, y eso exige privilegios que no hacen falta.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Ruta,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]]$CuentasModificar
    )

    $carpeta = New-Object System.IO.DirectoryInfo $Ruta
    $acl = $carpeta.GetAccessControl([System.Security.AccessControl.AccessControlSections]::Access)

    $acl.SetAccessRuleProtection($true, $false)

    foreach ($sidAmplio in $script:SidsAmplios)
    {
        $acl.PurgeAccessRules((New-Object System.Security.Principal.SecurityIdentifier $sidAmplio))
    }

    $control = [System.Security.AccessControl.FileSystemRights]::FullControl
    $modificar = [System.Security.AccessControl.FileSystemRights]::Modify

    $acl.AddAccessRule((New-ReglaDeCarpeta (New-Object System.Security.Principal.SecurityIdentifier $script:SidAdministradores) $control))
    $acl.AddAccessRule((New-ReglaDeCarpeta (New-Object System.Security.Principal.SecurityIdentifier $script:SidSistema) $control))

    foreach ($cuenta in $CuentasModificar)
    {
        $acl.AddAccessRule((New-ReglaDeCarpeta (Get-SidDeCuenta $cuenta) $modificar))
    }

    $carpeta.SetAccessControl($acl)
}

function Get-EscrituraDeNoAdministradores
{
    <#
    .SYNOPSIS
        Lista los permisos de escritura de cuentas que no son administradores sobre una carpeta y todo lo que contiene.

    .DESCRIPTION
        Se usa para negarse a registrar como SYSTEM un ejecutable que un usuario comun podria reemplazar. Cuentan como
        "no administradores" los grupos amplios (Todos, Usuarios autenticados, Usuarios) y las cuentas indicadas en
        -CuentasExtra (por ejemplo, la cuenta con la que se usa Retail POS). Devuelve texto legible, uno por hallazgo;
        una lista vacia significa que nadie sin privilegios puede modificar el contenido.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)][string]$Ruta,
        [string[]]$CuentasExtra = @()
    )

    $sospechosos = @($script:SidsAmplios)
    foreach ($cuenta in $CuentasExtra)
    {
        $sospechosos += (Get-SidDeCuenta $cuenta).Value
    }

    $elementos = @(Get-Item -LiteralPath $Ruta -Force) + @(Get-ChildItem -LiteralPath $Ruta -Recurse -Force)
    foreach ($elemento in $elementos)
    {
        $acl = $elemento.GetAccessControl([System.Security.AccessControl.AccessControlSections]::Access)
        foreach ($regla in $acl.GetAccessRules($true, $true, [System.Security.Principal.SecurityIdentifier]))
        {
            if ($regla.AccessControlType -ne [System.Security.AccessControl.AccessControlType]::Allow)
            {
                continue
            }

            if ($sospechosos -notcontains $regla.IdentityReference.Value)
            {
                continue
            }

            $derechos = [int]$regla.FileSystemRights -band [int]$script:DerechosDeEscritura
            if ($derechos -ne 0)
            {
                "$($elemento.FullName): $($regla.IdentityReference.Value) tiene $($regla.FileSystemRights)"
            }
        }
    }
}

function Read-ConfiguracionDeCli
{
    <#
    .SYNOPSIS
        Lee Instancia, BaseDeDatos y Directorio del appsettings.json que acompana a Retail.Cli.exe.

    .DESCRIPTION
        Devuelve un objeto con esas tres propiedades; las que no figuran en el archivo (o si el archivo no existe)
        quedan en $null. El directorio sale con las variables de entorno expandidas, igual que en la aplicacion.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$CarpetaCli)

    $configuracion = [pscustomobject]@{ Instancia = $null; BaseDeDatos = $null; Directorio = $null }

    $archivo = Join-Path $CarpetaCli "appsettings.json"
    if (-not (Test-Path -LiteralPath $archivo -PathType Leaf))
    {
        return $configuracion
    }

    $json = Get-Content -LiteralPath $archivo -Raw -Encoding UTF8 | ConvertFrom-Json

    $conexiones = $json.PSObject.Properties["ConnectionStrings"]
    if ($conexiones -and $conexiones.Value.PSObject.Properties["DefaultConnection"])
    {
        $constructor = New-Object System.Data.SqlClient.SqlConnectionStringBuilder ([string]$conexiones.Value.DefaultConnection)
        if ($constructor.DataSource) { $configuracion.Instancia = $constructor.DataSource }
        if ($constructor.InitialCatalog) { $configuracion.BaseDeDatos = $constructor.InitialCatalog }
    }

    $respaldos = $json.PSObject.Properties["BackupSettings"]
    if ($respaldos -and $respaldos.Value.PSObject.Properties["Directorio"] -and $respaldos.Value.Directorio)
    {
        $configuracion.Directorio = [Environment]::ExpandEnvironmentVariables([string]$respaldos.Value.Directorio)
    }

    return $configuracion
}

function Test-InstanciaValida
{
    <#
    .SYNOPSIS
        Acepta solo nombres de instancia con letras, numeros y . _ - \ , : ( ): rechaza ; = comillas y espacios.

    .DESCRIPTION
        El valor se inserta en una cadena de conexion; sin esta validacion, "x;Initial Catalog=otra" cambiaria la
        conexion que usa el script (que corre como administrador).
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param([string]$Instancia)

    return [bool]($Instancia -match '^[A-Za-z0-9_.\-\\,:()]+$')
}

Export-ModuleMember -Function Get-SidDeCuenta, Set-PermisoDeCarpeta, Get-EscrituraDeNoAdministradores, Read-ConfiguracionDeCli, Test-InstanciaValida
