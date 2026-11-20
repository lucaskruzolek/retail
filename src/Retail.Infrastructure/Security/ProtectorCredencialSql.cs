using System.Security.Cryptography;
using System.Text;

namespace Retail.Infrastructure.Security;

/// <summary>
/// Cifra y descifra la contraseña del login de SQL Server que guarda <c>appsettings.local.json</c> (MT-10).
/// </summary>
/// <remarks>
/// Usa DPAPI con alcance de equipo (<see cref="DataProtectionScope.LocalMachine"/>) y una entropía fija
/// (<see cref="Entropia"/>): cualquier cuenta de Windows de ESTA PC puede descifrarla (la aplicación corre con la cuenta
/// del cajero, y la tarea programada como SYSTEM), pero el texto cifrado copiado a otra PC no sirve. No es un secreto
/// frente a un administrador local: protege el archivo si se copia o se filtra, no la PC.
/// <para>
/// CONTRATO CON LOS SCRIPTS: <c>scripts/configurar-terminal.ps1</c> cifra con exactamente estos parámetros
/// (<c>ProtectedData.Protect(bytes UTF-8 de la contraseña, bytes UTF-8 de "Retail.POS.Sql", LocalMachine)</c>) y guarda
/// el resultado en base64 en <c>BaseDeDatos:PasswordCifrada</c>. Si se cambia algo acá, hay que cambiarlo allá.
/// </para>
/// </remarks>
public static class ProtectorCredencialSql
{
    /// <summary>Entropía adicional de DPAPI (texto, codificado en UTF-8). No es secreta: separa este uso de otros.</summary>
    public const string Entropia = "Retail.POS.Sql";

    /// <summary>Mensaje cuando el valor guardado no se puede descifrar en esta PC.</summary>
    public const string MensajeNoSePuedeDescifrar =
        "La contraseña de la base guardada en appsettings.local.json no se puede descifrar en esta PC: vuelva a configurarla con configurar-terminal.ps1.";

    private const string MensajeSoloWindows = "La protección de la contraseña de la base requiere Windows (DPAPI).";

    private static byte[] BytesDeEntropia => Encoding.UTF8.GetBytes(Entropia);

    /// <summary>
    /// Cifra la contraseña y devuelve el resultado en base64.
    /// </summary>
    public static string Cifrar(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(MensajeSoloWindows);
        }

        var cifrado = ProtectedData.Protect(Encoding.UTF8.GetBytes(password), BytesDeEntropia, DataProtectionScope.LocalMachine);
        return Convert.ToBase64String(cifrado);
    }

    /// <summary>
    /// Descifra un valor producido por <see cref="Cifrar"/>. Si no es base64 o no corresponde a esta PC o a esta
    /// entropía, lanza <see cref="InvalidOperationException"/> con <see cref="MensajeNoSePuedeDescifrar"/>.
    /// </summary>
    public static string Descifrar(string base64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base64);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(MensajeSoloWindows);
        }

        try
        {
            var plano = ProtectedData.Unprotect(Convert.FromBase64String(base64.Trim()), BytesDeEntropia, DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(plano);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            throw new InvalidOperationException(MensajeNoSePuedeDescifrar, ex);
        }
    }
}
