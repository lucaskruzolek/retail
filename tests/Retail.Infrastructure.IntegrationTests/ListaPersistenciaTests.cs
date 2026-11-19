using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Application.Exceptions;
using Retail.Domain.Entities;
using Retail.Infrastructure.IntegrationTests.TestData;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Persistencia del agregado <see cref="ListaArticulos"/> (RF-30) contra SQL Server Express: mapeo, unicidad del
/// nombre (L-10), baja lógica de renglones, concurrencia y lista de origen de la venta (L-09).
/// </summary>
public class ListaPersistenciaTests : ListasPruebaBase
{
    public ListaPersistenciaTests()
        : base("Lista")
    {
    }

    [Fact]
    public async Task Lista_ConRenglones_SePersisteYSeRecupera()
    {
        // Arrange
        var cuaderno = await SembrarArticuloAsync("Cuaderno rayado", 1500m);
        var lapiz = await SembrarArticuloAsync("Lápiz negro", 300m);
        var lista = await SembrarListaAsync("3.º Grado Colegio N", (cuaderno, 2), (lapiz, 10));
        Contexto.ChangeTracker.Clear();

        // Act
        var leida = await Contexto.Listas.Include(l => l.Renglones).SingleAsync(l => l.Id == lista.Id);

        // Assert
        leida.Nombre.Should().Be("3.º Grado Colegio N");
        leida.IdUsuarioCreador.Should().Be(IdUsuario);
        leida.FechaModificacion.Should().Be(lista.FechaModificacion, "la precisión de datetime2 se conserva");
        leida.Renglones.Should().HaveCount(2);
        leida.Renglones.Single(r => r.IdArticulo == lapiz.Id).Cantidad.Should().Be(10);
    }

    [Fact]
    public async Task IndiceNombre_MismoNombreSinAcentosNiMayusculas_LanzaRestriccionDeUnicidad()
    {
        // Arrange
        var articulo = await SembrarArticuloAsync("Cuaderno", 1000m);
        await SembrarListaAsync("Lista Música", (articulo, 1));

        var (repositorio, unidadDeTrabajo) = NuevoRepositorioDeListas(Contexto);
        await repositorio.AddAsync(ListaArticulos.Crear("LISTA MUSICA", IdUsuario, [(articulo.Id, 1)]));

        // Act
        var act = () => unidadDeTrabajo.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<RestriccionDeUnicidadException>();
    }

    [Fact]
    public async Task IndiceNombre_ListaDadaDeBaja_LiberaElNombre()
    {
        // Arrange
        var articulo = await SembrarArticuloAsync("Cuaderno", 1000m);
        var original = await SembrarListaAsync("Lista Música", (articulo, 1));
        original.MarkAsDeleted();
        await Contexto.SaveChangesAsync();

        var (repositorio, unidadDeTrabajo) = NuevoRepositorioDeListas(Contexto);
        await repositorio.AddAsync(ListaArticulos.Crear("Lista Música", IdUsuario, [(articulo.Id, 1)]));

        // Act
        var act = () => unidadDeTrabajo.SaveChangesAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task QuitarRenglon_MarcaBajaLogicaYPermiteVolverAAgregarElArticulo()
    {
        // Arrange
        var cuaderno = await SembrarArticuloAsync("Cuaderno rayado", 1500m);
        var lapiz = await SembrarArticuloAsync("Lápiz negro", 300m);
        var lista = await SembrarListaAsync("Útiles", (cuaderno, 2), (lapiz, 3));
        var idLista = lista.Id;
        Contexto.ChangeTracker.Clear();

        var (repositorio, unidadDeTrabajo) = NuevoRepositorioDeListas(Contexto);
        var cargada = (await repositorio.GetByIdWithIncludesAsync(idLista, default, l => l.Renglones))!;
        cargada.QuitarRenglon(cuaderno.Id);

        // Act
        await repositorio.UpdateAsync(cargada);
        await unidadDeTrabajo.SaveChangesAsync();
        unidadDeTrabajo.DescartarCambios();

        // Assert: la fila sigue (Ley 3) con deleted_at, pero el filtro global la oculta del agregado.
        (await ContarFilasDeRenglonesAsync(idLista)).Should().Be(2);
        (await ContarFilasBorradasAsync(idLista)).Should().Be(1);
        // Sin tracking: un renglón borrado que quedara trackeado se engancharía en el agregado al recargarlo
        // (navigation fixup no aplica el filtro global).
        var borrada = await Contexto.Set<RenglonLista>().IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(r => r.IdListaArticulos == idLista && r.IdArticulo == cuaderno.Id);
        borrada.DeletedAt.Should().NotBeNull();

        var recargada = (await repositorio.GetByIdWithIncludesAsync(idLista, default, l => l.Renglones))!;
        recargada.Renglones.Should().ContainSingle(r => r.IdArticulo == lapiz.Id);

        // El índice único filtrado permite volver a agregar el artículo quitado como un renglón nuevo.
        recargada.AgregarRenglon(cuaderno.Id, 4);
        await repositorio.UpdateAsync(recargada);
        await unidadDeTrabajo.SaveChangesAsync();

        (await ContarFilasDeRenglonesAsync(idLista)).Should().Be(3);
    }

    [Fact]
    public async Task RowVersion_DosContextosEditanLaMismaLista_ElSegundoLanzaConflicto()
    {
        // Arrange
        var articulo = await SembrarArticuloAsync("Cuaderno", 1000m);
        var lista = await SembrarListaAsync("Lista original", (articulo, 1));

        await using var contextoA = NuevoContexto();
        await using var contextoB = NuevoContexto();
        var (repositorioA, unidadA) = NuevoRepositorioDeListas(contextoA);
        var (repositorioB, unidadB) = NuevoRepositorioDeListas(contextoB);
        var listaA = (await repositorioA.GetByIdWithIncludesAsync(lista.Id, default, l => l.Renglones))!;
        var listaB = (await repositorioB.GetByIdWithIncludesAsync(lista.Id, default, l => l.Renglones))!;

        listaA.Renombrar("Cambio de la terminal A");
        await repositorioA.UpdateAsync(listaA);
        await unidadA.SaveChangesAsync();

        listaB.Renombrar("Cambio de la terminal B");
        await repositorioB.UpdateAsync(listaB);

        // Act
        var act = () => unidadB.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<ConflictoDeConcurrenciaException>();
    }

    [Fact]
    public async Task Venta_ConListaDeOrigen_SePersisteYUnaListaSeVendeVariasVeces()
    {
        // Arrange
        var articulo = await SembrarArticuloAsync("Cuaderno", 1000m);
        var lista = await SembrarListaAsync("Lista escolar", (articulo, 1));
        var idTurno = await SembrarTurnoAsync();

        // Act: el índice de id_lista_origen no es único (L-09)
        Contexto.Ventas.Add(Venta.Registrar(idTurno, IdUsuario, null, idListaOrigen: lista.Id));
        Contexto.Ventas.Add(Venta.Registrar(idTurno, IdUsuario, null, idListaOrigen: lista.Id));
        await Contexto.SaveChangesAsync();

        // Assert
        await using var lectura = NuevoContexto();
        var ventas = await lectura.Ventas.AsNoTracking().Where(v => v.IdListaOrigen == lista.Id).ToListAsync();
        ventas.Should().HaveCount(2);
    }

    private async Task<int> ContarFilasDeRenglonesAsync(int idLista)
    {
        // Cuenta las filas físicas, incluso las marcadas con deleted_at: verifica que quitar un renglón no lo borra.
        return await Contexto.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM RENGLONES_LISTA WHERE id_lista = {idLista}")
            .SingleAsync();
    }

    private async Task<int> ContarFilasBorradasAsync(int idLista)
    {
        return await Contexto.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM RENGLONES_LISTA WHERE id_lista = {idLista} AND deleted_at IS NOT NULL")
            .SingleAsync();
    }
}
