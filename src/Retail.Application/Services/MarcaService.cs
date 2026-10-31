using FluentValidation;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Common;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Casos de uso de administración de marcas (Track 6.5, RF-04) y de sus alias y prefijos EAN, que son hijos del
/// agregado <see cref="Marca"/> (Track 6.6, sección 8.1). Toda escritura exige un actor con autoridad (D-5); los
/// listados los consulta cualquier usuario (D-21).
/// </summary>
public class MarcaService : IMarcaService
{
    private readonly IRepository<Marca> _repository;
    private readonly IRepository<Proveedor> _proveedores;
    private readonly IRepository<Usuario> _usuarios;
    private readonly IClasificacionQueryService _consultas;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CrearMarcaDto> _crearValidator;
    private readonly IValidator<RenombrarMarcaDto> _renombrarValidator;
    private readonly IValidator<AliasMarcaDto> _aliasValidator;
    private readonly IValidator<PrefijoMarcaDto> _prefijoValidator;

    public MarcaService(
        IRepository<Marca> repository,
        IRepository<Proveedor> proveedores,
        IRepository<Usuario> usuarios,
        IClasificacionQueryService consultas,
        IUnitOfWork unitOfWork,
        IValidator<CrearMarcaDto> crearValidator,
        IValidator<RenombrarMarcaDto> renombrarValidator,
        IValidator<AliasMarcaDto> aliasValidator,
        IValidator<PrefijoMarcaDto> prefijoValidator)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _proveedores = proveedores ?? throw new ArgumentNullException(nameof(proveedores));
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
        _consultas = consultas ?? throw new ArgumentNullException(nameof(consultas));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _crearValidator = crearValidator ?? throw new ArgumentNullException(nameof(crearValidator));
        _renombrarValidator = renombrarValidator ?? throw new ArgumentNullException(nameof(renombrarValidator));
        _aliasValidator = aliasValidator ?? throw new ArgumentNullException(nameof(aliasValidator));
        _prefijoValidator = prefijoValidator ?? throw new ArgumentNullException(nameof(prefijoValidator));
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

        await ExigirNombreDistinguibleAsync(nombre, idMarcaPropia: null, cancellationToken);

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

        await ExigirNombreDistinguibleAsync(nombre, dto.IdMarca, cancellationToken);

        entidad.Renombrar(nombre);

        await _repository.UpdateAsync(entidad, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task BajaAsync(int idActor, int idMarca, CancellationToken cancellationToken = default)
    {
        await AutorizacionClasificacion.ExigirAsync(_usuarios, idActor, cancellationToken);

        // Con sus alias y prefijos: la baja de la marca los da de baja también (Track 6.6), y solo alcanza a los que
        // están en memoria. Si quedaran activos, el índice único no dejaría usarlos en otra marca.
        var entidad = await _repository.GetByIdWithIncludesAsync(idMarca, cancellationToken, m => m.Alias, m => m.Prefijos);
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

        // M-35: las equivalencias que traducen a esta marca no tienen sentido sin ella. Se dan de baja en el mismo
        // guardado que la marca, así que la baja ocurre completa o no ocurre.
        await PersistenciaDiccionarios.QuitarEquivalenciasHaciaAsync(
            _proveedores,
            _consultas,
            TipoEquivalenciaEnum.Marca,
            idMarca,
            cancellationToken);

        entidad.MarkAsDeleted();

        await _repository.UpdateAsync(entidad, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AliasListadoDto>> ListarAliasAsync(
        string? filtro,
        int? idMarca,
        CancellationToken cancellationToken = default)
    {
        return await _consultas.ListarAliasAsync(filtro, idMarca, cancellationToken);
    }

    public async Task<IReadOnlyList<PrefijoListadoDto>> ListarPrefijosAsync(
        string? filtro,
        int? idMarca,
        CancellationToken cancellationToken = default)
    {
        return await _consultas.ListarPrefijosAsync(filtro, idMarca, cancellationToken);
    }

    public async Task AgregarAliasAsync(int idActor, AliasMarcaDto dto, CancellationToken cancellationToken = default)
    {
        await AutorizacionClasificacion.ExigirAsync(_usuarios, idActor, cancellationToken);
        await _aliasValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var entidad = await ObtenerConDiccionariosAsync(dto.IdMarca, cancellationToken);
        string alias = ReglasClasificacion.ExigirAlias(dto.Alias);

        // Unicidad entre agregados (sección 8.1): la marca solo ve sus propios alias. Que el alias no sea de otra marca
        // ni el nombre de otra marca lo resuelve esta capa; si no, el motor encontraría dos marcas para la misma palabra.
        string? duenoDelAlias = await _consultas.BuscarMarcaPorAliasAsync(alias, dto.IdMarca, cancellationToken);
        if (duenoDelAlias is not null)
        {
            throw new DomainException($"El alias '{alias}' ya pertenece a la marca '{duenoDelAlias}'.");
        }

        string? marcaConEseNombre = await _consultas.BuscarMarcaPorNombreAsync(alias, dto.IdMarca, cancellationToken);
        if (marcaConEseNombre is not null)
        {
            throw new DomainException($"El alias '{alias}' es el nombre de la marca '{marcaConEseNombre}'.");
        }

        entidad.AgregarAlias(alias);

        await _repository.UpdateAsync(entidad, cancellationToken);
        await PersistenciaDiccionarios.GuardarAsync(
            _unitOfWork,
            $"Otro usuario registró el alias '{alias}' al mismo tiempo. Actualice la lista y vuelva a intentarlo.",
            cancellationToken);
    }

    public async Task QuitarAliasAsync(int idActor, AliasMarcaDto dto, CancellationToken cancellationToken = default)
    {
        await AutorizacionClasificacion.ExigirAsync(_usuarios, idActor, cancellationToken);
        await _aliasValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var entidad = await ObtenerConDiccionariosAsync(dto.IdMarca, cancellationToken);
        entidad.QuitarAlias(dto.Alias);

        await _repository.UpdateAsync(entidad, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task AgregarPrefijoAsync(int idActor, PrefijoMarcaDto dto, CancellationToken cancellationToken = default)
    {
        await AutorizacionClasificacion.ExigirAsync(_usuarios, idActor, cancellationToken);
        await _prefijoValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var entidad = await ObtenerConDiccionariosAsync(dto.IdMarca, cancellationToken);
        string prefijo = ReglasCodigoBarras.ExigirPrefijoTitular(dto.Prefijo);

        // Un prefijo identifica al titular del código: si perteneciera a dos marcas, el motor no sabría a cuál sugerir.
        string? duenoDelPrefijo = await _consultas.BuscarMarcaPorPrefijoAsync(prefijo, dto.IdMarca, cancellationToken);
        if (duenoDelPrefijo is not null)
        {
            throw new DomainException($"El prefijo '{prefijo}' ya pertenece a la marca '{duenoDelPrefijo}'.");
        }

        entidad.AgregarPrefijo(prefijo);

        await _repository.UpdateAsync(entidad, cancellationToken);
        await PersistenciaDiccionarios.GuardarAsync(
            _unitOfWork,
            $"Otro usuario registró el prefijo '{prefijo}' al mismo tiempo. Actualice la lista y vuelva a intentarlo.",
            cancellationToken);
    }

    public async Task QuitarPrefijoAsync(int idActor, PrefijoMarcaDto dto, CancellationToken cancellationToken = default)
    {
        await AutorizacionClasificacion.ExigirAsync(_usuarios, idActor, cancellationToken);
        await _prefijoValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var entidad = await ObtenerConDiccionariosAsync(dto.IdMarca, cancellationToken);
        entidad.QuitarPrefijo(dto.Prefijo);

        await _repository.UpdateAsync(entidad, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // El motor de catalogación busca las marcas en su forma de comparación (mayúsculas y sin diacríticos): un nombre que
    // en esa forma coincide con otra marca o con un alias de otra haría que el motor encontrara dos marcas para la misma
    // palabra. M-39: el control de duplicados de D-8 usa Modern_Spanish_CI_AI, que distingue la ñ ("Nandu" ≠ "Ñandú"),
    // así que no alcanza. M-38: la regla simétrica del alias.
    private async Task ExigirNombreDistinguibleAsync(string nombre, int? idMarcaPropia, CancellationToken cancellationToken)
    {
        string formaComparacion = ReglasClasificacion.NormalizarTexto(nombre);
        string? homonima = await _consultas.BuscarMarcaPorNombreAsync(formaComparacion, idMarcaPropia, cancellationToken);
        if (homonima is not null)
        {
            throw new DomainException(
                $"Ya existe la marca '{homonima}': el motor de catalogación no la distingue de '{nombre}'.");
        }

        string? duenoDelAlias = await _consultas.BuscarMarcaPorAliasAsync(formaComparacion, idMarcaPropia, cancellationToken);
        if (duenoDelAlias is not null)
        {
            throw new DomainException($"El nombre '{nombre}' ya es un alias de la marca '{duenoDelAlias}'.");
        }
    }

    // El agregado se carga con sus alias y prefijos: sus invariantes internas (sin duplicados, el alias distinto del
    // nombre) solo se pueden verificar si los ve.
    private async Task<Marca> ObtenerConDiccionariosAsync(int idMarca, CancellationToken cancellationToken)
    {
        var entidad = await _repository.GetByIdWithIncludesAsync(idMarca, cancellationToken, m => m.Alias, m => m.Prefijos);
        if (entidad is null)
        {
            throw new DomainException($"No se encontró la marca con ID {idMarca}.");
        }

        return entidad;
    }
}
