<#
.SYNOPSIS
    Configura una PC (terminal o servidor) para conectarse a la base de Retail POS en red (MT-7, MT-10). Se ejecuta en CADA
    PC, como administrador.

.DESCRIPTION
    Escribe la configuracion propia de esta PC en %ProgramData%\Retail\appsettings.local.json (o en RETAIL_CONFIG_DIR),
    fuera de la carpeta de la aplicacion: actualizar la aplicacion (reemplazar los archivos del ZIP) nunca la toca.

      1. Crea la carpeta si falta y le da permiso de MODIFICAR al grupo local Usuarios SOLO a esa carpeta (la aplicacion
         corre con la cuenta del cajero y escribe ahi terminal.json, la identidad de la PC). Administradores y SYSTEM
         conservan control total; no se tocan permisos de ninguna otra carpeta.
      2. Escribe (o actualiza) appsettings.local.json en UTF-8 sin BOM, con sangria de 2 espacios:
           - ConnectionStrings:DefaultConnection: servidor y base, con el login "retail_app" y SIN contrasena;
           - BaseDeDatos:PasswordCifrada: la contrasena cifrada con DPAPI de equipo (base64). Solo esta PC la puede
             descifrar: copiar el archivo a otra PC no sirve (hay que volver a ejecutar este script en cada una);
           - ArcaSettings:BaseUrl: la direccion del servicio de facturacion (arcasdk) en la PC servidor;
           - con -ConexionMantenimiento: ConnectionStrings:Mantenimiento (ver abajo).
         Si el archivo ya existe se cambian solo esas claves y se conserva el resto.
      3. Con -ProbarConexion abre una conexion real con ese login para verificar servidor, red, login y contrasena.

    No registra ninguna tarea programada (eso es solo de la PC servidor: registrar-tarea-backup.ps1). La contrasena se pide
    sin mostrarla (Read-Host -AsSecureString) y no se imprime; es la que se eligio en crear-login-aplicacion.ps1.

    En la PC servidor se usa el mismo script, con -ConexionMantenimiento: agrega ConnectionStrings:Mantenimiento, una
    conexion LOCAL con autenticacion de Windows para las operaciones de administracion (copias bajo demanda, restauracion,
    migraciones), que el login de la aplicacion no puede hacer a proposito. Solo se acepta en la PC servidor.

.PARAMETER Servidor
    Servidor SQL, con instancia si la hay: por ejemplo PCSERVIDOR\SQLEXPRESS (en red) o .\SQLEXPRESS (en el propio servidor).

.PARAMETER BaseDeDatos
    Nombre de la base. Por defecto RetailDb.

.PARAMETER UrlFiscal
    Direccion del servicio de facturacion (arcasdk). Por defecto http://{nombre de la PC servidor, sin la instancia}:8080
    (en el propio servidor, http://localhost:8080).

.PARAMETER ConexionMantenimiento
    Solo en la PC servidor: agrega ConnectionStrings:Mantenimiento con Server={instancia local};Trusted_Connection=True;
    TrustServerCertificate=True.

.PARAMETER ProbarConexion
    Despues de escribir el archivo, abre una conexion con el login de la aplicacion para comprobar que funciona.

.PARAMETER Password
    Contrasena del login como SecureString, para automatizar. Si se omite, se pide por consola.

.EXAMPLE
    .\configurar-terminal.ps1 -Servidor "PCSERVIDOR\SQLEXPRESS" -ProbarConexion

    Configura una terminal, pide la contrasena y prueba la conexion.

.EXAMPLE
    .\configurar-terminal.ps1 -Servidor ".\SQLEXPRESS" -ConexionMantenimiento

    Configura la PC servidor misma, con la conexion de mantenimiento local.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Servidor,

    [ValidatePattern('^[A-Za-z0-9_\-]+$')]
    [string]$BaseDeDatos = "RetailDb",

    [string]$UrlFiscal,

    [switch]$ConexionMantenimiento,

    [switch]$ProbarConexion,

    [System.Security.SecureString]$Password
)

$ErrorActionPreference = "Stop"

Import-Module (Join-Path $PSScriptRoot "RetailPermisos.psm1") -Force

$LoginAplicacion = "retail_app"
$SidUsuarios = "S-1-5-32-545"

function Test-Administrador
{
    $identidad = [Security.Principal.WindowsIdentity]::GetCurrent()
    return ([Security.Principal.WindowsPrincipal]$identidad).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Con RETAIL_CONFIG_DIR (desarrollo y pruebas) la carpeta es otra, elegida por quien ejecuta: no hace falta ser administrador.
if (-not $env:RETAIL_CONFIG_DIR -and -not (Test-Administrador))
{
    throw "Este script debe ejecutarse como administrador: abra PowerShell con 'Ejecutar como administrador' y vuelva a intentarlo."
}

if (-not (Test-InstanciaValida $Servidor))
{
    throw "El servidor '$Servidor' no es valido: solo se admiten letras, numeros y . _ - \ , : ( ) (sin punto y coma, signo igual, comillas ni espacios). Ejemplo: PCSERVIDOR\SQLEXPRESS"
}

# Nombre de la PC y de la instancia: "PCSERVIDOR\SQLEXPRESS" -> PCSERVIDOR y SQLEXPRESS; "PCSERVIDOR,1433" -> PCSERVIDOR.
$partes = $Servidor.Split('\')
$equipoServidor = $partes[0].Split(',')[0].Trim()
$nombreInstancia = if ($partes.Count -gt 1) { $partes[1] } else { $null }
$esEquipoLocal = $equipoServidor -in @(".", "(local)", "localhost", "127.0.0.1", $env:COMPUTERNAME)

if ($ConexionMantenimiento -and -not $esEquipoLocal)
{
    throw "-ConexionMantenimiento solo se usa en la PC servidor: '$equipoServidor' no es esta PC ('$env:COMPUTERNAME'). En las terminales no se configura."
}

if (-not $UrlFiscal)
{
    $hostFiscal = if ($esEquipoLocal -and $equipoServidor -in @(".", "(local)", "127.0.0.1")) { "localhost" } else { $equipoServidor }
    $UrlFiscal = "http://${hostFiscal}:8080"
}

$uri = $null
if (-not [Uri]::TryCreate($UrlFiscal, [UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -notin @("http", "https"))
{
    throw "La direccion fiscal '$UrlFiscal' no es valida: debe ser una URL http o https, por ejemplo http://PCSERVIDOR:8080"
}

if (-not $Password)
{
    $Password = Read-Host -AsSecureString "Contrasena del login '$LoginAplicacion'"
}

$passwordTexto = ConvertTo-TextoPlano $Password
if ([string]::IsNullOrEmpty($passwordTexto))
{
    throw "La contrasena no puede estar vacia."
}

# --- Carpeta y permisos ---------------------------------------------------------------------------------------------------

$carpeta = Get-CarpetaConfiguracionLocal
$archivo = Join-Path $carpeta "appsettings.local.json"

Write-Host "Servidor:      $Servidor"
Write-Host "Base:          $BaseDeDatos"
Write-Host "Facturacion:   $UrlFiscal"
Write-Host "Archivo:       $archivo"
Write-Host ""

if (-not (Test-Path -LiteralPath $carpeta))
{
    [void](New-Item -ItemType Directory -Path $carpeta -Force)
}

# Administradores y SYSTEM: control total; el grupo local Usuarios: modificar (la aplicacion escribe terminal.json aca).
Set-PermisoDeCarpeta -Ruta $carpeta -CuentasModificar @($SidUsuarios)
Write-Host "Carpeta lista: Usuarios puede modificar solo esta carpeta."

# --- appsettings.local.json -----------------------------------------------------------------------------------------------

$configuracion = [ordered]@{}
if (Test-Path -LiteralPath $archivo -PathType Leaf)
{
    $existente = Get-Content -LiteralPath $archivo -Raw -Encoding UTF8
    if (-not [string]::IsNullOrWhiteSpace($existente))
    {
        $configuracion = ConvertFrom-JsonAHashtable ($existente | ConvertFrom-Json)
    }
}

function Set-Seccion($Raiz, [string]$Nombre)
{
    if (-not $Raiz.Contains($Nombre) -or $Raiz[$Nombre] -isnot [System.Collections.IDictionary])
    {
        $Raiz[$Nombre] = [ordered]@{}
    }

    return $Raiz[$Nombre]
}

$conexiones = Set-Seccion $configuracion "ConnectionStrings"
$conexiones["DefaultConnection"] = "Server=$Servidor;Database=$BaseDeDatos;User Id=$LoginAplicacion;TrustServerCertificate=True;MultipleActiveResultSets=true"

if ($ConexionMantenimiento)
{
    $instanciaLocal = if ($nombreInstancia) { ".\$nombreInstancia" } else { "." }
    $conexiones["Mantenimiento"] = "Server=$instanciaLocal;Trusted_Connection=True;TrustServerCertificate=True"
}

$baseDeDatosSeccion = Set-Seccion $configuracion "BaseDeDatos"
$baseDeDatosSeccion["PasswordCifrada"] = Protect-PasswordSql $passwordTexto

$arca = Set-Seccion $configuracion "ArcaSettings"
$arca["BaseUrl"] = $UrlFiscal

$json = ConvertTo-JsonLegible $configuracion
[System.IO.File]::WriteAllText($archivo, $json + [Environment]::NewLine, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Configuracion escrita en $archivo (la contrasena queda cifrada para esta PC)."

# La carpeta es modificable por Usuarios porque la aplicacion escribe ahi terminal.json, pero ESTE archivo no: quien
# pudiera editarlo podria apuntar la conexion a otro servidor. Se corta la herencia y Usuarios queda solo con lectura
# (la aplicacion lo lee con la cuenta del cajero). Con RETAIL_CONFIG_DIR (pruebas) no se toca: ahi el script corre sin
# privilegios y quitarse el permiso de escritura romperia la siguiente ejecucion.
if (-not $env:RETAIL_CONFIG_DIR)
{
    $infoArchivo = New-Object System.IO.FileInfo $archivo
    $aclArchivo = $infoArchivo.GetAccessControl([System.Security.AccessControl.AccessControlSections]::Access)
    $aclArchivo.SetAccessRuleProtection($true, $false)
    foreach ($regla in @($aclArchivo.Access))
    {
        [void]$aclArchivo.RemoveAccessRuleSpecific($regla)
    }

    $permitir = [System.Security.AccessControl.AccessControlType]::Allow
    $aclArchivo.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule(
        (New-Object System.Security.Principal.SecurityIdentifier "S-1-5-32-544"),
        [System.Security.AccessControl.FileSystemRights]::FullControl, $permitir)))
    $aclArchivo.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule(
        (New-Object System.Security.Principal.SecurityIdentifier "S-1-5-18"),
        [System.Security.AccessControl.FileSystemRights]::FullControl, $permitir)))
    $aclArchivo.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule(
        (New-Object System.Security.Principal.SecurityIdentifier $SidUsuarios),
        [System.Security.AccessControl.FileSystemRights]::ReadAndExecute, $permitir)))
    $infoArchivo.SetAccessControl($aclArchivo)
    Write-Host "appsettings.local.json: Usuarios solo puede leerlo; Administradores y SYSTEM, modificarlo."
}

# --- Prueba de conexion ---------------------------------------------------------------------------------------------------

$conexionOk = $true
if ($ProbarConexion)
{
    $constructor = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $conexiones["DefaultConnection"]
    $constructor["Password"] = $passwordTexto
    $constructor["Connect Timeout"] = 10

    $conexion = New-Object System.Data.SqlClient.SqlConnection $constructor.ConnectionString
    try
    {
        $conexion.Open()
        Write-Host "Prueba de conexion: correcta (servidor, red, login y contrasena)."
    }
    catch
    {
        $conexionOk = $false
        Write-Warning "La prueba de conexion fallo: $($_.Exception.Message)"
        Write-Warning "Revise el nombre del servidor, que TCP/IP y el puerto 1433 esten habilitados, que crear-login-aplicacion.ps1 se haya ejecutado (y SQL Server reiniciado) y que la contrasena sea la correcta."
    }
    finally
    {
        $conexion.Dispose()
    }
}

Write-Host ""
Write-Host "Resumen"
Write-Host "  - Esta PC se conecta a '$Servidor' (base '$BaseDeDatos') con el login '$LoginAplicacion'."
if ($ConexionMantenimiento)
{
    Write-Host "  - Conexion de mantenimiento local configurada: las copias bajo demanda y la restauracion funcionan en esta PC (el servidor)."
}

Write-Host "  - En el primer arranque, un Gerente activa esta PC como terminal."
Write-Host "  - Al actualizar la aplicacion este archivo se conserva: no hace falta repetir el script."

if (-not $conexionOk)
{
    exit 1
}
