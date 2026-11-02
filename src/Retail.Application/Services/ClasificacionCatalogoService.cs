using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Application.Services.Clasificacion;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Clasificación pasiva del catálogo de un proveedor (Track 6.6, Fase 8; M-72 a M-77). Usa las mismas reglas que la
/// sugerencia al incorporar, a través de <see cref="MotorDeClasificacion"/>, pero escribe el resultado en cada ítem
/// en lugar de mostrarlo. Lee el lote y sus diccionarios una vez y guarda con una sola unidad de trabajo, como el
/// importador.
/// </summary>
public class ClasificacionCatalogoService : IClasificacionCatalogoService
{
    private readonly IRepository<Proveedor> _proveedorRepository;
    private readonly ICatalogoProveedorQueryService _catalogo;
    private readonly IDiccionariosClasificacionQueryService _diccionarios;
    private readonly IUnitOfWork _unitOfWork;

    public ClasificacionCatalogoService(
        IRepository<Proveedor> proveedorRepository,
        ICatalogoProveedorQueryService catalogo,
        IDiccionariosClasificacionQueryService diccionarios,
        IUnitOfWork unitOfWork)
    {
        _proveedorRepository = proveedorRepository ?? throw new ArgumentNullException(nameof(proveedorRepository));
        _catalogo = catalogo ?? throw new ArgumentNullException(nameof(catalogo));
        _diccionarios = diccionarios ?? throw new ArgumentNullException(nameof(diccionarios));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<IReadOnlyList<ValorSinTraducirDto>> ObtenerValoresSinTraducirAsync(int idProveedor, CancellationToken cancellationToken = default)
    {
        var valores = await _catalogo.ObtenerValoresDeClasificacionAsync(idProveedor, cancellationToken);
        if (valores.Count == 0)
        {
            return [];
        }

        // Solo se consultan las equivalencias y las marcas del comercio: sin ítems no hay términos ni prefijos.
        var diccionarios = new DiccionariosDelLote(
            await _diccionarios.ObtenerParaLoteAsync(idProveedor, [], [], cancellationToken));

        var sinTraducir = new Dictionary<(TipoEquivalenciaEnum Tipo, string? Rubro, string Texto), int>();
        foreach (var valor in valores)
        {
            // Se corre la misma regla del motor sobre un ítem que solo trae ese valor; si queda sin traducir, el valor
            // cuenta tantas veces como ítems lo traen (el motor cuenta de a uno).
            var delValor = new Dictionary<(TipoEquivalenciaEnum, string?, string), int>();
            var item = new ItemAClasificar(0, string.Empty, string.Empty, string.Empty, [], null, valor.Rubro, null, null);
            if (valor.Tipo == TipoEquivalenciaEnum.Marca)
            {
                MotorDeClasificacion.SugerirMarcaDelProveedor(item with { MarcaProveedor = valor.Texto }, diccionarios, delValor);
            }
            else
            {
                MotorDeClasificacion.SugerirCategoriaDelProveedor(item with { CategoriaProveedor = valor.Texto }, diccionarios, delValor);
            }

            foreach (var (clave, cantidad) in delValor)
            {
                sinTraducir[clave] = sinTraducir.GetValueOrDefault(clave) + (cantidad * valor.CantidadDeItems);
            }
        }

        return sinTraducir
            .Select(v => new ValorSinTraducirDto { Tipo = v.Key.Tipo, TextoRubro = v.Key.Rubro, Texto = v.Key.Texto, CantidadDeItems = v.Value })
            .OrderByDescending(v => v.CantidadDeItems)
            .ThenBy(v => v.Texto, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<ResultadoClasificacionCatalogoDto> ClasificarAsync(int idProveedor, bool soloPendientes, CancellationToken cancellationToken = default)
    {
        var proveedor = await _proveedorRepository.GetByIdAsync(idProveedor, includeDeleted: false, cancellationToken)
            ?? throw new DomainException($"No se encontró el proveedor con ID {idProveedor}.");

        // Antes de cargar los ítems, como en el importador: Update recorre el grafo del proveedor y marcaría como
        // modificadas todas las columnas de los ítems ya enganchados en Catalogos.
        if (!soloPendientes)
        {
            proveedor.ConfigurarClasificacionDelCatalogo(true);
            await _proveedorRepository.UpdateAsync(proveedor, cancellationToken);
        }

        // Entidades con tracking: el ChangeTracker detecta lo que RegistrarClasificacionSugerida modifica.
        var catalogos = await _catalogo.ObtenerParaClasificarAsync(idProveedor, soloPendientes, cancellationToken);
        var items = catalogos.Select(ItemAClasificar.Desde).ToList();

        var conCategoria = 0;
        var conMarca = 0;

        try
        {
            if (items.Count > 0)
            {
                var diccionarios = new DiccionariosDelLote(await _diccionarios.ObtenerParaLoteAsync(
                    idProveedor,
                    items.Select(i => i.Termino).Where(t => t.Length > 0).Distinct().ToList(),
                    items.Select(i => i.PrefijoTitular).OfType<string>().Distinct().ToList(),
                    cancellationToken));

                // Los valores sin traducir no se usan acá: ya se ofrecieron antes de clasificar (M-76).
                var sinTraducir = new Dictionary<(TipoEquivalenciaEnum, string?, string), int>();

                for (var i = 0; i < items.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var marca = MotorDeClasificacion.SugerirMarca(items[i], diccionarios, sinTraducir);
                    var categoria = MotorDeClasificacion.ClasificarCategoria(items[i], diccionarios, sinTraducir).Sugerencia;

                    // Pasiva (M-73): lo que ninguna regla resuelve queda fechado y sin categoría (M-74).
                    catalogos[i].RegistrarClasificacionSugerida(categoria?.Id, marca?.Id);
                    conCategoria += categoria is null ? 0 : 1;
                    conMarca += marca is null ? 0 : 1;
                }
            }
        }
        catch
        {
            // Cancelación o falla a mitad del lote: no se deja una clasificación a medias en el contexto compartido.
            _unitOfWork.DescartarCambios();
            throw;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ResultadoClasificacionCatalogoDto
        {
            ItemsClasificados = items.Count,
            ConCategoria = conCategoria,
            ConMarca = conMarca,
            SinCategoria = items.Count - conCategoria
        };
    }
}
