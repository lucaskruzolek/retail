using FluentValidation;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Common;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Casos de uso de la tabla de términos (Track 6.6, M-16, M-27). Lo usan la pestaña de términos y la decisión en
/// bloque de la incorporación, que registra el término nuevo con la categoría que elige el usuario.
/// </summary>
public class TerminoClasificacionService : ITerminoClasificacionService
{
    private readonly IRepository<TerminoClasificacion> _repository;
    private readonly IRepository<Categoria> _categorias;
    private readonly IRepository<Usuario> _usuarios;
    private readonly IClasificacionQueryService _consultas;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<RegistrarTerminoDto> _registrarValidator;

    public TerminoClasificacionService(
        IRepository<TerminoClasificacion> repository,
        IRepository<Categoria> categorias,
        IRepository<Usuario> usuarios,
        IClasificacionQueryService consultas,
        IUnitOfWork unitOfWork,
        IValidator<RegistrarTerminoDto> registrarValidator)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _categorias = categorias ?? throw new ArgumentNullException(nameof(categorias));
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
        _consultas = consultas ?? throw new ArgumentNullException(nameof(consultas));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _registrarValidator = registrarValidator ?? throw new ArgumentNullException(nameof(registrarValidator));
    }

    public async Task<IReadOnlyList<TerminoDto>> ListarAsync(
        string? filtro,
        int? idCategoria,
        bool soloAmbiguos,
        CancellationToken cancellationToken = default)
    {
        return await _consultas.ListarTerminosAsync(filtro, idCategoria, soloAmbiguos, cancellationToken);
    }

    public async Task RegistrarAsync(int idActor, RegistrarTerminoDto dto, CancellationToken cancellationToken = default)
    {
        // Primero la autorización: quien no tiene permiso no recibe detalles de validación.
        await AutorizacionClasificacion.ExigirAsync(_usuarios, idActor, cancellationToken);
        await _registrarValidator.ValidateAndThrowAsync(dto, cancellationToken);

        // D-23: la categoría de destino debe existir y estar activa; da un mensaje claro en lugar de un error de la FK.
        if (dto.IdCategoria.HasValue)
        {
            var categoria = await _categorias.GetByIdAsync(dto.IdCategoria.Value, includeDeleted: false, cancellationToken);
            if (categoria is null)
            {
                throw new DomainException($"No se encontró la categoría con ID {dto.IdCategoria.Value}.");
            }
        }

        // Unicidad entre agregados: cada término es su propia raíz, así que "un término en una sola categoría" se
        // resuelve buscándolo antes de crear. Si existe, se reasigna (como la equivalencia, M-32).
        string termino = ReglasClasificacion.ExigirTermino(dto.Termino);
        var existentes = await _repository.FindAsync(x => x.Termino == termino, includeDeleted: false, cancellationToken);
        var entidad = existentes.Count > 0 ? existentes[0] : null;

        if (entidad is null)
        {
            entidad = dto.IdCategoria.HasValue
                ? TerminoClasificacion.CrearConCategoria(termino, dto.IdCategoria.Value)
                : TerminoClasificacion.CrearAmbiguo(termino);
            await _repository.AddAsync(entidad, cancellationToken);
        }
        else
        {
            if (dto.IdCategoria.HasValue)
            {
                entidad.AsignarCategoria(dto.IdCategoria.Value);
            }
            else
            {
                entidad.MarcarAmbiguo();
            }

            await _repository.UpdateAsync(entidad, cancellationToken);
        }

        await PersistenciaDiccionarios.GuardarAsync(
            _unitOfWork,
            $"Otro usuario registró el término '{termino}' al mismo tiempo. Actualice la lista y vuelva a intentarlo.",
            cancellationToken);
    }

    public async Task BajaAsync(int idActor, int idTermino, CancellationToken cancellationToken = default)
    {
        await AutorizacionClasificacion.ExigirAsync(_usuarios, idActor, cancellationToken);

        var entidad = await _repository.GetByIdAsync(idTermino, includeDeleted: false, cancellationToken);
        if (entidad is null)
        {
            throw new DomainException($"No se encontró el término con ID {idTermino}.");
        }

        entidad.MarkAsDeleted();

        await _repository.UpdateAsync(entidad, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
