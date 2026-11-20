<#
.SYNOPSIS
    Crea el login de SQL Server "retail_app" con permisos minimos para las terminales de Retail POS (MT-10). Se ejecuta UNA
    VEZ, en la PC servidor.

.DESCRIPTION
    En una red sin dominio, el usuario de Windows de una terminal no existe para la PC servidor y la conexion con
    autenticacion de Windows falla. Las terminales se conectan entonces con un login de SQL Server propio de la aplicacion
    (no de cada cajero: las personas siguen entrando con su usuario y contrasena de Retail). Este script lo prepara:

      1. Activa la autenticacion mixta ("SQL Server y Windows") si hace falta (LoginMode = 2 en el registro de la instancia).
         Ese cambio solo rige cuando se reinicia el servicio de SQL Server: el script lo explica y, con -ReiniciarServicio,
         lo reinicia el mismo (corta las conexiones abiertas: hacerlo fuera del horario de venta).
      2. Crea el login (o, si ya existe, le cambia la contrasena) con CHECK_POLICY = ON (la contrasena debe cumplir la
         politica de Windows: longitud, mayusculas, minusculas, numeros) y CHECK_EXPIRATION = OFF (una contrasena que
         vence dejaria a todas las terminales sin base de un dia para el otro).
      3. En la base crea el usuario del login y le da SOLO lo que la aplicacion necesita:
           - db_datareader y db_datawriter (leer y escribir las tablas; el borrado es logico, no hay DELETE de negocio);
           - EXECUTE sobre el esquema dbo (la aplicacion llama a procedimientos almacenados: sp_Login y
             sp_RegistrarIntentoLogin, de las migraciones).
         Nada mas: ni db_owner, ni db_ddladmin (no cambia el esquema: las migraciones las hace Retail.Cli en el servidor),
         ni db_backupoperator (las copias las hace la tarea programada y la pantalla de Respaldos en el servidor). Si el
         usuario tuviera alguno de esos roles de una ejecucion anterior o de una copia restaurada, se los quita.

    La contrasena se pide dos veces sin mostrarla (Read-Host -AsSecureString) y nunca se imprime ni se guarda: despues se
    la cifra en cada PC con configurar-terminal.ps1. Es idempotente: volver a ejecutarlo deja todo igual (y repara un usuario
    "huerfano" despues de restaurar una copia).

    Los permisos del usuario viven DENTRO de la base: si se restaura una copia tomada antes de ejecutar este script, hay que
    volver a ejecutarlo (como registrar-tarea-backup.ps1).

    Requisitos: ejecutar como administrador de Windows, en la PC servidor, con un usuario que sea sysadmin de la instancia, y
    con la base ya creada y migrada (Retail.Cli migrar). Con -WhatIf se muestra cada accion sin ejecutarla.

.PARAMETER Instancia
    Instancia de SQL Server. Por defecto sale de la configuracion de la aplicacion (appsettings.json junto al script y
    %ProgramData%\Retail\appsettings.local.json); si no figura, .\SQLEXPRESS.

.PARAMETER BaseDeDatos
    Nombre de la base de Retail POS. Por defecto sale de la misma configuracion; si no figura, RetailDb.

.PARAMETER NombreLogin
    Nombre del login. Por defecto retail_app (el que usan los scripts de las terminales).

.PARAMETER ReiniciarServicio
    Si se cambio la autenticacion a modo mixto, reinicia el servicio de SQL Server para que rija (corta las conexiones).
    Sin este parametro el script solo avisa que hay que reiniciarlo.

.EXAMPLE
    .\crear-login-aplicacion.ps1

    Crea el login retail_app con la configuracion de la aplicacion y pide la contrasena.

.EXAMPLE
    .\crear-login-aplicacion.ps1 -ReiniciarServicio

    Igual, y reinicia SQL Server si hubo que activar la autenticacion mixta.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$Instancia,

    [ValidatePattern('^[A-Za-z0-9_\-]+$')]
    [string]$BaseDeDatos,

    [ValidatePattern('^[A-Za-z0-9_]+$')]
    [string]$NombreLogin = "retail_app",

    [switch]$ReiniciarServicio
)

$ErrorActionPreference = "Stop"

Import-Module (Join-Path $PSScriptRoot "RetailPermisos.psm1") -Force

# Roles que el usuario de la aplicacion NO debe tener (el script se los quita si los encuentra).
$RolesProhibidos = @("db_owner", "db_ddladmin", "db_backupoperator", "db_securityadmin", "db_accessadmin")

function Test-Administrador
{
    $identidad = [Security.Principal.WindowsIdentity]::GetCurrent()
    return ([Security.Principal.WindowsPrincipal]$identidad).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-Delimitado([string]$Identificador)
{
    return "[" + $Identificador.Replace("]", "]]") + "]"
}

function Invoke-Sql([string]$Base, [string]$Sql, [bool]$Escalar = $false, [hashtable]$Parametros = @{})
{
    $cadena = "Server=$Instancia;Database=$Base;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=15"
    $conexion = New-Object System.Data.SqlClient.SqlConnection $cadena
    try
    {
        $conexion.Open()
        $comando = $conexion.CreateCommand()
        $comando.CommandText = $Sql
        foreach ($nombre in $Parametros.Keys)
        {
            [void]$comando.Parameters.AddWithValue($nombre, $Parametros[$nombre])
        }

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

function Read-PasswordDosVeces
{
    $primera = Read-Host -AsSecureString "Contrasena del login '$NombreLogin'"
    $segunda = Read-Host -AsSecureString "Repita la contrasena"

    $textoPrimera = ConvertTo-TextoPlano $primera
    $textoSegunda = ConvertTo-TextoPlano $segunda
    if ([string]::IsNullOrEmpty($textoPrimera))
    {
        throw "La contrasena no puede estar vacia."
    }

    if ($textoPrimera -cne $textoSegunda)
    {
        throw "Las dos contrasenas no coinciden. Vuelva a ejecutar el script."
    }

    return $textoPrimera
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

# La configuracion se busca junto al script (appsettings.json de la aplicacion) y en la carpeta local de esta PC.
$configuracion = Read-ConfiguracionDeCli $PSScriptRoot
if (-not $Instancia)
{
    $Instancia = if ($configuracion.Instancia) { $configuracion.Instancia } else { ".\SQLEXPRESS" }
}

if (-not $BaseDeDatos)
{
    $BaseDeDatos = if ($configuracion.BaseDeDatos) { $configuracion.BaseDeDatos } else { "RetailDb" }
}

if (-not (Test-InstanciaValida $Instancia))
{
    throw "La instancia '$Instancia' no es valida: solo se admiten letras, numeros y . _ - \ , : ( ) (sin punto y coma, signo igual, comillas ni espacios)."
}

# --- Solo en la PC servidor (misma comprobacion que registrar-tarea-backup.ps1) ---------------------------------------------

try
{
    $equipoDelMotor = Invoke-Sql "master" "SELECT CAST(SERVERPROPERTY('ComputerNamePhysicalNetBIOS') AS nvarchar(128));" $true
    $esSysadmin = Invoke-Sql "master" "SELECT IS_SRVROLEMEMBER('sysadmin');" $true
    $soloWindows = Invoke-Sql "master" "SELECT CAST(SERVERPROPERTY('IsIntegratedSecurityOnly') AS int);" $true
    $nombreInstancia = Invoke-Sql "master" "SELECT CAST(SERVERPROPERTY('InstanceName') AS nvarchar(128));" $true
    $existeBase = Invoke-Sql "master" "SELECT DB_ID(@base);" $true @{ "@base" = $BaseDeDatos }
}
catch
{
    throw "No se pudo consultar la instancia '$Instancia': $($_.Exception.Message)"
}

if ($equipoDelMotor -is [DBNull] -or [string]::IsNullOrWhiteSpace([string]$equipoDelMotor))
{
    throw "SQL Server no informo en que equipo corre la instancia '$Instancia': no se puede comprobar que esta sea la PC servidor."
}

if ($equipoDelMotor -ine $env:COMPUTERNAME)
{
    throw "Este script se ejecuta solo en la PC servidor ('$equipoDelMotor'). En las terminales use configurar-terminal.ps1."
}

if ($esSysadmin -ne 1)
{
    throw "El usuario actual no es sysadmin de '$Instancia': no puede crear el login ni dar permisos. Ejecute el script con la cuenta que instalo SQL Server."
}

if ($existeBase -is [DBNull] -or $null -eq $existeBase)
{
    throw "La base '$BaseDeDatos' no existe en '$Instancia'. Cree y migre la base primero (Retail.Cli migrar) y vuelva a ejecutar este script."
}

Write-Host "Instancia:  $Instancia"
Write-Host "Base:       $BaseDeDatos"
Write-Host "Login:      $NombreLogin"
Write-Host ""

# --- 1. Autenticacion mixta -------------------------------------------------------------------------------------------

$hayQueReiniciar = $false
if ($soloWindows -eq 1)
{
    if ($PSCmdlet.ShouldProcess($Instancia, "Activar la autenticacion mixta (LoginMode = 2); rige al reiniciar el servicio de SQL Server"))
    {
        Invoke-Sql "master" "EXEC master.dbo.xp_instance_regwrite N'HKEY_LOCAL_MACHINE', N'Software\Microsoft\MSSQLServer\MSSQLServer', N'LoginMode', REG_DWORD, 2;"
        Write-Host "Autenticacion mixta activada en la configuracion de la instancia."
        $hayQueReiniciar = $true
    }
}
else
{
    Write-Host "La instancia ya admite autenticacion mixta."
}

# --- 2. Login ---------------------------------------------------------------------------------------------------------

$existeLogin = Invoke-Sql "master" "SELECT SUSER_ID(@login);" $true @{ "@login" = $NombreLogin }
$existeLogin = -not ($existeLogin -is [DBNull] -or $null -eq $existeLogin)
$accionLogin = if ($existeLogin) { "Cambiar la contrasena del login $NombreLogin (y dejar CHECK_POLICY = ON)" } else { "Crear el login $NombreLogin con CHECK_POLICY = ON" }

if ($PSCmdlet.ShouldProcess("$Instancia (master)", $accionLogin))
{
    Write-Host "Configuracion del login '$NombreLogin'. Elija una contrasena larga: la va a pedir configurar-terminal.ps1 en cada PC."
    $password = Read-PasswordDosVeces

    # La contrasena viaja como parametro y se arma el DDL con QUOTENAME y comillas dobladas: nada del texto se interpreta como SQL.
    if ($existeLogin)
    {
        $ddl = "DECLARE @sql nvarchar(max) = N'ALTER LOGIN ' + QUOTENAME(@login) + N' WITH PASSWORD = N''' + REPLACE(@password, N'''', N'''''') + N''', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;'; EXEC (@sql);"
    }
    else
    {
        $ddl = "DECLARE @sql nvarchar(max) = N'CREATE LOGIN ' + QUOTENAME(@login) + N' WITH PASSWORD = N''' + REPLACE(@password, N'''', N'''''') + N''', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF, DEFAULT_DATABASE = ' + QUOTENAME(@base) + N';'; EXEC (@sql);"
    }

    try
    {
        Invoke-Sql "master" $ddl $false @{ "@login" = $NombreLogin; "@password" = $password; "@base" = $BaseDeDatos }
    }
    catch
    {
        throw "SQL Server rechazo la contrasena o el login: $($_.Exception.Message) (la contrasena debe cumplir la politica de Windows: longitud, mayusculas, minusculas y numeros)."
    }

    Write-Host "Login '$NombreLogin' listo."
}

# --- 3. Usuario y permisos minimos en la base -------------------------------------------------------------------------

$usuario = Get-Delimitado $NombreLogin

if ($PSCmdlet.ShouldProcess("$Instancia ($BaseDeDatos)", "Crear el usuario $NombreLogin si falta (o reenlazarlo al login si quedo huerfano)"))
{
    # El nombre ya se valido (solo letras, numeros y guion bajo), asi que puede ir directo en el DDL.
    Invoke-Sql $BaseDeDatos "IF DATABASE_PRINCIPAL_ID(N'$NombreLogin') IS NULL CREATE USER $usuario FOR LOGIN $usuario; ELSE ALTER USER $usuario WITH LOGIN = $usuario;"
}

$permisos = @(
    @{ Sql = "ALTER ROLE db_datareader ADD MEMBER $usuario;"; Motivo = "leer las tablas" },
    @{ Sql = "ALTER ROLE db_datawriter ADD MEMBER $usuario;"; Motivo = "escribir las tablas (el borrado de negocio es logico)" },
    @{ Sql = "GRANT EXECUTE ON SCHEMA::dbo TO $usuario;"; Motivo = "ejecutar los procedimientos almacenados (sp_Login, sp_RegistrarIntentoLogin)" }
)

foreach ($permiso in $permisos)
{
    if ($PSCmdlet.ShouldProcess("$Instancia ($BaseDeDatos)", "$($permiso.Sql)  -- $($permiso.Motivo)"))
    {
        Invoke-Sql $BaseDeDatos $permiso.Sql
    }
}

foreach ($rol in $RolesProhibidos)
{
    $sql = "IF DATABASE_PRINCIPAL_ID(N'$NombreLogin') IS NOT NULL AND IS_ROLEMEMBER(N'$rol', N'$NombreLogin') = 1 ALTER ROLE $rol DROP MEMBER $usuario;"
    if ($PSCmdlet.ShouldProcess("$Instancia ($BaseDeDatos)", "Quitar $NombreLogin del rol $rol si lo tuviera"))
    {
        Invoke-Sql $BaseDeDatos $sql
    }
}

# --- 4. Reinicio del servicio -----------------------------------------------------------------------------------------

if ($hayQueReiniciar -and -not $vistaPrevia)
{
    $nombreServicio = if ($nombreInstancia -is [DBNull] -or [string]::IsNullOrWhiteSpace([string]$nombreInstancia)) { "MSSQLSERVER" } else { "MSSQL`$$nombreInstancia" }

    if ($ReiniciarServicio)
    {
        if ($PSCmdlet.ShouldProcess($nombreServicio, "Reiniciar el servicio de SQL Server (corta las conexiones abiertas)"))
        {
            Restart-Service -Name $nombreServicio -Force
            Write-Host "Servicio '$nombreServicio' reiniciado: la autenticacion mixta ya rige."
            $hayQueReiniciar = $false
        }
    }
}

# --- Resumen ----------------------------------------------------------------------------------------------------------

if ($vistaPrevia)
{
    Write-Host ""
    Write-Host "Vista previa (-WhatIf): no se cambio nada."
    return
}

Write-Host ""
Write-Host "Resumen"
Write-Host "  - Login '$NombreLogin' en $Instancia, con acceso a '$BaseDeDatos' solo para leer, escribir y ejecutar procedimientos."
Write-Host "  - Sin permisos de administracion: no cambia el esquema, no crea bases ni hace copias (eso queda en el servidor)."
if ($hayQueReiniciar)
{
    Write-Host ""
    Write-Host "ATENCION: la autenticacion mixta rige recien cuando se reinicia el servicio de SQL Server. Hasta entonces el"
    Write-Host "login '$NombreLogin' existe pero no se puede usar. Reinicielo fuera del horario de venta (corta las conexiones):"
    Write-Host "  - con este script:  .\crear-login-aplicacion.ps1 -ReiniciarServicio   (repite todo sin cambios)"
    Write-Host "  - o desde el Administrador de servicios de Windows, o:  Restart-Service -Name 'MSSQL`$NOMBREDEINSTANCIA' -Force"
}

Write-Host ""
Write-Host "Siguiente paso: en cada PC (tambien en esta) ejecutar configurar-terminal.ps1 como administrador con esa contrasena."
Write-Host "Si restaura una copia tomada antes de este script, vuelva a ejecutarlo (los permisos viven dentro de la base)."
