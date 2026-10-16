<#
.SYNOPSIS
    Registra la tarea programada de la copia de seguridad diaria de Retail POS (Fase 7, D-28 y D-29).

.DESCRIPTION
    Prepara todo lo necesario para que "Retail.Cli backup" corra solo, todos los dias, sin que nadie inicie sesion:

      1. Registra la tarea contra el Retail.Cli.exe de la aplicacion INSTALADA (la carpeta donde esta este script; no hace
         ninguna copia aparte). Asi, al actualizar la aplicacion (reemplazar los archivos de esa carpeta como administrador)
         la tarea ejecuta automaticamente el Retail.Cli nuevo y no hace falta volver a registrarla. La aplicacion se instala
         descomprimiendo el ZIP de la version como administrador en una carpeta que solo los administradores puedan
         modificar, por ejemplo C:\Program Files\Retail POS. La tarea corre como SYSTEM: si el ejecutable estuviera en una
         carpeta que un usuario comun puede modificar (el perfil, Descargas), cualquiera podria reemplazarlo y ejecutar
         codigo como SYSTEM. Por eso el script se niega a continuar si la carpeta, su contenido O CUALQUIER CARPETA
         SUPERIOR hasta la raiz del disco puede ser modificada por Todos, Usuarios autenticados, Usuarios o la cuenta de
         la aplicacion (quien pudiera renombrar una carpeta superior pondria otro ejecutable en su lugar). No hay
         excepciones: ninguna subcarpeta de la instalacion debe ser escribible por no administradores. Los registros
         NO se escriben en la instalacion: la aplicacion y la herramienta usan %LOCALAPPDATA%\Retail POS\logs de la
         cuenta que las ejecuta (para la tarea, que corre como SYSTEM, es
         C:\Windows\System32\config\systemprofile\AppData\Local\Retail POS\logs, legible por administradores). Si existe
         la copia que creaba una version anterior de este script ("Program Files\Retail POS\Cli"), ofrece eliminarla.
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
    Ruta de Retail.Cli.exe de la aplicacion instalada. Por defecto se busca junto al script o en su carpeta padre. La
    tarea necesita el ejecutable publicado: no sirve "dotnet run". La tarea apunta directamente a este archivo (no se
    copia), asi que su carpeta debe estar protegida: solo administradores pueden modificarla ni ninguna carpeta superior.

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
    Elimina la tarea programada. No borra la carpeta de respaldos ni sus copias, ni el login de SQL Server, ni la
    aplicacion instalada.

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
# Carpeta "Cli" que creaba la version anterior de este script (copia del CLI): hoy se ofrece eliminarla.
$CopiaAnteriorCli = Join-Path $env:ProgramFiles "Retail POS\Cli"
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

function Test-EstaDentroDe([string]$Ruta, [string]$Carpeta)
{
    $carpetaCompleta = [IO.Path]::GetFullPath($Carpeta).TrimEnd('\') + "\"
    return [IO.Path]::GetFullPath($Ruta).TrimEnd('\').StartsWith($carpetaCompleta, [StringComparison]::OrdinalIgnoreCase)
}

function Find-RutaCli
{
    $candidatos = @(
        (Join-Path $PSScriptRoot "Retail.Cli.exe"),
        (Join-Path (Split-Path $PSScriptRoot -Parent) "Retail.Cli.exe")
    )
    return $candidatos | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
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
    # La configuracion se busca junto al Retail.Cli.exe instalado (el indicado o el que acompana al script).
    $rutaCliDesregistro = if ($RutaCli) { $RutaCli } else { Find-RutaCli }
    Resolve-Configuracion $(if ($rutaCliDesregistro) { Split-Path $rutaCliDesregistro -Parent } else { $null })

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

    Write-Host "No se tocaron la carpeta de respaldos ($Directorio), sus copias, el login de SQL Server ($CuentaSistema) ni la aplicacion instalada."
    return
}

if (-not $RutaCli)
{
    $RutaCli = Find-RutaCli
}

if (-not $RutaCli -or -not (Test-Path -LiteralPath $RutaCli -PathType Leaf))
{
    throw "No se encontro Retail.Cli.exe junto al script ni en su carpeta padre. La tarea necesita el ejecutable publicado: indique su ruta con -RutaCli."
}

$RutaCli = (Resolve-Path -LiteralPath $RutaCli).Path
$CarpetaCli = Split-Path $RutaCli -Parent
Resolve-Configuracion $CarpetaCli

# La tarea ejecuta directamente el Retail.Cli.exe de la aplicacion instalada (sin copia aparte).
$RutaCliTarea = $RutaCli
$CarpetaLogsTarea = Join-Path $env:SystemRoot "System32\config\systemprofile\AppData\Local\Retail POS\logs"
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

# --- Proteccion de la instalacion (escalada de privilegios) ----------------------------------------------------------------
# La tarea corre como SYSTEM directamente desde esta carpeta: si alguien sin privilegios puede modificar el ejecutable, su
# contenido o cualquier carpeta superior (renombrarla y poner otra con su propio ejecutable), obtiene SYSTEM. Se comprueba
# tambien en -WhatIf, antes de consultar SQL Server. No hay excepciones: los registros van a %LOCALAPPDATA%, no a la instalacion.
$escribibles = @(Get-EscrituraEnInstalacion -Ruta $CarpetaCli -CuentasExtra @($UsuarioApp))
if ($escribibles.Count -gt 0)
{
    $detalle = ($escribibles | Select-Object -First 5) -join [Environment]::NewLine
    throw ("Instale Retail POS en una carpeta que solo puedan modificar los administradores, por ejemplo C:\Program Files\Retail POS (descomprima el ZIP ahi como administrador), y vuelva a ejecutar el script desde esa carpeta." +
        "$([Environment]::NewLine)La carpeta '$CarpetaCli' (o su contenido, o una carpeta superior) puede ser modificada por usuarios sin privilegios y la tarea corre como SYSTEM; esos usuarios podrian reemplazar el ejecutable y obtener SYSTEM. Hallazgos:$([Environment]::NewLine)$detalle")
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
Write-Host "Instalacion:        $CarpetaCli"
Write-Host "Ejecutable (tarea): $RutaCliTarea"
Write-Host "Registros (tarea):  $CarpetaLogsTarea"
Write-Host "Hora:               $Hora todos los dias"
Write-Host ""

# --- 1. Carpeta de respaldos y permisos -----------------------------------------------------------------------------

if ($PSCmdlet.ShouldProcess($Directorio, "Crear la carpeta si falta y dejarla solo para Administradores y SYSTEM (control total) y Modificar a: $cuentaMotor, $CuentaSistema, $UsuarioApp (sin herencia ni grupos amplios)"))
{
    if (-not (Test-Path -LiteralPath $Directorio))
    {
        [void](New-Item -ItemType Directory -Path $Directorio)
    }

    Set-PermisoDeCarpeta -Ruta $Directorio -CuentasModificar @($cuentaMotor, $CuentaSistema, $UsuarioApp)
    Write-Host "Carpeta de respaldos lista."
}

# --- 2. Login y permisos de SQL Server ------------------------------------------------------------------------------

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

# --- 3. Tarea programada --------------------------------------------------------------------------------------------

if ($PSCmdlet.ShouldProcess($NombreTarea, "Registrar (o actualizar) la tarea: '$RutaCliTarea backup' como $CuentaSistema a las $Hora, iniciar si se perdio"))
{
    $horaDelDia = [datetime]::ParseExact($Hora, "HH:mm", [System.Globalization.CultureInfo]::InvariantCulture)

    $descripcion = "Copia de seguridad diaria de Retail POS (Retail.Cli backup). Si la PC estaba apagada, corre al encenderla."
    if ($concedeElScript)
    {
        $descripcion = "$descripcion $MarcaPermisoDeServidor"
    }

    $accion = New-ScheduledTaskAction -Execute $RutaCliTarea -Argument "backup" -WorkingDirectory $CarpetaCli
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

# --- 4. Migracion desde la version anterior del script ----------------------------------------------------------------
# La version anterior copiaba el CLI a "Program Files\Retail POS\Cli" y registraba la tarea con esa copia, que quedaba
# desactualizada al actualizar la aplicacion. La tarea ya apunta a la instalacion (arriba), asi que la copia sobra.
# Se elimina despues de registrar la tarea para que nunca quede una tarea apuntando a una carpeta borrada.

$copiaAnteriorEliminada = $false
if ((Test-Path -LiteralPath $CopiaAnteriorCli) -and -not (Test-MismaRuta $CarpetaCli $CopiaAnteriorCli) -and -not (Test-EstaDentroDe $CarpetaCli $CopiaAnteriorCli))
{
    if ($PSCmdlet.ShouldProcess($CopiaAnteriorCli, "Eliminar la copia del CLI que creaba la version anterior de este script (sus registros incluidos): la tarea ya usa la instalacion"))
    {
        Remove-Item -LiteralPath $CopiaAnteriorCli -Recurse -Force
        $copiaAnteriorEliminada = $true
        Write-Host "Copia anterior eliminada: $CopiaAnteriorCli."
    }
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
Write-Host "  - La tarea usa el Retail.Cli de la aplicacion instalada ($CarpetaCli): al actualizar la aplicacion (reemplazando los"
Write-Host "    archivos de esa carpeta como administrador) la tarea ejecuta el CLI nuevo sin volver a registrarla."
Write-Host "  - Si restaura una copia de seguridad tomada antes de este script, o una migracion agrega tablas que usa la copia,"
Write-Host "    vuelva a ejecutar este script como administrador (los permisos de SQL Server viven dentro de la base)."
if ($copiaAnteriorEliminada)
{
    Write-Host "  - Se elimino la copia del CLI que creaba la version anterior del script: $CopiaAnteriorCli."
}
elseif ((Test-Path -LiteralPath $CopiaAnteriorCli) -and -not (Test-MismaRuta $CarpetaCli $CopiaAnteriorCli))
{
    Write-Host "  - Queda la copia antigua del CLI en ${CopiaAnteriorCli}: ya no se usa; puede eliminarla."
}
Write-Host ""
Write-Host "Para probarla ahora:"
Write-Host "  Start-ScheduledTask -TaskName `"$NombreTarea`""
Write-Host "  Get-ScheduledTaskInfo -TaskName `"$NombreTarea`"   (LastTaskResult 0 = exito)"
Write-Host ""
Write-Host "Donde mirar el resultado:"
Write-Host "  - Registros de la tarea (como SYSTEM, legibles por administradores): $CarpetaLogsTarea"
Write-Host "  - Registros de la aplicacion de cada usuario: %LOCALAPPDATA%\Retail POS\logs"
Write-Host "  - Historial de copias en Retail POS (pantalla de Respaldos): la copia figura como Automatica."
Write-Host ""
Write-Host "Para quitarla: .\registrar-tarea-backup.ps1 -Desregistrar   (agregue -RevocarPermisos para quitar tambien CREATE ANY DATABASE)"
