<#
.SYNOPSIS
    Registra la tarea programada de la copia de seguridad diaria de Retail POS (Fase 7, D-28 y D-29).

.DESCRIPTION
    Prepara todo lo necesario para que "Retail.Cli backup" corra solo, todos los dias, sin que nadie inicie sesion:

      1. Copia la carpeta de Retail.Cli (ejecutable, bibliotecas y appsettings.json) a "Program Files\Retail POS\Cli" y
         registra la tarea con ESA copia. La tarea corre como SYSTEM: si apuntara a una carpeta que un usuario comun puede
         modificar (el perfil, Descargas, la carpeta de publicacion), cualquier usuario podria reemplazar el ejecutable y
         ejecutar codigo como SYSTEM. Si la carpeta de destino resulta escribible por un usuario sin privilegios, el
         script se niega a continuar. Volver a ejecutar el script despues de actualizar la aplicacion refresca esa copia.
      2. Crea la carpeta de respaldos y la deja accesible SOLO para Administradores y SYSTEM (control total) y, con permiso
         de modificacion, para el servicio de SQL Server (escribe los .bak), SYSTEM (la tarea escribe manifiestos y aplica
         la retencion) y la cuenta que usa Retail POS (las copias manuales desde la aplicacion calculan el SHA-256 y
         escriben el manifiesto). Los respaldos contienen datos personales: se corta la herencia sin copiar las reglas
         heredadas y se quitan los grupos amplios (Usuarios, Usuarios autenticados, Todos).
      3. Crea el login de SQL Server "NT AUTHORITY\SYSTEM" (si falta) y le da los permisos MINIMOS para copiar y
         registrar el historial (ver la lista $Permisos mas abajo, con el motivo de cada uno). No puede leer ninguna tabla
         de datos de negocio ni de usuarios.
      4. Registra la tarea "Retail POS - Copia de seguridad diaria": ejecuta "Retail.Cli.exe backup" como SYSTEM
         (sin guardar contrasenas) a la hora indicada. Si la PC estaba apagada a esa hora, la copia corre apenas
         se enciende ("iniciar si se perdio"); si SQL Server todavia esta arrancando, la herramienta espera hasta 2 minutos
         a que acepte conexiones. Volver a ejecutar el script actualiza la tarea (es idempotente).

    Los permisos del login y del usuario SYSTEM dentro de la base viven en la propia base de datos: si se restaura una
    copia tomada ANTES de ejecutar este script, o si una migracion agrega tablas que usa la copia, hay que volver a
    ejecutar el script (la restauracion avisa cuando falta).

    Requisitos: ejecutar como administrador de Windows (en una consola "Ejecutar como administrador"), con un usuario
    que sea sysadmin de la instancia de SQL Server, y con la base ya creada y migrada (Retail.Cli migrar).
    Con -WhatIf se muestra cada accion sin ejecutarla; ese modo no exige ser administrador.

.PARAMETER Hora
    Hora diaria de la copia, en formato HH:mm de 24 horas. Por defecto 21:00.

.PARAMETER RutaCli
    Ruta de Retail.Cli.exe. Por defecto se busca junto al script o en su carpeta padre (instalacion publicada). La tarea
    necesita el ejecutable publicado: no sirve "dotnet run". Se copia TODA su carpeta a "Program Files\Retail POS\Cli".

.PARAMETER Instancia
    Instancia de SQL Server. Por defecto sale de ConnectionStrings:DefaultConnection del appsettings.json que acompana a
    Retail.Cli.exe; si no figura, .\SQLEXPRESS. Un valor explicito tiene prioridad.

.PARAMETER BaseDeDatos
    Nombre de la base de Retail POS. Por defecto sale del mismo appsettings.json; si no figura, RetailDb.

.PARAMETER Directorio
    Carpeta de respaldos. Por defecto sale de BackupSettings:Directorio del appsettings.json (con las variables de entorno
    expandidas); si no figura, C:\RetailData\Backups. Debe coincidir con lo que usa la herramienta: si se indica un valor
    distinto del appsettings.json, las copias seguirian yendo a la carpeta del appsettings.json.

.PARAMETER UsuarioApp
    Cuenta de Windows con la que se usa Retail POS (por ejemplo, PC-CAJA\gerente). Por defecto, el usuario que ejecuta
    el script. Si se ejecuta con OTRA cuenta de administrador, indique aqui la cuenta de la aplicacion: si no, las
    copias manuales desde la aplicacion fallarian por falta de permiso en la carpeta.

.PARAMETER Desregistrar
    Elimina la tarea programada. No borra la carpeta de respaldos ni sus copias, ni el login de SQL Server, ni la copia
    de la herramienta en Program Files.

.PARAMETER RevocarPermisos
    Solo junto con -Desregistrar. Ademas de eliminar la tarea, ofrece (con confirmacion) quitar el permiso de servidor
    CREATE ANY DATABASE a NT AUTHORITY\SYSTEM, pero solo si lo concedio este script (la tarea registrada lo recuerda).
    Es opcional porque otro software del equipo que corra como SYSTEM podria necesitar ese permiso. Los permisos dentro
    de la base de Retail POS no se tocan.

.EXAMPLE
    .\registrar-tarea-backup.ps1

    Registra la copia diaria a las 21:00 con los valores del appsettings.json de la herramienta.

.EXAMPLE
    .\registrar-tarea-backup.ps1 -Hora "22:30" -UsuarioApp "PC-CAJA\gerente" -WhatIf

    Muestra que haria para una copia a las 22:30 con la aplicacion usada por PC-CAJA\gerente, sin cambiar nada.

.EXAMPLE
    .\registrar-tarea-backup.ps1 -Desregistrar -RevocarPermisos

    Quita la tarea programada y, si el script habia concedido CREATE ANY DATABASE, pide confirmacion para revocarlo.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [ValidatePattern('^([01]\d|2[0-3]):[0-5]\d$')]
    [string]$Hora = "21:00",

    [string]$RutaCli,

    [string]$Instancia,

    [ValidatePattern('^[A-Za-z0-9_\-]+$')]
    [string]$BaseDeDatos,

    [string]$Directorio,

    [string]$UsuarioApp = [Security.Principal.WindowsIdentity]::GetCurrent().Name,

    [switch]$Desregistrar,

    [switch]$RevocarPermisos
)

$ErrorActionPreference = "Stop"

Import-Module (Join-Path $PSScriptRoot "RetailPermisos.psm1") -Force

$NombreTarea = "Retail POS - Copia de seguridad diaria"
$CuentaSistema = "NT AUTHORITY\SYSTEM"
$DestinoCli = Join-Path $env:ProgramFiles "Retail POS\Cli"
# Texto que la tarea registrada lleva en su descripcion cuando ESTE script fue quien concedio CREATE ANY DATABASE.
$MarcaPermisoDeServidor = "[permiso-de-servidor-concedido-por-el-script]"

# --------------------------------------------------------------------------------------------------------------------
# Permisos de SQL Server para NT AUTHORITY\SYSTEM: es la lista COMPLETA y la unica. {0} es la cuenta, ya delimitada.
# La prueba de integracion PermisosTareaProgramadaIntegracionTests lee estas lineas y comprueba con una cuenta de
# prueba que "Retail.Cli backup" funciona con exactamente estos permisos: si se agrega o quita uno, se prueba solo.
# La copia NO lee USUARIOS: ComandoBackup consulta solo el esquema (migraciones), no el estado de los usuarios.
# --------------------------------------------------------------------------------------------------------------------
$Permisos = @(
    @{ Ambito = 'Servidor'; Sql = 'GRANT CREATE ANY DATABASE TO {0};'; Motivo = 'RESTORE VERIFYONLY (verificacion de la copia) lo exige aunque no cree ninguna base' },
    @{ Ambito = 'BaseDeDatos'; Sql = 'ALTER ROLE db_backupoperator ADD MEMBER {0};'; Motivo = 'BACKUP DATABASE de la base' },
    @{ Ambito = 'BaseDeDatos'; Sql = 'GRANT SELECT ON OBJECT::dbo.[__EFMigrationsHistory] TO {0};'; Motivo = 'Leer el estado del esquema: la copia solo corre si la base esta al dia' },
    @{ Ambito = 'BaseDeDatos'; Sql = 'GRANT SELECT, INSERT, UPDATE ON OBJECT::dbo.REGISTROS_BACKUP TO {0};'; Motivo = 'Historial de copias: alta, cambios de estado y retencion (sin DELETE: el borrado es logico)' }
    # sp_getapplock sobre master (bloqueo contra restauraciones) no necesita permiso propio: lo permite el rol public.
)

function Test-Administrador
{
    $identidad = [Security.Principal.WindowsIdentity]::GetCurrent()
    return ([Security.Principal.WindowsPrincipal]$identidad).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-Delimitado([string]$Identificador)
{
    return "[" + $Identificador.Replace("]", "]]") + "]"
}

function Invoke-Sql([string]$Base, [string]$Sql, [bool]$Escalar = $false)
{
    $cadena = "Server=$Instancia;Database=$Base;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=15"
    $conexion = New-Object System.Data.SqlClient.SqlConnection $cadena
    try
    {
        $conexion.Open()
        $comando = $conexion.CreateCommand()
        $comando.CommandText = $Sql
        if ($Escalar)
        {
            return $comando.ExecuteScalar()
        }

        [void]$comando.ExecuteNonQuery()
    }
    finally
    {
        $conexion.Dispose()
    }
}

function Resolve-Configuracion([string]$CarpetaConfiguracion)
{
    # Un parametro explicito siempre gana; si no, manda el appsettings.json de la herramienta; si tampoco figura, el valor de fabrica.
    $configuracion = if ($CarpetaConfiguracion) { Read-ConfiguracionDeCli $CarpetaConfiguracion } else { [pscustomobject]@{ Instancia = $null; BaseDeDatos = $null; Directorio = $null } }

    if (-not $script:Instancia)
    {
        $script:Instancia = if ($configuracion.Instancia) { $configuracion.Instancia } else { ".\SQLEXPRESS" }
    }

    if (-not $script:BaseDeDatos)
    {
        $script:BaseDeDatos = if ($configuracion.BaseDeDatos) { $configuracion.BaseDeDatos } else { "RetailDb" }
    }

    if (-not $script:Directorio)
    {
        $script:Directorio = if ($configuracion.Directorio) { $configuracion.Directorio } else { "C:\RetailData\Backups" }
    }

    if (-not (Test-InstanciaValida $script:Instancia))
    {
        throw "La instancia '$($script:Instancia)' no es valida: solo se admiten letras, numeros y . _ - \ , : ( ) (sin punto y coma, signo igual, comillas ni espacios)."
    }

    if ($script:BaseDeDatos -notmatch '^[A-Za-z0-9_\-]+$')
    {
        throw "El nombre de la base '$($script:BaseDeDatos)' no es valido: solo se admiten letras, numeros, guion y guion bajo."
    }
}

function Test-MismaRuta([string]$A, [string]$B)
{
    return [IO.Path]::GetFullPath($A).TrimEnd('\') -ieq [IO.Path]::GetFullPath($B).TrimEnd('\')
}

# --- Requisitos -----------------------------------------------------------------------------------------------------

$vistaPrevia = [bool]$WhatIfPreference
if (-not (Test-Administrador))
{
    if (-not $vistaPrevia)
    {
        throw "Este script debe ejecutarse como administrador: abra PowerShell con 'Ejecutar como administrador' y vuelva a intentarlo (con -WhatIf se puede ver la vista previa sin permisos)."
    }

    Write-Warning "Vista previa sin permisos de administrador: para aplicar los cambios hay que ejecutar el script como administrador."
}

if ($RevocarPermisos -and -not $Desregistrar)
{
    throw "-RevocarPermisos solo se usa junto con -Desregistrar."
}

$tareaActual = Get-ScheduledTask -TaskName $NombreTarea -ErrorAction SilentlyContinue

if ($Desregistrar)
{
    # La configuracion se busca en la copia instalada por este script: la carpeta original puede no existir mas.
    Resolve-Configuracion $(if ($RutaCli) { Split-Path $RutaCli -Parent } elseif (Test-Path -LiteralPath $DestinoCli) { $DestinoCli } else { $null })

    if ($RevocarPermisos)
    {
        $cuentaSqlDesregistro = Get-Delimitado $CuentaSistema
        if ($tareaActual -and $tareaActual.Description -and $tareaActual.Description.Contains($MarcaPermisoDeServidor))
        {
            if ($PSCmdlet.ShouldProcess("$Instancia (master)", "REVOKE CREATE ANY DATABASE FROM $cuentaSqlDesregistro  -- otro software que corra como SYSTEM podria necesitarlo"))
            {
                Invoke-Sql "master" "IF SUSER_ID(N'$CuentaSistema') IS NOT NULL REVOKE CREATE ANY DATABASE FROM $cuentaSqlDesregistro;"
                Write-Host "Permiso CREATE ANY DATABASE revocado a $CuentaSistema."
            }
        }
        else
        {
            Write-Host "No se revoca CREATE ANY DATABASE: este script no lo concedio (o la tarea ya no esta registrada), asi que no se sabe si otro software lo necesita."
        }
    }

    if ($tareaActual)
    {
        if ($PSCmdlet.ShouldProcess($NombreTarea, "Eliminar la tarea programada"))
        {
            Unregister-ScheduledTask -TaskName $NombreTarea -Confirm:$false
            Write-Host "Tarea '$NombreTarea' eliminada."
        }
    }
    else
    {
        Write-Host "La tarea '$NombreTarea' no estaba registrada: no hay nada que eliminar."
    }

    Write-Host "No se tocaron la carpeta de respaldos ($Directorio), sus copias, el login de SQL Server ($CuentaSistema) ni la copia de la herramienta ($DestinoCli)."
    return
}

if (-not $RutaCli)
{
    $candidatos = @(
        (Join-Path $PSScriptRoot "Retail.Cli.exe"),
        (Join-Path (Split-Path $PSScriptRoot -Parent) "Retail.Cli.exe")
    )
    $RutaCli = $candidatos | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if (-not $RutaCli -or -not (Test-Path -LiteralPath $RutaCli -PathType Leaf))
{
    throw "No se encontro Retail.Cli.exe junto al script ni en su carpeta padre. La tarea necesita el ejecutable publicado: indique su ruta con -RutaCli."
}

$RutaCli = (Resolve-Path -LiteralPath $RutaCli).Path
$CarpetaCli = Split-Path $RutaCli -Parent
Resolve-Configuracion $CarpetaCli

# Lo que la tarea ejecuta de verdad es la copia protegida, no el original.
$RutaCliTarea = Join-Path $DestinoCli (Split-Path $RutaCli -Leaf)
$Directorio = [System.IO.Path]::GetFullPath($Directorio).TrimEnd('\')

# Se cambian los permisos de la carpeta: nunca de la raiz de un disco.
if ($Directorio -eq [System.IO.Path]::GetPathRoot($Directorio).TrimEnd('\'))
{
    throw "El directorio de respaldos no puede ser la raiz de un disco: use una carpeta dedicada (por ejemplo, C:\RetailData\Backups)."
}

try
{
    [void](Get-SidDeCuenta $UsuarioApp)
}
catch
{
    throw "La cuenta de Windows '$UsuarioApp' no existe en este equipo. Indique una cuenta valida con -UsuarioApp (formato EQUIPO\usuario)."
}

# --- Instancia de SQL Server ----------------------------------------------------------------------------------------

try
{
    $esSysadmin = Invoke-Sql "master" "SELECT IS_SRVROLEMEMBER('sysadmin');" $true
    $cuentaMotor = Invoke-Sql "master" "SELECT TOP (1) service_account FROM sys.dm_server_services WHERE servicename LIKE 'SQL Server (%';" $true
    $existeBase = Invoke-Sql "master" "SELECT DB_ID(N'$($BaseDeDatos.Replace("'", "''"))');" $true
    $cantidadPermisoServidor = Invoke-Sql "master" "SELECT COUNT(*) FROM sys.server_permissions p JOIN sys.server_principals s ON s.principal_id = p.grantee_principal_id WHERE s.name = N'$CuentaSistema' AND p.permission_name = N'CREATE ANY DATABASE' AND p.state IN ('G', 'W');" $true
}
catch
{
    throw "No se pudo consultar la instancia '$Instancia': $($_.Exception.Message)"
}

if ($esSysadmin -ne 1)
{
    throw "El usuario actual no es sysadmin de '$Instancia': no puede crear el login ni dar permisos. Ejecute el script con la cuenta que instalo SQL Server."
}

if ($existeBase -is [DBNull] -or $null -eq $existeBase)
{
    throw "La base '$BaseDeDatos' no existe en '$Instancia'. Cree y migre la base primero (Retail.Cli migrar) y vuelva a ejecutar este script."
}

if ([string]::IsNullOrWhiteSpace($cuentaMotor))
{
    throw "SQL Server no informo la cuenta de su servicio. No se pueden dar permisos sobre la carpeta de respaldos."
}

# CREATE ANY DATABASE: solo se considera "concedido por el script" si no lo tenia antes o si una ejecucion anterior ya lo marco.
$yaMarcado = $tareaActual -and $tareaActual.Description -and $tareaActual.Description.Contains($MarcaPermisoDeServidor)
$concedeElScript = $yaMarcado -or ($cantidadPermisoServidor -eq 0)

Write-Host "Instancia:          $Instancia (servicio: $cuentaMotor)"
Write-Host "Base:               $BaseDeDatos"
Write-Host "Carpeta:            $Directorio"
Write-Host "Cuenta de la app:   $UsuarioApp"
Write-Host "Origen del CLI:     $CarpetaCli"
Write-Host "Ejecutable (copia): $RutaCliTarea"
Write-Host "Hora:               $Hora todos los dias"
Write-Host ""

# --- 1. Copia protegida de la herramienta ---------------------------------------------------------------------------

$RaizCli = Split-Path $DestinoCli -Parent

if (Test-MismaRuta $CarpetaCli $DestinoCli)
{
    Write-Host "Retail.Cli ya se ejecuta desde ${DestinoCli}: no hace falta copiarlo."
}
elseif ($PSCmdlet.ShouldProcess($DestinoCli, "Copiar TODA la carpeta '$CarpetaCli' (ejecutable, bibliotecas y appsettings.json; sin los registros) para que la tarea no ejecute como SYSTEM un archivo que un usuario comun pueda reemplazar"))
{
    if (-not (Test-Path -LiteralPath $DestinoCli))
    {
        [void](New-Item -ItemType Directory -Path $DestinoCli -Force)
    }

    # Se vacia la copia anterior (salvo sus registros) para no arrastrar bibliotecas que la version nueva ya no usa.
    Get-ChildItem -LiteralPath $DestinoCli -Force | Where-Object { $_.Name -ne "logs" } | Remove-Item -Recurse -Force
    Get-ChildItem -LiteralPath $CarpetaCli -Force | Where-Object { $_.Name -ne "logs" } | Copy-Item -Destination $DestinoCli -Recurse -Force
    Write-Host "Retail.Cli copiado a $DestinoCli."
}

if ($PSCmdlet.ShouldProcess((Join-Path $DestinoCli "logs"), "Crear la carpeta de registros con control total solo para Administradores y SYSTEM"))
{
    $carpetaLogs = Join-Path $DestinoCli "logs"
    if (-not (Test-Path -LiteralPath $carpetaLogs))
    {
        [void](New-Item -ItemType Directory -Path $carpetaLogs -Force)
    }

    Set-PermisoDeCarpeta -Ruta $carpetaLogs -CuentasModificar @()
}

# Si alguien sin privilegios pudiera modificar la copia (permisos heredados raros, una carpeta preexistente), la tarea
# seria una escalada de privilegios: se aborta antes de registrarla.
if (Test-Path -LiteralPath $RaizCli)
{
    $escribibles = @(Get-EscrituraDeNoAdministradores -Ruta $RaizCli -CuentasExtra @($UsuarioApp))
    if ($escribibles.Count -gt 0)
    {
        $detalle = ($escribibles | Select-Object -First 5) -join [Environment]::NewLine
        throw "La carpeta de la herramienta ($RaizCli) puede ser modificada por usuarios sin privilegios y la tarea corre como SYSTEM. Corrija sus permisos (solo Administradores y SYSTEM deben poder escribir) y vuelva a ejecutar el script:$([Environment]::NewLine)$detalle"
    }
}

# --- 2. Carpeta de respaldos y permisos -----------------------------------------------------------------------------

if ($PSCmdlet.ShouldProcess($Directorio, "Crear la carpeta si falta y dejarla solo para Administradores y SYSTEM (control total) y Modificar a: $cuentaMotor, $CuentaSistema, $UsuarioApp (sin herencia ni grupos amplios)"))
{
    if (-not (Test-Path -LiteralPath $Directorio))
    {
        [void](New-Item -ItemType Directory -Path $Directorio)
    }

    Set-PermisoDeCarpeta -Ruta $Directorio -CuentasModificar @($cuentaMotor, $CuentaSistema, $UsuarioApp)
    Write-Host "Carpeta de respaldos lista."
}

# --- 3. Login y permisos de SQL Server ------------------------------------------------------------------------------

$cuentaSql = Get-Delimitado $CuentaSistema

if ($PSCmdlet.ShouldProcess("$Instancia (master)", "Crear el login $CuentaSistema si falta"))
{
    Invoke-Sql "master" "IF SUSER_ID(N'$CuentaSistema') IS NULL CREATE LOGIN $cuentaSql FROM WINDOWS;"
}

if ($PSCmdlet.ShouldProcess("$Instancia ($BaseDeDatos)", "Crear el usuario $CuentaSistema si falta"))
{
    Invoke-Sql $BaseDeDatos "IF DATABASE_PRINCIPAL_ID(N'$CuentaSistema') IS NULL CREATE USER $cuentaSql FOR LOGIN $cuentaSql;"
}

foreach ($permiso in $Permisos)
{
    $sql = $permiso.Sql -f $cuentaSql
    $base = if ($permiso.Ambito -eq 'Servidor') { "master" } else { $BaseDeDatos }

    if ($PSCmdlet.ShouldProcess("$Instancia ($base)", "$sql  -- $($permiso.Motivo)"))
    {
        Invoke-Sql $base $sql
    }
}

# --- 4. Tarea programada --------------------------------------------------------------------------------------------

if ($PSCmdlet.ShouldProcess($NombreTarea, "Registrar (o actualizar) la tarea: '$RutaCliTarea backup' como $CuentaSistema a las $Hora, iniciar si se perdio"))
{
    $horaDelDia = [datetime]::ParseExact($Hora, "HH:mm", [System.Globalization.CultureInfo]::InvariantCulture)

    $descripcion = "Copia de seguridad diaria de Retail POS (Retail.Cli backup). Si la PC estaba apagada, corre al encenderla."
    if ($concedeElScript)
    {
        $descripcion = "$descripcion $MarcaPermisoDeServidor"
    }

    $accion = New-ScheduledTaskAction -Execute $RutaCliTarea -Argument "backup" -WorkingDirectory $DestinoCli
    $disparador = New-ScheduledTaskTrigger -Daily -At $horaDelDia
    $principal = New-ScheduledTaskPrincipal -UserId $CuentaSistema -LogonType ServiceAccount -RunLevel Highest
    $configuracion = New-ScheduledTaskSettingsSet `
        -StartWhenAvailable `
        -MultipleInstances IgnoreNew `
        -ExecutionTimeLimit (New-TimeSpan -Hours 2) `
        -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries

    [void](Register-ScheduledTask `
        -TaskName $NombreTarea `
        -Description $descripcion `
        -Action $accion `
        -Trigger $disparador `
        -Principal $principal `
        -Settings $configuracion `
        -Force)

    Write-Host "Tarea '$NombreTarea' registrada."
}

# --- Resumen --------------------------------------------------------------------------------------------------------

if ($vistaPrevia)
{
    Write-Host ""
    Write-Host "Vista previa (-WhatIf): no se cambio nada."
    return
}

Write-Host ""
Write-Host "Resumen"
Write-Host "  - Todos los dias a las $Hora corre: $RutaCliTarea backup (como $CuentaSistema)."
Write-Host "  - Si la PC estaba apagada a esa hora, la copia corre al encenderla (y espera hasta 2 minutos a que SQL Server este listo)."
Write-Host "  - Las copias se guardan en $Directorio (la cantidad que se conserva sale de BackupSettings en appsettings.json)."
Write-Host "  - La tarea usa la copia protegida de Retail.Cli en $DestinoCli. Despues de actualizar la aplicacion, o de restaurar"
Write-Host "    una copia de seguridad o migrar la base, vuelva a ejecutar este script como administrador."
Write-Host ""
Write-Host "Para probarla ahora:"
Write-Host "  Start-ScheduledTask -TaskName `"$NombreTarea`""
Write-Host "  Get-ScheduledTaskInfo -TaskName `"$NombreTarea`"   (LastTaskResult 0 = exito)"
Write-Host ""
Write-Host "Donde mirar el resultado:"
Write-Host "  - Registro de la herramienta (solo Administradores): $(Join-Path $DestinoCli 'logs')"
Write-Host "  - Historial de copias en Retail POS (pantalla de Respaldos): la copia figura como Automatica."
Write-Host ""
Write-Host "Para quitarla: .\registrar-tarea-backup.ps1 -Desregistrar   (agregue -RevocarPermisos para quitar tambien CREATE ANY DATABASE)"
