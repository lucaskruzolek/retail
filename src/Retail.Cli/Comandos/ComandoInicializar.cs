using Microsoft.Extensions.Logging;
using Retail.Application.DTOs.Usuarios;
using Retail.Application.Interfaces.Services;
using Retail.Cli.Consola;
using Retail.Domain.Enums;

namespace Retail.Cli.Comandos;

/// <summary>
/// <c>inicializar</c>: crea la cuenta del primer Gerente (D-12). Reemplaza al usuario "admin" con contraseña fija
/// que sembraba la aplicación: ahora la contraseña la elige el titular frente al equipo.
/// </summary>
internal sealed class ComandoInicializar
{
    private readonly IEstadoSistemaService _estadoSistema;
    private readonly IAdministracionSistemaService _administracion;
    private readonly ILogger<ComandoInicializar> _logger;

    public ComandoInicializar(
        IEstadoSistemaService estadoSistema,
        IAdministracionSistemaService administracion,
        ILogger<ComandoInicializar> logger)
    {
        _estadoSistema = estadoSistema ?? throw new ArgumentNullException(nameof(estadoSistema));
        _administracion = administracion ?? throw new ArgumentNullException(nameof(administracion));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> EjecutarAsync()
    {
        var estado = await _estadoSistema.ObtenerEstadoAsync();

        if (!estado.Esquema.EstaAlDia)
        {
            Console.Error.WriteLine("La base no está al día. Ejecute primero: Retail.Cli migrar");
            return CodigoSalida.Fallo;
        }

        // El servicio también lo rechaza; se verifica antes para no pedir datos que no se van a usar.
        if (estado.ExisteGerenteActivo)
        {
            Console.Error.WriteLine(
                "Ya existe un Gerente activo. Las demás cuentas se crean desde la aplicación (módulo Usuarios).");
            return CodigoSalida.Fallo;
        }

        Console.WriteLine("Alta del primer Gerente. Esta cuenta podrá crear el resto de los usuarios desde la aplicación.");
        Console.WriteLine();

        var dto = new CrearUsuarioDto
        {
            NombreUsuario = EntradaConsola.LeerTexto("Nombre de usuario (login)"),
            Nombre = EntradaConsola.LeerTexto("Nombre"),
            Apellido = EntradaConsola.LeerTexto("Apellido"),
            EmailRecuperacion = EntradaConsola.LeerTexto("Email de recuperación"),
            Password = EntradaConsola.LeerSecretoConfirmado("Contraseña"),
            Rol = RolUsuarioEnum.Gerente
        };

        var gerente = await _administracion.CrearPrimerGerenteAsync(dto);

        _logger.LogWarning(
            "Primer Gerente '{Usuario}' creado desde Retail.Cli por {UsuarioWindows} en {Equipo}",
            gerente.NombreUsuario, Environment.UserName, Environment.MachineName);

        Console.WriteLine();
        Console.WriteLine($"Gerente '{gerente.NombreUsuario}' ({gerente.NombreCompleto}) creado. Ya puede iniciar sesión en Retail POS.");
        return CodigoSalida.Exito;
    }
}
