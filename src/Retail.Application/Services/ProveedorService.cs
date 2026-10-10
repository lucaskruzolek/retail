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

        var parseo = await _excelCatalogParser.ParsearCatalogoAsync(archivoStream, mapeo, progreso, cancellationToken);

        // Q2: solo los artículos con hilo a un renglón de ESTE proveedor (H-14). SQL Server lo resuelve con
        // un JOIN por la FK y un único parámetro escalar, sin enviar listas de IDs.
        var articulosLocales = await _articuloRepository.FindAsync(
            a => a.CatalogoProveedor != null && a.CatalogoProveedor.IdProveedor == mapeo.IdProveedor,
            includeDeleted: false,
            cancellationToken);
        var articulosPorCatalogo = articulosLocales.ToLookup(a => a.IdCatalogoProveedor!.Value);

        // Q3: las presentaciones derivadas de esos artículos (RF-21). No tienen catálogo propio, así que se
        // llega a ellas por la navegación al origen: otro JOIN con el mismo parámetro escalar, sin listas.
        var presentaciones = await _articuloRepository.FindAsync(
            a => a.ArticuloOrigen != null
                && a.ArticuloOrigen.CatalogoProveedor != null
                && a.ArticuloOrigen.CatalogoProveedor.IdProveedor == mapeo.IdProveedor,
            includeDeleted: false,
            cancellationToken);
        var presentacionesPorOrigen = presentaciones.ToLookup(a => a.IdArticuloOrigen);

        // Las filas que el parser no pudo interpretar también son errores que el usuario debe ver (H-03).
        var erroresDetalle = new List<string>(parseo.FilasDescartadas);
        var filasValidas = FiltrarFilasValidas(parseo.Items, erroresDetalle);

        var codigos = filasValidas.Select(f => f.CodigoProveedor);
        var mapCatalogosExactos = await _catalogoQueryService.ObtenerMapaPorCodigosProveedorAsync(mapeo.IdProveedor, codigos, cancellationToken);
        var mapCatalogos = new Dictionary<string, CatalogoProveedor>(StringComparer.OrdinalIgnoreCase);
        foreach (var (codigo, catalogo) in mapCatalogosExactos)
        {
            mapCatalogos.TryAdd(codigo.Trim(), catalogo);
        }

        int filasProcesadas = 0;
        int nuevosRegistros = 0;
        int actualizados = 0;

        try
        {
            foreach (var item in filasValidas)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Una fila que falla no queda a medias: ActualizarPrecio valida antes de asignar y, si pasa, el
                // precio es positivo, así que ni el artículo (H-08) ni sus presentaciones derivadas pueden fallar
                // después. Si el Dominio agregara una regla que sí pudiera fallar ahí, habría que validar la fila
                // completa antes de modificar el catálogo.
                try
                {
                    if (mapCatalogos.TryGetValue(item.CodigoProveedor, out var catalogoExistente))
                    {
                        catalogoExistente.ActualizarPrecio(item.PrecioCosto, item.Descripcion, item.CodigoBarras);
                        await _catalogoQueryService.ActualizarAsync(catalogoExistente, cancellationToken);

                        foreach (var articuloAsociado in articulosPorCatalogo[catalogoExistente.Id])
                        {
                            // Solo cuenta como precio actualizado si el costo del artículo realmente cambia (H-16).
                            if (articuloAsociado.CostoReposicion == item.PrecioCosto)
                            {
                                continue;
                            }

                            articuloAsociado.ActualizarCostoYRecalcularPrecio(item.PrecioCosto);
                            await _articuloRepository.UpdateAsync(articuloAsociado, cancellationToken);
                            actualizados++;

                            // El nuevo costo se propaga a las presentaciones; cada una cuenta solo si su costo cambia,
                            // porque el redondeo puede dejar igual el costo unitario (H-16, RF-21).
                            foreach (var presentacion in presentacionesPorOrigen[articuloAsociado.Id])
                            {
                                var costoAnteriorPresentacion = presentacion.CostoReposicion;
                                presentacion.RecalcularCostoDesdeOrigen(articuloAsociado.CostoReposicion);
                                if (presentacion.CostoReposicion == costoAnteriorPresentacion)
                                {
                                    continue;
                                }

                                await _articuloRepository.UpdateAsync(presentacion, cancellationToken);
                                actualizados++;
                            }
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
                    erroresDetalle.Add($"Fila {item.NumeroFila}: {ex.Message}");
                }
            }
        }
        catch
        {
            // Cancelación o falla inesperada a mitad del lote: las filas ya procesadas quedaron en el contexto de la
            // pantalla y se guardarían con la próxima operación exitosa.
            _unitOfWork.DescartarCambios();
            throw;
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
    /// para que un dato inválido no aborte el guardado de toda la importación. Los mensajes usan el número de
    /// fila real de la hoja, informado por el parser (H-02).
    /// </summary>
    private static List<ItemCatalogoImportadoDto> FiltrarFilasValidas(
        IReadOnlyList<ItemCatalogoImportadoDto> filas,
        List<string> errores)
    {
        var validas = new List<ItemCatalogoImportadoDto>(filas.Count);
        var codigosVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fila in filas)
        {
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
                errores.Add($"Fila {fila.NumeroFila}: {motivo}");
                continue;
            }

            validas.Add(fila with { CodigoProveedor = codigo, Descripcion = descripcion, CodigoBarras = codigoBarras });
        }

        return validas;
    }

    public async Task<CatalogoPaginadoDto> ListarItemsCatalogoAsync(ConsultaCatalogoProveedorDto consulta, CancellationToken cancellationToken = default)
    {
        return await _catalogoQueryService.ObtenerCatalogoPaginadoAsync(consulta, cancellationToken);
    }

    public async Task<ResultadoIncorporacionDto> IncorporarArticulosATiendaAsync(IncorporarCatalogoArticulosDto dto, CancellationToken cancellationToken = default)
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

        int incorporados = 0;
        int omitidosYaVinculados = 0;

        foreach (var catalogo in catalogos)
        {
            // Un ítem que ya tiene artículo en la tienda no se duplica, pero se informa (H-11).
            if (idsYaVinculados.Contains(catalogo.Id))
            {
                omitidosYaVinculados++;
                continue;
            }

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
            incorporados++;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ResultadoIncorporacionDto
        {
            Incorporados = incorporados,
            OmitidosYaVinculados = omitidosYaVinculados
        };
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

        // Un ítem de catálogo se vincula a un solo artículo de la tienda, igual que en la incorporación (H-17).
        // Revincular el mismo artículo sigue permitido.
        var vinculadosAOtroArticulo = await _articuloRepository.FindAsync(
            a => a.IdCatalogoProveedor == idCatalogoProveedor && a.Id != idArticulo,
            includeDeleted: false,
            cancellationToken);
        if (vinculadosAOtroArticulo.Count > 0)
        {
            throw new DomainException(
                $"El ítem '{catalogo.CodigoProveedor}' ya está vinculado al artículo '{vinculadosAOtroArticulo[0].Descripcion}'. " +
                "Desvincúlelo antes de vincularlo a otro artículo.");
        }

        articulo.VincularCatalogoProveedor(catalogo.Id, catalogo.CostoReposicion);

        await _articuloRepository.UpdateAsync(articulo, cancellationToken);

        // El costo del catálogo llega también a las presentaciones del artículo (RF-05, RF-21).
        var presentaciones = await _articuloRepository.FindAsync(
            a => a.IdArticuloOrigen == articulo.Id,
            includeDeleted: false,
            cancellationToken);
        foreach (var presentacion in presentaciones)
        {
            presentacion.RecalcularCostoDesdeOrigen(articulo.CostoReposicion);
            await _articuloRepository.UpdateAsync(presentacion, cancellationToken);
        }

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

