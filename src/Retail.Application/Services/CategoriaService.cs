using FluentValidation;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Common;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Casos de uso de administración de categorías (Track 6.5, RF-04). Toda escritura exige un actor con autoridad (D-5);
/// el listado lo consulta cualquier usuario (D-21).
/// </summary>
public class CategoriaService : ICategoriaService
{
    private readonly IRepository<Categoria> _repository;
    private readonly IRepository<Usuario> _usuarios;
    private readonly IClasificacionQueryService _consultas;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CrearCategoriaDto> _crearValidator;
    private readonly IValidator<RenombrarCategoriaDto> _renombrarValidator;

    public CategoriaService(
        IRepository<Categoria> repository,
        IRepository<Usuario> usuarios,
        IClasificacionQueryService consultas,
        IUnitOfWork unitOfWork,
        IValidator<CrearCategoriaDto> crearValidator,
        IValidator<RenombrarCategoriaDto> renombrarValidator)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
        _consultas = consultas ?? throw new ArgumentNullException(nameof(consultas));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _crearValidator = crearValidator ?? throw new ArgumentNullException(nameof(crearValidator));
        _renombrarValidator = renombrarValidator ?? throw new ArgumentNullException(nameof(renombrarValidator));
    }

    public async Task<IReadOnlyList<CategoriaDto>> ListarAsync(string? filtro, CancellationToken cancellationToken = default)
    {
        return await _consultas.ListarCategoriasAsync(filtro, cancellationToken);
    }

    public async Task<CategoriaDto> CrearAsync(int idActor, CrearCategoriaDto dto, CancellationToken cancellationToken = default)
    {
        // Primero la autorización: quien no tiene permiso no recibe detalles de validación.
        await AutorizacionClasificacion.ExigirAsync(_usuarios, idActor, cancellationToken);
        await _crearValidator.ValidateAndThrowAsync(dto, cancellationToken);

        // La comparación sin distinguir mayúsculas ni acentos la resuelve la colación de SQL Server (R-3) y el
        // índice único filtrado es la garantía final ante dos altas simultáneas (D-4).
        string nombre = ReglasTexto.NormalizarNombreClasificacion(dto.Nombre);
        var existentes = await _repository.FindAsync(
            x => x.NombreCategoria == nombre,
            includeDeleted: false,
            cancellationToken);

        if (existentes.Count > 0)
        {
            throw new DomainException($"Ya existe una categoría activa con el nombre '{nombre}'.");
        }

        var entidad = Categoria.Crear(nombre);

        await _repository.AddAsync(entidad, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CategoriaDto
        {
            IdCategoria = entidad.Id,
            NombreCategoria = entidad.NombreCategoria,
            CantidadEnUso = 0
        };
    }

    public async Task RenombrarAsync(int idActor, RenombrarCategoriaDto dto, CancellationToken cancellationToken = default)
    {
        await AutorizacionClasificacion.ExigirAsync(_usuarios, idActor, cancellationToken);
        await _renombrarValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var entidad = await _repository.GetByIdAsync(dto.IdCategoria, includeDeleted: false, cancellationToken);
        if (entidad == null)
        {
            throw new DomainException($"No se encontró la categoría con ID {dto.IdCategoria}.");
        }

        // Se excluye el propio registro: renombrar al mismo nombre, o cambiar solo mayúsculas o acentos del
        // propio nombre, está permitido (D-8). El índice único filtrado respalda la regla (D-4).
        string nombre = ReglasTexto.NormalizarNombreClasificacion(dto.Nombre);
        var existentes = await _repository.FindAsync(
            x => x.NombreCategoria == nombre && x.Id != dto.IdCategoria,
            includeDeleted: false,
            cancellationToken);

        if (existentes.Count > 0)
        {
            throw new DomainException($"Ya existe una categoría activa con el nombre '{nombre}'.");
        }

        entidad.Renombrar(nombre);

        await _repository.UpdateAsync(entidad, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task BajaAsync(int idActor, int idCategoria, CancellationToken cancellationToken = default)
    {
        await AutorizacionClasificacion.ExigirAsync(_usuarios, idActor, cancellationToken);

        var entidad = await _repository.GetByIdAsync(idCategoria, includeDeleted: false, cancellationToken);
        if (entidad == null)
        {
            throw new DomainException($"No se encontró la categoría con ID {idCategoria}.");
        }

        // Invariante entre agregados: Categoria y sus dependientes son raíces distintas, por eso la regla "no se da de
        // baja lo que está en uso" (D-1) se resuelve acá y no en la entidad (sección 4). Entre el conteo y el
        // guardado otro usuario podría asignar la categoría a un artículo: ese riesgo se acepta (R-5).
        int enUso = await _consultas.ContarArticulosPorCategoriaAsync(idCategoria, cancellationToken);
        if (enUso > 0)
        {
            throw new ClasificacionEnUsoException("la categoría", entidad.NombreCategoria, enUso, "artículos");
        }

        entidad.MarkAsDeleted();

        await _repository.UpdateAsync(entidad, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
