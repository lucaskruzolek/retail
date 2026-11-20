using Retail.Domain.Entities;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Repositories;

namespace Retail.Infrastructure.IntegrationTests.TestData;

/// <summary>
/// Base de las pruebas de integración de listas de artículos (RF-30): reutiliza la base y el usuario de
/// <see cref="PedidosPruebaBase"/> y agrega artículos, listas y turno de caja de ejemplo.
/// </summary>
public abstract class ListasPruebaBase : PedidosPruebaBase
{
    private int _secuenciaArticulos;

    protected ListasPruebaBase(string prefijo)
        : base(prefijo)
    {
    }

    protected static (Repository<ListaArticulos> Repositorio, UnitOfWork UnidadDeTrabajo) NuevoRepositorioDeListas(RetailDbContext contexto)
    {
        return (new Repository<ListaArticulos>(contexto), new UnitOfWork(contexto));
    }

    protected async Task<Articulo> SembrarArticuloAsync(string descripcion, decimal precio)
    {
        var articulo = new Articulo
        {
            Descripcion = descripcion,
            CodigoBarras = $"779200000{++_secuenciaArticulos:D4}",
            CostoReposicion = precio / 2,
            PorcentajeGanancia = 100m,
            PrecioVenta = precio,
            StockActual = 50,
            StockMinimo = 1
        };
        Contexto.Articulos.Add(articulo);
        await Contexto.SaveChangesAsync();
        return articulo;
    }

    protected async Task<ListaArticulos> SembrarListaAsync(string nombre, params (Articulo Articulo, int Cantidad)[] renglones)
    {
        var lista = ListaArticulos.Crear(nombre, IdUsuario, renglones.Select(r => (r.Articulo.Id, r.Cantidad)));
        Contexto.Listas.Add(lista);
        await Contexto.SaveChangesAsync();
        return lista;
    }

    protected async Task<int> SembrarTurnoAsync()
    {
        var idTerminal = await TerminalesDePrueba.SembrarAsync(Contexto);
        var turno = TurnoCaja.Abrir(IdUsuario, idTerminal, saldoInicial: 5000m);
        Contexto.TurnosCaja.Add(turno);
        await Contexto.SaveChangesAsync();
        return turno.Id;
    }
}
