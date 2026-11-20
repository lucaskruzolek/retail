using FluentValidation;
using Retail.Application.DTOs.Sesiones;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Reglas de la sesión única por usuario y del mantenimiento programado. La atomicidad y el reloj viven en el puerto
/// <see cref="ISesionesActivas"/>; acá se validan los argumentos y se autoriza al Gerente (releído de la base).
/// </summary>
public class SesionService : ISesionService
{
    private const string MensajeSoloGerente = "Solo un Gerente puede administrar las sesiones.";

    private const string MensajeMantenimiento = "Solo un Gerente o un Administrador puede programar o cancelar un mantenimiento.";

    private readonly ISesionesActivas _sesiones;
    private readonly IRepository<Usuario> _usuarios;
    private readonly IValidator<ProgramarMantenimientoDto> _mantenimientoValidator;

    public SesionService(
        ISesionesActivas sesiones,
        IRepository<Usuario> usuarios,
        IValidator<ProgramarMantenimientoDto> mantenimientoValidator)
    {
        _sesiones = sesiones ?? throw new ArgumentNullException(nameof(sesiones));
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
        _mantenimientoValidator = mantenimientoValidator ?? throw new ArgumentNullException(nameof(mantenimientoValidator));
    }

    public Task<ResultadoInicioSesionDto> IniciarAsync(int idUsuario, int idTerminal, CancellationToken cancellationToken = default)
    {
        ValidarIds(idUsuario, idTerminal);
        return _sesiones.IniciarAsync(idUsuario, idTerminal, cancellationToken);
    }

    public Task<EstadoLatidoDto> RenovarAsync(int idUsuario, int idTerminal, CancellationToken cancellationToken = default)
    {
        ValidarIds(idUsuario, idTerminal);
        return _sesiones.RenovarAsync(idUsuario, idTerminal, cancellationToken);
    }

    public Task CerrarAsync(int idUsuario, int idTerminal, CancellationToken cancellationToken = default)
    {
        ValidarIds(idUsuario, idTerminal);
        return _sesiones.CerrarAsync(idUsuario, idTerminal, cancellationToken);
    }

    public async Task<IReadOnlyList<SesionActivaDto>> ListarAsync(int idActor, CancellationToken cancellationToken = default)
    {
        await AutorizacionGerente.ExigirAsync(_usuarios, idActor, MensajeSoloGerente, cancellationToken);

        return await _sesiones.ListarAsync(cancellationToken);
    }

    public async Task<bool> ForzarCierreAsync(int idActor, int idUsuario, CancellationToken cancellationToken = default)
    {
        await AutorizacionGerente.ExigirAsync(_usuarios, idActor, MensajeSoloGerente, cancellationToken);

        if (idUsuario == idActor)
        {
            throw new DomainException("No puede cerrar su propia sesión desde acá: se cierra al salir de la aplicación.");
        }

        return await _sesiones.ForzarCierreAsync(idUsuario, cancellationToken);
    }

    public async Task ProgramarMantenimientoAsync(
        int idActor,
        int minutosDesdeAhora,
        string? motivo,
        CancellationToken cancellationToken = default)
    {
        // El Administrador también puede: prepara el mantenimiento antes de la restauración que ejecuta con la
        // autorización de un Gerente (control dual, Track 6.3.1).
        await AutorizacionGerente.ExigirGerenteOAdministradorAsync(_usuarios, idActor, MensajeMantenimiento, cancellationToken);
        await _mantenimientoValidator.ValidateAndThrowAsync(
            new ProgramarMantenimientoDto { MinutosDesdeAhora = minutosDesdeAhora, Motivo = motivo },
            cancellationToken);

        var motivoNormalizado = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();

        await _sesiones.ProgramarMantenimientoAsync(minutosDesdeAhora, motivoNormalizado, idActor, cancellationToken);
    }

    public async Task CancelarMantenimientoAsync(int idActor, CancellationToken cancellationToken = default)
    {
        await AutorizacionGerente.ExigirGerenteOAdministradorAsync(_usuarios, idActor, MensajeMantenimiento, cancellationToken);

        await _sesiones.CancelarMantenimientoAsync(cancellationToken);
    }

    private static void ValidarIds(int idUsuario, int idTerminal)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(idUsuario);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(idTerminal);
    }
}
