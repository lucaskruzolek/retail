using FluentValidation;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Common;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Casos de uso de administración de categorías (Track 6.5, RF-04). Toda escritura exige un actor con autoridad (D-5);
/// el listado lo consulta cualquier usuario (D-21).
/// </summary>
public class CategoriaService : ICategoriaService
{
    private readonly IRepository<Categoria> _repository;
    private readonly IRepository<Rubro> _rubros;
    private readonly IRepository<TerminoClasificacion> _terminos;
    private readonly IRepository<Proveedor> _proveedores;
    private readonly IRepository<Usuario> _usuarios;
    private readonly IClasificacionQueryService _consultas;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CrearCategoriaDto> _crearValidator;
    private readonly IValidator<ActualizarCategoriaDto> _actualizarValidator;

    public CategoriaService(
        IRepository<Categoria> repository,
        IRepository<Rubro> rubros,
        IRepository<TerminoClasificacion> terminos,
        IRepository<Proveedor> proveedores,
        IRepository<Usuario> usuarios,
        IClasificacionQueryService consultas,
        IUnitOfWork unitOfWork,
        IValidator<CrearCategoriaDto> crearValidator,
        IValidator<ActualizarCategoriaDto> actualizarValidator)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _rubros = rubros ?? throw new ArgumentNullException(nameof(rubros));
        _terminos = terminos ?? throw new ArgumentNullException(nameof(terminos));
        _proveedores = proveedores ?? throw new ArgumentNullException(nameof(proveedores));
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
        _consultas = consultas ?? throw new ArgumentNullException(nameof(consultas));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _crearValidator = crearValidator ?? throw new ArgumentNullException(nameof(crearValidator));
        _actualizarValidator = actualizarValidator ?? throw new ArgumentNullException(nameof(actualizarValidator));
    }

    public async Task<IReadOnlyList<CategoriaDto>> ListarAsync(string? filtro, int? idRubro, CancellationToken cancellationToken = default)
    {
        return await _consultas.ListarCategoriasAsync(filtro, idRubro, cancellationToken);
    }

    public async Task<CategoriaDto> CrearAsync(int idActor, CrearCategoriaDto dto, CancellationToken cancellationToken = default)
    {
        // Primero la autorización: quien no tiene permiso no recibe detalles de validación.
        await AutorizacionClasificacion.ExigirAsync(_usuarios, idActor, cancellationToken);
        await _crearValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var (entidad, rubro) = await AltaCategoria.PrepararAsync(
            _repository,
            _rubros,
            dto.Nombre,
            dto.IdRubro,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CategoriaDto
        {
            IdCategoria = entidad.Id,
            NombreCategoria = entidad.NombreCategoria,
            IdRubro = rubro.Id,
            NombreRubro = rubro.NombreRubro,
            CantidadEnUso = 0
        };
    }

    public async Task ActualizarAsync(int idActor, ActualizarCategoriaDto dto, CancellationToken cancellationToken = default)
    {
        await AutorizacionClasificacion.ExigirAsync(_usuarios, idActor, cancellationToken);
        await _actualizarValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var entidad = await _repository.GetByIdAsync(dto.IdCategoria, includeDeleted: false, cancellationToken);
        if (entidad == null)
        {
            throw new DomainException($"No se encontró la categoría con ID {dto.IdCategoria}.");
        }

        var rubro = await AltaCategoria.ObtenerRubroActivoAsync(_rubros, dto.IdRubro, cancellationToken);

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
        entidad.MoverARubro(rubro.Id);

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

        // M-35 y M-27: los términos y las equivalencias que apuntan a la categoría son reglas derivadas que no tienen
        // sentido sin ella. Se dan de baja en el mismo guardado que la categoría: la baja ocurre completa o no ocurre.
        // El término pasa a ser desconocido y vuelve a la decisión en bloque de la incorporación (M-26).
        var terminos = await _terminos.FindAsync(t => t.IdCategoria == idCategoria, includeDeleted: false, cancellationToken);
        foreach (var termino in terminos)
        {
            termino.MarkAsDeleted();
            await _terminos.UpdateAsync(termino, cancellationToken);
        }

        // Los pares hacia la categoría son de otros términos, los ambiguos (P-3). Se dan de baja por su raíz (Ley 2),
        // que llega con sus pares (AutoInclude, M-57); EF traduce el Any a un EXISTS. Los ignorados no se tocan.
        var conPares = await _terminos.FindAsync(
            t => t.Pares.Any(p => p.IdCategoria == idCategoria),
            includeDeleted: false,
            cancellationToken);
        foreach (var termino in conPares)
        {
            termino.QuitarParesHacia(idCategoria);
            await _terminos.UpdateAsync(termino, cancellationToken);
        }

        await PersistenciaDiccionarios.QuitarEquivalenciasHaciaAsync(
            _proveedores,
            _consultas,
            TipoEquivalenciaEnum.Categoria,
            idCategoria,
            cancellationToken);

        entidad.MarkAsDeleted();

        await _repository.UpdateAsync(entidad, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
