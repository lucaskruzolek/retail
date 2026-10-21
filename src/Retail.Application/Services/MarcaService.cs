using FluentValidation;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Common;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Casos de uso de administración de marcas (Track 6.5, RF-04). Toda escritura exige un actor con autoridad (D-5);
/// el listado lo consulta cualquier usuario (D-21).
/// </summary>
public class MarcaService : IMarcaService
{
    private readonly IRepository<Marca> _repository;
    private readonly IRepository<Usuario> _usuarios;
    private readonly IClasificacionQueryService _consultas;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CrearMarcaDto> _crearValidator;
    private readonly IValidator<RenombrarMarcaDto> _renombrarValidator;

    public MarcaService(
        IRepository<Marca> repository,
        IRepository<Usuario> usuarios,
        IClasificacionQueryService consultas,
        IUnitOfWork unitOfWork,
        IValidator<CrearMarcaDto> crearValidator,
        IValidator<RenombrarMarcaDto> renombrarValidator)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
        _consultas = consultas ?? throw new ArgumentNullException(nameof(consultas));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _crearValidator = crearValidator ?? throw new ArgumentNullException(nameof(crearValidator));
        _renombrarValidator = renombrarValidator ?? throw new ArgumentNullException(nameof(renombrarValidator));
    }

    public async Task<IReadOnlyList<MarcaDto>> ListarAsync(string? filtro, CancellationToken cancellationToken = default)
    {
        return await _consultas.ListarMarcasAsync(filtro, cancellationToken);
    }

    public async Task<MarcaDto> CrearAsync(int idActor, CrearMarcaDto dto, CancellationToken cancellationToken = default)
    {
        // Primero la autorización: quien no tiene permiso no recibe detalles de validación.
        await AutorizacionClasificacion.ExigirAsync(_usuarios, idActor, cancellationToken);
        await _crearValidator.ValidateAndThrowAsync(dto, cancellationToken);

        // La comparación sin distinguir mayúsculas ni acentos la resuelve la colación de SQL Server (R-3) y el
        // índice único filtrado es la garantía final ante dos altas simultáneas (D-4).
        string nombre = ReglasTexto.NormalizarNombreClasificacion(dto.Nombre);
        var existentes = await _repository.FindAsync(
            x => x.NombreMarca == nombre,
            includeDeleted: false,
            cancellationToken);

        if (existentes.Count > 0)
        {
            throw new DomainException($"Ya existe una marca activa con el nombre '{nombre}'.");
        }

        var entidad = Marca.Crear(nombre);

        await _repository.AddAsync(entidad, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new MarcaDto
        {
            IdMarca = entidad.Id,
            NombreMarca = entidad.NombreMarca,
            CantidadEnUso = 0
        };
    }

    public async Task RenombrarAsync(int idActor, RenombrarMarcaDto dto, CancellationToken cancellationToken = default)
    {
        await AutorizacionClasificacion.ExigirAsync(_usuarios, idActor, cancellationToken);
        await _renombrarValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var entidad = await _repository.GetByIdAsync(dto.IdMarca, includeDeleted: false, cancellationToken);
        if (entidad == null)
        {
            throw new DomainException($"No se encontró la marca con ID {dto.IdMarca}.");
        }

        // Se excluye el propio registro: renombrar al mismo nombre, o cambiar solo mayúsculas o acentos del
        // propio nombre, está permitido (D-8). El índice único filtrado respalda la regla (D-4).
        string nombre = ReglasTexto.NormalizarNombreClasificacion(dto.Nombre);
        var existentes = await _repository.FindAsync(
            x => x.NombreMarca == nombre && x.Id != dto.IdMarca,
            includeDeleted: false,
            cancellationToken);

        if (existentes.Count > 0)
        {
            throw new DomainException($"Ya existe una marca activa con el nombre '{nombre}'.");
        }

        entidad.Renombrar(nombre);

        await _repository.UpdateAsync(entidad, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task BajaAsync(int idActor, int idMarca, CancellationToken cancellationToken = default)
    {
        await AutorizacionClasificacion.ExigirAsync(_usuarios, idActor, cancellationToken);

        var entidad = await _repository.GetByIdAsync(idMarca, includeDeleted: false, cancellationToken);
        if (entidad == null)
        {
            throw new DomainException($"No se encontró la marca con ID {idMarca}.");
        }

        // Invariante entre agregados: Marca y sus dependientes son raíces distintas, por eso la regla "no se da de
        // baja lo que está en uso" (D-1) se resuelve acá y no en la entidad (sección 4). Entre el conteo y el
        // guardado otro usuario podría asignar la marca a un artículo: ese riesgo se acepta (R-5).
        int enUso = await _consultas.ContarArticulosPorMarcaAsync(idMarca, cancellationToken);
        if (enUso > 0)
        {
            throw new ClasificacionEnUsoException("la marca", entidad.NombreMarca, enUso, "artículos");
        }

        entidad.MarkAsDeleted();

        await _repository.UpdateAsync(entidad, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
