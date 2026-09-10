using FluentValidation;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Implementación de los casos de uso de administración del catálogo de artículos, stock,
/// cálculo reactivo de markup y alertas de inventario (RF-04, RF-06, RF-08).
/// </summary>
public class InventarioService : IInventarioService
{
    private readonly IRepository<Articulo> _articuloRepository;
    private readonly IRepository<Categoria> _categoriaRepository;
    private readonly IRepository<Marca> _marcaRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CrearArticuloDto> _crearArticuloValidator;
    private readonly IValidator<ActualizarArticuloDto> _actualizarArticuloValidator;

    public InventarioService(
        IRepository<Articulo> articuloRepository,
        IRepository<Categoria> categoriaRepository,
        IRepository<Marca> marcaRepository,
        IUnitOfWork unitOfWork,
        IValidator<CrearArticuloDto> crearArticuloValidator,
        IValidator<ActualizarArticuloDto> actualizarArticuloValidator)
    {
        _articuloRepository = articuloRepository ?? throw new ArgumentNullException(nameof(articuloRepository));
        _categoriaRepository = categoriaRepository ?? throw new ArgumentNullException(nameof(categoriaRepository));
        _marcaRepository = marcaRepository ?? throw new ArgumentNullException(nameof(marcaRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _crearArticuloValidator = crearArticuloValidator ?? throw new ArgumentNullException(nameof(crearArticuloValidator));
        _actualizarArticuloValidator = actualizarArticuloValidator ?? throw new ArgumentNullException(nameof(actualizarArticuloValidator));
    }

    public async Task<IReadOnlyList<ArticuloDto>> ListarArticulosAsync(CancellationToken cancellationToken = default)
    {
        var articulos = await _articuloRepository.ListAllAsync(includeDeleted: false, cancellationToken);
        var categorias = (await _categoriaRepository.ListAllAsync(includeDeleted: false, cancellationToken))
            .ToDictionary(c => c.Id, c => c.NombreCategoria);
        var marcas = (await _marcaRepository.ListAllAsync(includeDeleted: false, cancellationToken))
            .ToDictionary(m => m.Id, m => m.NombreMarca);

        return articulos
            .OrderBy(a => a.Descripcion)
            .Select(a => MapToDto(
                a,
                a.IdCategoria.HasValue ? categorias.GetValueOrDefault(a.IdCategoria.Value) : null,
                a.IdMarca.HasValue ? marcas.GetValueOrDefault(a.IdMarca.Value) : null))
            .ToList();
    }

    public async Task<IReadOnlyList<ArticuloVentaDto>> BuscarArticulosParaVentaAsync(string terminoBusqueda, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(terminoBusqueda))
        {
            var articulosTodos = await _articuloRepository.ListAllAsync(includeDeleted: false, cancellationToken);
            return articulosTodos
                .OrderBy(a => a.Descripcion)
                .Select(MapToVentaDto)
                .ToList();
        }

        var termino = terminoBusqueda.Trim();

        // Push-down query: busca coincidencia exacta por código o parcial en descripción
        var articulos = await _articuloRepository.FindAsync(
            a => a.CodigoBarras == termino || a.Descripcion.Contains(termino),
            includeDeleted: false,
            cancellationToken);

        return articulos
            .OrderBy(a => a.Descripcion)
            .Select(MapToVentaDto)
            .ToList();
    }

    public async Task<ArticuloDto?> ObtenerPorIdAsync(int idArticulo, CancellationToken cancellationToken = default)
    {
        if (idArticulo <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idArticulo), "El identificador del artículo debe ser mayor a cero.");
        }

        var articulo = await _articuloRepository.GetByIdAsync(idArticulo, includeDeleted: false, cancellationToken);
        if (articulo == null)
        {
            return null;
        }

        var categoria = articulo.IdCategoria.HasValue
            ? await _categoriaRepository.GetByIdAsync(articulo.IdCategoria.Value, cancellationToken)
            : null;
        var marca = articulo.IdMarca.HasValue
            ? await _marcaRepository.GetByIdAsync(articulo.IdMarca.Value, cancellationToken)
            : null;

        return MapToDto(articulo, categoria?.NombreCategoria, marca?.NombreMarca);
    }

    public async Task<ArticuloDto?> ObtenerPorCodigoBarrasAsync(string codigoBarras, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(codigoBarras))
        {
            return null;
        }

        var codigo = codigoBarras.Trim();
        var articulos = await _articuloRepository.FindAsync(
            a => a.CodigoBarras == codigo,
            includeDeleted: false,
            cancellationToken);

        var articulo = articulos.Count > 0 ? articulos[0] : null;
        if (articulo == null)
        {
            return null;
        }

        var categoria = articulo.IdCategoria.HasValue
            ? await _categoriaRepository.GetByIdAsync(articulo.IdCategoria.Value, cancellationToken)
            : null;
        var marca = articulo.IdMarca.HasValue
            ? await _marcaRepository.GetByIdAsync(articulo.IdMarca.Value, cancellationToken)
            : null;

        return MapToDto(articulo, categoria?.NombreCategoria, marca?.NombreMarca);
    }

    public async Task<ArticuloDto> CrearArticuloAsync(CrearArticuloDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await _crearArticuloValidator.ValidateAndThrowAsync(dto, cancellationToken);

        // Validar unicidad de código de barras no nulo
        if (!string.IsNullOrWhiteSpace(dto.CodigoBarras))
        {
            var codigoNormalizado = dto.CodigoBarras.Trim();
            var existentes = await _articuloRepository.FindAsync(
                a => a.CodigoBarras == codigoNormalizado,
                includeDeleted: false,
                cancellationToken);

            if (existentes.Count > 0)
            {
                throw new DomainException($"Ya existe un artículo activo con el código de barras '{codigoNormalizado}'.");
            }
        }

        var articulo = new Articulo();
        articulo.ActualizarDatos(
            descripcion: dto.Descripcion,
            idCategoria: dto.IdCategoria,
            idMarca: dto.IdMarca,
            codigoBarras: dto.CodigoBarras,
            costoReposicion: dto.CostoReposicion,
            porcentajeGanancia: dto.PorcentajeGanancia,
            stockActual: dto.StockActual,
            stockMinimo: dto.StockMinimo,
            esServicio: dto.EsServicio,
            idCatalogoProveedor: dto.IdCatalogoProveedor);

        await _articuloRepository.AddAsync(articulo, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var categoria = articulo.IdCategoria.HasValue
            ? await _categoriaRepository.GetByIdAsync(articulo.IdCategoria.Value, cancellationToken)
            : null;
        var marca = articulo.IdMarca.HasValue
            ? await _marcaRepository.GetByIdAsync(articulo.IdMarca.Value, cancellationToken)
            : null;

        return MapToDto(articulo, categoria?.NombreCategoria, marca?.NombreMarca);
    }

    public async Task<ArticuloDto> ActualizarArticuloAsync(ActualizarArticuloDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await _actualizarArticuloValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var articulo = await _articuloRepository.GetByIdAsync(dto.IdArticulo, includeDeleted: false, cancellationToken);
        if (articulo == null)
        {
            throw new DomainException($"No se encontró ningún artículo activo con el identificador {dto.IdArticulo}.");
        }

        // Validar unicidad de código de barras no nulo si cambió
        if (!string.IsNullOrWhiteSpace(dto.CodigoBarras))
        {
            var codigoNormalizado = dto.CodigoBarras.Trim();
            var existentes = await _articuloRepository.FindAsync(
                a => a.Id != dto.IdArticulo && a.CodigoBarras == codigoNormalizado,
                includeDeleted: false,
                cancellationToken);

            if (existentes.Count > 0)
            {
                throw new DomainException($"Ya existe otro artículo activo con el código de barras '{codigoNormalizado}'.");
            }
        }

        articulo.ActualizarDatos(
            descripcion: dto.Descripcion,
            idCategoria: dto.IdCategoria,
            idMarca: dto.IdMarca,
            codigoBarras: dto.CodigoBarras,
            costoReposicion: dto.CostoReposicion,
            porcentajeGanancia: dto.PorcentajeGanancia,
            stockActual: dto.StockActual,
            stockMinimo: dto.StockMinimo,
            esServicio: dto.EsServicio,
            idCatalogoProveedor: dto.IdCatalogoProveedor);

        await _articuloRepository.UpdateAsync(articulo, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var categoria = articulo.IdCategoria.HasValue
            ? await _categoriaRepository.GetByIdAsync(articulo.IdCategoria.Value, cancellationToken)
            : null;
        var marca = articulo.IdMarca.HasValue
            ? await _marcaRepository.GetByIdAsync(articulo.IdMarca.Value, cancellationToken)
            : null;

        return MapToDto(articulo, categoria?.NombreCategoria, marca?.NombreMarca);
    }

    public async Task BajaArticuloAsync(int idArticulo, CancellationToken cancellationToken = default)
    {
        if (idArticulo <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idArticulo), "El identificador del artículo debe ser mayor a cero.");
        }

        var articulo = await _articuloRepository.GetByIdAsync(idArticulo, includeDeleted: false, cancellationToken);
        if (articulo == null)
        {
            throw new DomainException($"No se encontró ningún artículo activo con el identificador {idArticulo}.");
        }

        articulo.MarkAsDeleted();
        await _articuloRepository.UpdateAsync(articulo, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlertaStockDto>> ObtenerAlertasStockMinimoAsync(CancellationToken cancellationToken = default)
    {
        // Push-down: filtra solo productos físicos con stock actual menor o igual al mínimo
        var articulosCriticos = await _articuloRepository.FindAsync(
            a => !a.EsServicio && a.StockActual <= a.StockMinimo,
            includeDeleted: false,
            cancellationToken);

        return articulosCriticos
            .OrderBy(a => a.StockActual)
            .Select(a => new AlertaStockDto
            {
                IdArticulo = a.Id,
                CodigoBarras = a.CodigoBarras,
                Descripcion = a.Descripcion,
                StockActual = a.StockActual,
                StockMinimo = a.StockMinimo
            })
            .ToList();
    }

    public async Task ActualizarCostoYPrecioAsync(int idArticulo, decimal nuevoCostoReposicion, CancellationToken cancellationToken = default)
    {
        if (idArticulo <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idArticulo), "El identificador del artículo debe ser mayor a cero.");
        }

        var articulo = await _articuloRepository.GetByIdAsync(idArticulo, includeDeleted: false, cancellationToken);
        if (articulo == null)
        {
            throw new DomainException($"No se encontró ningún artículo activo con el identificador {idArticulo}.");
        }

        articulo.ActualizarCostoYRecalcularPrecio(nuevoCostoReposicion);
        await _articuloRepository.UpdateAsync(articulo, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(CancellationToken cancellationToken = default)
    {
        var categorias = await _categoriaRepository.ListAllAsync(includeDeleted: false, cancellationToken);

        return categorias
            .OrderBy(c => c.NombreCategoria)
            .Select(c => new CategoriaDto
            {
                IdCategoria = c.Id,
                NombreCategoria = c.NombreCategoria
            })
            .ToList();
    }

    public async Task<IReadOnlyList<MarcaDto>> ListarMarcasAsync(CancellationToken cancellationToken = default)
    {
        var marcas = await _marcaRepository.ListAllAsync(includeDeleted: false, cancellationToken);

        return marcas
            .OrderBy(m => m.NombreMarca)
            .Select(m => new MarcaDto
            {
                IdMarca = m.Id,
                NombreMarca = m.NombreMarca
            })
            .ToList();
    }

    private static ArticuloDto MapToDto(Articulo articulo, string? categoriaNombre, string? marcaNombre)
    {
        return new ArticuloDto
        {
            IdArticulo = articulo.Id,
            CodigoBarras = articulo.CodigoBarras,
            Descripcion = articulo.Descripcion,
            IdCategoria = articulo.IdCategoria,
            CategoriaNombre = categoriaNombre ?? "Sin categoría",
            IdMarca = articulo.IdMarca,
            MarcaNombre = marcaNombre ?? "Sin marca",
            IdCatalogoProveedor = articulo.IdCatalogoProveedor,
            CostoReposicion = articulo.CostoReposicion,
            PorcentajeGanancia = articulo.PorcentajeGanancia,
            PrecioVenta = articulo.PrecioVenta,
            StockActual = articulo.StockActual,
            StockMinimo = articulo.StockMinimo,
            EsServicio = articulo.EsServicio
        };
    }

    private static ArticuloVentaDto MapToVentaDto(Articulo articulo)
    {
        return new ArticuloVentaDto
        {
            IdArticulo = articulo.Id,
            CodigoBarras = articulo.CodigoBarras,
            Descripcion = articulo.Descripcion,
            PrecioVenta = articulo.PrecioVenta,
            StockActual = articulo.StockActual,
            EsServicio = articulo.EsServicio
        };
    }
}
