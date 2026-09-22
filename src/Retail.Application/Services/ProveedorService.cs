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
        var mapArticulos = articulosLocales.ToDictionary(a => a.IdCatalogoProveedor!.Value, a => a);

        var codigos = filasImportadas.Select(f => f.CodigoProveedor);
        var mapCatalogos = await _catalogoQueryService.ObtenerMapaPorCodigosProveedorAsync(mapeo.IdProveedor, codigos, cancellationToken);

        int filasProcesadas = 0;
        int nuevosRegistros = 0;
        int actualizados = 0;
        int errores = 0;

        foreach (var item in filasImportadas)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (mapCatalogos.TryGetValue(item.CodigoProveedor, out var catalogoExistente))
                {
                    catalogoExistente.ActualizarPrecio(item.PrecioCosto, item.Descripcion, item.CodigoBarras);
                    await _catalogoQueryService.ActualizarAsync(catalogoExistente, cancellationToken);

                    if (mapArticulos.TryGetValue(catalogoExistente.Id, out var articuloAsociado))
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
                    nuevosRegistros++;
                }

                filasProcesadas++;
            }
            catch
            {
                errores++;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        cronometro.Stop();

        return new ResultadoImportacionDto
        {
            TotalFilasProcesadas = filasProcesadas,
            NuevosRegistros = nuevosRegistros,
            PreciosActualizados = actualizados,
            FilasConError = errores,
            TiempoTranscurrido = cronometro.Elapsed
        };
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

        var catalogos = await _catalogoQueryService.ObtenerPorIdsAsync(ids, cancellationToken);
        var itemDict = dto.Items.ToDictionary(i => i.IdCatalogo);

        foreach (var catalogo in catalogos)
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

