using System.Diagnostics;
using FluentValidation;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Implementación de los casos de uso para gestión de proveedores e importación masiva de catálogos (RF-05, RF-07, RNF-02, RNF-03).
/// </summary>
public class ProveedorService : IProveedorService
{
    private const int LongitudMaximaCodigo = 50;
    private const int LongitudMaximaDescripcion = 200;

    private readonly IRepository<Proveedor> _proveedorRepository;
    private readonly ICatalogoProveedorQueryService _catalogoQueryService;
    private readonly IRepository<Articulo> _articuloRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExcelCatalogParser _excelCatalogParser;
    private readonly IValidator<CrearProveedorDto> _crearProveedorValidator;
    private readonly IValidator<ProveedorDto> _actualizarProveedorValidator;
    private readonly IValidator<MapeoColumnasDto> _mapeoColumnasValidator;
    private readonly IValidator<IncorporarCatalogoArticulosDto> _incorporarArticulosValidator;

    public ProveedorService(
        IRepository<Proveedor> proveedorRepository,
        ICatalogoProveedorQueryService catalogoQueryService,
        IRepository<Articulo> articuloRepository,
        IUnitOfWork unitOfWork,
        IExcelCatalogParser excelCatalogParser,
        IValidator<CrearProveedorDto> crearProveedorValidator,
        IValidator<ProveedorDto> actualizarProveedorValidator,
        IValidator<MapeoColumnasDto> mapeoColumnasValidator,
        IValidator<IncorporarCatalogoArticulosDto> incorporarArticulosValidator)
    {
        _proveedorRepository = proveedorRepository ?? throw new ArgumentNullException(nameof(proveedorRepository));
        _catalogoQueryService = catalogoQueryService ?? throw new ArgumentNullException(nameof(catalogoQueryService));
        _articuloRepository = articuloRepository ?? throw new ArgumentNullException(nameof(articuloRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _excelCatalogParser = excelCatalogParser ?? throw new ArgumentNullException(nameof(excelCatalogParser));
        _crearProveedorValidator = crearProveedorValidator ?? throw new ArgumentNullException(nameof(crearProveedorValidator));
        _actualizarProveedorValidator = actualizarProveedorValidator ?? throw new ArgumentNullException(nameof(actualizarProveedorValidator));
        _mapeoColumnasValidator = mapeoColumnasValidator ?? throw new ArgumentNullException(nameof(mapeoColumnasValidator));
        _incorporarArticulosValidator = incorporarArticulosValidator ?? throw new ArgumentNullException(nameof(incorporarArticulosValidator));
    }

    public async Task<IReadOnlyList<ProveedorDto>> ListarProveedoresAsync(CancellationToken cancellationToken = default)
    {
        var proveedores = await _proveedorRepository.ListAllAsync(includeDeleted: false, cancellationToken);

        return proveedores
            .OrderBy(p => p.RazonSocial)
            .Select(p => new ProveedorDto
            {
                IdProveedor = p.Id,
                RazonSocial = p.RazonSocial,
                Cuit = p.Cuit,
                Telefono = p.Telefono,
                Email = p.Email
            })
            .ToList();
    }

    public async Task<ProveedorDto?> ObtenerPorIdAsync(int idProveedor, CancellationToken cancellationToken = default)
    {
        var proveedor = await _proveedorRepository.GetByIdAsync(idProveedor, includeDeleted: false, cancellationToken);
        if (proveedor == null) return null;

        return new ProveedorDto
        {
            IdProveedor = proveedor.Id,
            RazonSocial = proveedor.RazonSocial,
            Cuit = proveedor.Cuit,
            Telefono = proveedor.Telefono,
            Email = proveedor.Email
        };
    }

    public async Task<ProveedorDto> CrearProveedorAsync(CrearProveedorDto dto, CancellationToken cancellationToken = default)
    {
        await _crearProveedorValidator.ValidateAndThrowAsync(dto, cancellationToken);

        string cuitLimpio = dto.Cuit.Replace("-", "").Trim();
        var existentes = await _proveedorRepository.FindAsync(
            p => p.Cuit == cuitLimpio || p.Cuit == dto.Cuit.Trim(),
            includeDeleted: false,
            cancellationToken);

        if (existentes.Count > 0)
        {
            throw new DomainException($"Ya existe un proveedor activo registrado con el CUIT {dto.Cuit}.");
        }

        var proveedor = new Proveedor();
        proveedor.ActualizarDatos(dto.RazonSocial, dto.Cuit, dto.Telefono, dto.Email);

        await _proveedorRepository.AddAsync(proveedor, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ProveedorDto
        {
            IdProveedor = proveedor.Id,
            RazonSocial = proveedor.RazonSocial,
            Cuit = proveedor.Cuit,
            Telefono = proveedor.Telefono,
            Email = proveedor.Email
        };
    }

    public async Task ActualizarProveedorAsync(ProveedorDto dto, CancellationToken cancellationToken = default)
    {
        await _actualizarProveedorValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var proveedor = await _proveedorRepository.GetByIdAsync(dto.IdProveedor, includeDeleted: false, cancellationToken);
        if (proveedor == null)
        {
            throw new DomainException($"No se encontró el proveedor con ID {dto.IdProveedor}.");
        }

        string cuitLimpio = dto.Cuit.Replace("-", "").Trim();
        var existentes = await _proveedorRepository.FindAsync(
            p => (p.Cuit == cuitLimpio || p.Cuit == dto.Cuit.Trim()) && p.Id != dto.IdProveedor,
            includeDeleted: false,
            cancellationToken);

        if (existentes.Count > 0)
        {
            throw new DomainException($"Ya existe otro proveedor activo registrado con el CUIT {dto.Cuit}.");
        }

        proveedor.ActualizarDatos(dto.RazonSocial, dto.Cuit, dto.Telefono, dto.Email);

        await _proveedorRepository.UpdateAsync(proveedor, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task BajaProveedorAsync(int idProveedor, CancellationToken cancellationToken = default)
    {
        var proveedor = await _proveedorRepository.GetByIdAsync(idProveedor, includeDeleted: false, cancellationToken);
        if (proveedor == null)
        {
            throw new DomainException($"No se encontró el proveedor con ID {idProveedor}.");
        }

        proveedor.MarkAsDeleted();
        await _proveedorRepository.UpdateAsync(proveedor, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<ResultadoImportacionDto> ImportarPlanillaProveedorAsync(
        Stream archivoStream,
        MapeoColumnasDto mapeo,
        IProgress<int>? progreso = null,
        CancellationToken cancellationToken = default)
    {
        await _mapeoColumnasValidator.ValidateAndThrowAsync(mapeo, cancellationToken);

        var proveedor = await _proveedorRepository.GetByIdAsync(mapeo.IdProveedor, includeDeleted: false, cancellationToken);
        if (proveedor == null)
        {
            throw new DomainException($"No se encontró el proveedor con ID {mapeo.IdProveedor}.");
        }

        var cronometro = Stopwatch.StartNew();

        var filasImportadas = await _excelCatalogParser.ParsearCatalogoAsync(archivoStream, mapeo, progreso, cancellationToken);

        var articulosLocales = await _articuloRepository.FindAsync(a => a.IdCatalogoProveedor != null, includeDeleted: false, cancellationToken);
        var articulosPorCatalogo = articulosLocales.ToLookup(a => a.IdCatalogoProveedor!.Value);

        var erroresDetalle = new List<string>();
        var filasValidas = FiltrarFilasValidas(filasImportadas, erroresDetalle);

        var codigos = filasValidas.Select(f => f.Item.CodigoProveedor);
        var mapCatalogosExactos = await _catalogoQueryService.ObtenerMapaPorCodigosProveedorAsync(mapeo.IdProveedor, codigos, cancellationToken);
        var mapCatalogos = new Dictionary<string, CatalogoProveedor>(StringComparer.OrdinalIgnoreCase);
        foreach (var (codigo, catalogo) in mapCatalogosExactos)
        {
            mapCatalogos.TryAdd(codigo.Trim(), catalogo);
        }

        int filasProcesadas = 0;
        int nuevosRegistros = 0;
        int actualizados = 0;

        foreach (var (numeroFila, item) in filasValidas)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (mapCatalogos.TryGetValue(item.CodigoProveedor, out var catalogoExistente))
                {
                    catalogoExistente.ActualizarPrecio(item.PrecioCosto, item.Descripcion, item.CodigoBarras);
                    await _catalogoQueryService.ActualizarAsync(catalogoExistente, cancellationToken);

                    foreach (var articuloAsociado in articulosPorCatalogo[catalogoExistente.Id])
                    {
                        articuloAsociado.ActualizarCostoYRecalcularPrecio(item.PrecioCosto);
                        await _articuloRepository.UpdateAsync(articuloAsociado, cancellationToken);
                        actualizados++;
                    }
                }
                else
                {
                    var nuevoCatalogo = new CatalogoProveedor
                    {
                        IdProveedor = mapeo.IdProveedor,
                        CodigoProveedor = item.CodigoProveedor,
                        CodigoBarras = item.CodigoBarras,
                        DescripcionProveedor = item.Descripcion,
                        CostoReposicion = item.PrecioCosto,
                        FechaActualizacion = DateTime.UtcNow
                    };
                    await _catalogoQueryService.AgregarAsync(nuevoCatalogo, cancellationToken);
                    mapCatalogos[item.CodigoProveedor] = nuevoCatalogo;
                    nuevosRegistros++;
                }

                filasProcesadas++;
            }
            catch (Exception ex) when (ex is ArgumentException or DomainException)
            {
                erroresDetalle.Add($"Fila {numeroFila}: {ex.Message}");
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        cronometro.Stop();

        return new ResultadoImportacionDto
        {
            TotalFilasProcesadas = filasProcesadas,
            NuevosRegistros = nuevosRegistros,
            PreciosActualizados = actualizados,
            FilasConError = erroresDetalle.Count,
            ErroresDetalle = erroresDetalle,
            TiempoTranscurrido = cronometro.Elapsed
        };
    }

    /// <summary>
    /// Normaliza (trim) y valida cada fila de la planilla contra los límites de columna y descarta duplicados
    /// de código (sin distinguir mayúsculas). Las filas descartadas se reportan en <paramref name="errores"/>
    /// para que un dato inválido no aborte el guardado de toda la importación.
    /// </summary>
    private static List<(int NumeroFila, ItemCatalogoImportadoDto Item)> FiltrarFilasValidas(
        IReadOnlyList<ItemCatalogoImportadoDto> filas,
        List<string> errores)
    {
        var validas = new List<(int, ItemCatalogoImportadoDto)>(filas.Count);
        var codigosVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < filas.Count; i++)
        {
            int numeroFila = i + 1;
            var fila = filas[i];
            string codigo = (fila.CodigoProveedor ?? string.Empty).Trim();
            string descripcion = (fila.Descripcion ?? string.Empty).Trim();
            string? codigoBarras = string.IsNullOrWhiteSpace(fila.CodigoBarras) ? null : fila.CodigoBarras.Trim();

            string? motivo = null;
            if (codigo.Length == 0)
            {
                motivo = "el código de proveedor está vacío.";
            }
            else if (codigo.Length > LongitudMaximaCodigo)
            {
                motivo = $"el código '{codigo}' supera los {LongitudMaximaCodigo} caracteres.";
            }
            else if (descripcion.Length == 0)
            {
                motivo = "la descripción está vacía.";
            }
            else if (descripcion.Length > LongitudMaximaDescripcion)
            {
                motivo = $"la descripción supera los {LongitudMaximaDescripcion} caracteres.";
            }
            else if (codigoBarras is { Length: > LongitudMaximaCodigo })
            {
                motivo = $"el código de barras supera los {LongitudMaximaCodigo} caracteres.";
            }
            else if (!codigosVistos.Add(codigo))
            {
                motivo = $"el código '{codigo}' está duplicado en la planilla (se conserva la primera aparición).";
            }

            if (motivo != null)
            {
                errores.Add($"Fila {numeroFila}: {motivo}");
                continue;
            }

            validas.Add((numeroFila, fila with { CodigoProveedor = codigo, Descripcion = descripcion, CodigoBarras = codigoBarras }));
        }

        return validas;
    }

    public async Task<CatalogoPaginadoDto> ListarItemsCatalogoAsync(ConsultaCatalogoProveedorDto consulta, CancellationToken cancellationToken = default)
    {
        return await _catalogoQueryService.ObtenerCatalogoPaginadoAsync(consulta, cancellationToken);
    }

    public async Task IncorporarArticulosATiendaAsync(IncorporarCatalogoArticulosDto dto, CancellationToken cancellationToken = default)
    {
        await _incorporarArticulosValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var ids = dto.Items.Count > 0
            ? dto.Items.Select(i => i.IdCatalogo).ToList()
            : dto.IdsCatalogo ?? Array.Empty<int>();

        ids = ids.Distinct().ToList();

        var catalogos = await _catalogoQueryService.ObtenerPorIdsAsync(ids, cancellationToken);
        var itemDict = dto.Items
            .GroupBy(i => i.IdCatalogo)
            .ToDictionary(g => g.Key, g => g.First());

        var yaVinculados = await _articuloRepository.FindAsync(
            a => a.IdCatalogoProveedor != null && ids.Contains(a.IdCatalogoProveedor.Value),
            includeDeleted: false,
            cancellationToken);
        var idsYaVinculados = yaVinculados.Select(a => a.IdCatalogoProveedor!.Value).ToHashSet();

        foreach (var catalogo in catalogos.Where(c => !idsYaVinculados.Contains(c.Id)))
        {
            var porcentajeGanancia = itemDict.TryGetValue(catalogo.Id, out var itemDto)
                ? itemDto.PorcentajeGanancia
                : dto.PorcentajeGananciaSugerido;

            var precioVenta = Articulo.CalcularPrecioVenta(catalogo.CostoReposicion, porcentajeGanancia);

            var nuevoArticulo = new Articulo
            {
                Descripcion = catalogo.DescripcionProveedor,
                CodigoBarras = catalogo.CodigoBarras,
                IdCategoria = dto.IdCategoria,
                IdMarca = dto.IdMarca,
                IdCatalogoProveedor = catalogo.Id,
                CostoReposicion = catalogo.CostoReposicion,
                PorcentajeGanancia = porcentajeGanancia,
                PrecioVenta = precioVenta,
                StockActual = 0,
                StockMinimo = 5
            };

            await _articuloRepository.AddAsync(nuevoArticulo, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task VincularArticuloACatalogoAsync(int idArticulo, int idCatalogoProveedor, CancellationToken cancellationToken = default)
    {
        var articulo = await _articuloRepository.GetByIdAsync(idArticulo, includeDeleted: false, cancellationToken);
        if (articulo == null)
        {
            throw new DomainException($"No se encontró el artículo con ID {idArticulo}.");
        }

        var catalogo = await _catalogoQueryService.ObtenerPorIdAsync(idCatalogoProveedor, cancellationToken);
        if (catalogo == null)
        {
            throw new DomainException($"No se encontró el ítem de catálogo con ID {idCatalogoProveedor}.");
        }

        articulo.VincularCatalogoProveedor(catalogo.Id, catalogo.CostoReposicion);

        await _articuloRepository.UpdateAsync(articulo, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DesvincularArticuloDeCatalogoAsync(int idArticulo, CancellationToken cancellationToken = default)
    {
        var articulo = await _articuloRepository.GetByIdAsync(idArticulo, includeDeleted: false, cancellationToken);
        if (articulo == null)
        {
            throw new DomainException($"No se encontró el artículo con ID {idArticulo}.");
        }

        articulo.DesvincularCatalogoProveedor();

        await _articuloRepository.UpdateAsync(articulo, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

