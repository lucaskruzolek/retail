using FluentValidation;
using Retail.Application.DTOs.Terminales;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Common;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Casos de uso de la identidad de terminal (MT-1): verificar si esta PC está activada y activarla, ya sea como una
/// terminal nueva o reemplazando el equipo de una existente. La activación la hace un Gerente ya autenticado; el
/// servicio vuelve a leerlo de la base para autorizarlo (D4).
/// </summary>
public class TerminalService : ITerminalService
{
    private const string MensajeSoloGerente = "Solo un Gerente puede activar una terminal.";

    private readonly IRepository<Terminal> _terminales;
    private readonly IRepository<Usuario> _usuarios;
    private readonly ITerminalLocal _terminalLocal;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<NuevaTerminalDto> _nuevaValidator;

    public TerminalService(
        IRepository<Terminal> terminales,
        IRepository<Usuario> usuarios,
        ITerminalLocal terminalLocal,
        IUnitOfWork unitOfWork,
        IValidator<NuevaTerminalDto> nuevaValidator)
    {
        _terminales = terminales ?? throw new ArgumentNullException(nameof(terminales));
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
        _terminalLocal = terminalLocal ?? throw new ArgumentNullException(nameof(terminalLocal));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _nuevaValidator = nuevaValidator ?? throw new ArgumentNullException(nameof(nuevaValidator));
    }

    public async Task<EstadoTerminalDto> VerificarAsync(CancellationToken cancellationToken = default)
    {
        if (_terminalLocal.IdTerminal is not int idTerminal)
        {
            return new EstadoTerminalDto { Estado = EstadoTerminalEnum.NoActivada };
        }

        // GetByIdAsync respeta el filtro de borrado lógico: una terminal dada de baja equivale a una inexistente.
        var terminal = await _terminales.GetByIdAsync(idTerminal, cancellationToken);
        if (terminal is null)
        {
            return new EstadoTerminalDto { Estado = EstadoTerminalEnum.Inexistente };
        }

        var coincide = string.Equals(terminal.NombreEquipo, _terminalLocal.NombreEquipo, StringComparison.OrdinalIgnoreCase);

        return new EstadoTerminalDto
        {
            Estado = coincide ? EstadoTerminalEnum.Activa : EstadoTerminalEnum.EquipoDistinto,
            Terminal = Mapear(terminal)
        };
    }

    public async Task<IReadOnlyList<TerminalDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var terminales = await _terminales.ListAllAsync(cancellationToken);

        return terminales
            .OrderBy(t => t.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .Select(Mapear)
            .ToList();
    }

    public async Task<TerminalDto> ActivarNuevaAsync(int idActor, string nombre, CancellationToken cancellationToken = default)
    {
        await AutorizacionGerente.ExigirAsync(_usuarios, idActor, MensajeSoloGerente, cancellationToken);
        await _nuevaValidator.ValidateAndThrowAsync(new NuevaTerminalDto { Nombre = nombre }, cancellationToken);

        var nombreNormalizado = ReglasTexto.NormalizarEspacios(nombre);
        var terminal = Terminal.Crear(nombre, _terminalLocal.NombreEquipo);

        try
        {
            await _terminales.AddAsync(terminal, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (RestriccionDeUnicidadException ex)
        {
            // El índice único filtrado por nombre es la garantía final, también frente a dos terminales activándose a la vez.
            _unitOfWork.DescartarCambios();
            throw new DomainException($"Ya existe una terminal llamada «{nombreNormalizado}».", ex);
        }

        await _terminalLocal.GuardarAsync(terminal.Id, cancellationToken);

        return Mapear(terminal);
    }

    public async Task<TerminalDto> ActivarReemplazoAsync(int idActor, int idTerminal, CancellationToken cancellationToken = default)
    {
        await AutorizacionGerente.ExigirAsync(_usuarios, idActor, MensajeSoloGerente, cancellationToken);

        var terminal = await _terminales.GetByIdAsync(idTerminal, cancellationToken)
            ?? throw new DomainException($"No se encontró la terminal {idTerminal}.");

        terminal.ReemplazarEquipo(_terminalLocal.NombreEquipo);

        await _terminales.UpdateAsync(terminal, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _terminalLocal.GuardarAsync(terminal.Id, cancellationToken);

        return Mapear(terminal);
    }

    private static TerminalDto Mapear(Terminal terminal)
    {
        return new TerminalDto
        {
            Id = terminal.Id,
            Nombre = terminal.Nombre,
            NombreEquipo = terminal.NombreEquipo
        };
    }
}
