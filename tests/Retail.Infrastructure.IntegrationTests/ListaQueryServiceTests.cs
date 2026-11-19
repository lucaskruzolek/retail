using FluentAssertions;
using Retail.Application.DTOs.Listas;
using Retail.Domain.Entities;
using Retail.Infrastructure.IntegrationTests.TestData;
using Retail.Infrastructure.Persistence.Services;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Consultas de <see cref="ListaQueryService"/> (RF-30) contra SQL Server Express: total con el precio de hoy,
/// artículos dados de baja, ventas de origen, búsqueda insensible a acentos y paginación, todo en SQL (Ley 8).
/// </summary>
public class ListaQueryServiceTests : ListasPruebaBase
{
    public ListaQueryServiceTests()
        : base("ListaQuery")
    {
    }

    [Fact]
    public async Task ListarAsync_CambiaElPrecioDelCatalogo_ElTotalLoSigueSinTocarLaLista()
    {
        // Arrange
        var cuaderno = await SembrarArticuloAsync("Cuaderno rayado", 1000m);
        var lapiz = await SembrarArticuloAsync("Lápiz negro", 200m);
        await SembrarListaAsync("Útiles", (cuaderno, 2), (lapiz, 5));

        var antes = await new ListaQueryService(NuevoContexto()).ListarAsync(new ConsultaListasDto());

        cuaderno.PrecioVenta = 1500m;
        await Contexto.SaveChangesAsync();

        // Act
        var despues = await new ListaQueryService(NuevoContexto()).ListarAsync(new ConsultaListasDto());

        // Assert
        antes.Items.Single().TotalEstimado.Should().Be(3000m);
        despues.Items.Single().TotalEstimado.Should().Be(4000m);
        despues.Items.Single().CantidadArticulos.Should().Be(2);
    }

    [Fact]
    public async Task ObtenerAsync_ArticuloDadoDeBaja_QuedaNoDisponibleYNoSumaAlTotal()
    {
        // Arrange
        var cuaderno = await SembrarArticuloAsync("Cuaderno rayado", 1000m);
        var lapiz = await SembrarArticuloAsync("Lápiz negro", 200m);
        var lista = await SembrarListaAsync("Útiles", (cuaderno, 2), (lapiz, 5));
        lapiz.MarkAsDeleted();
        await Contexto.SaveChangesAsync();

        var servicio = new ListaQueryService(NuevoContexto());

        // Act
        var detalle = await servicio.ObtenerAsync(lista.Id);
        var resumen = (await servicio.ListarAsync(new ConsultaListasDto())).Items.Single();

        // Assert
        detalle.Should().NotBeNull();
        detalle!.Renglones.Should().HaveCount(2);
        detalle.Renglones.Single(r => r.IdArticulo == lapiz.Id).EstaDisponible.Should().BeFalse();
        detalle.Renglones.Single(r => r.IdArticulo == cuaderno.Id).EstaDisponible.Should().BeTrue();
        detalle.TotalEstimado.Should().Be(2000m);
        resumen.CantidadArticulos.Should().Be(1);
        resumen.TotalEstimado.Should().Be(2000m);
    }

    [Fact]
    public async Task ObtenerAsync_ListaDadaDeBaja_DevuelveNull()
    {
        // Arrange
        var articulo = await SembrarArticuloAsync("Cuaderno", 1000m);
        var lista = await SembrarListaAsync("Lista vieja", (articulo, 1));
        lista.MarkAsDeleted();
        await Contexto.SaveChangesAsync();

        // Act
        var detalle = await new ListaQueryService(NuevoContexto()).ObtenerAsync(lista.Id);

        // Assert
        detalle.Should().BeNull();
    }

    [Fact]
    public async Task ListarAsync_ListaConVentasDeOrigen_CuentaLasVentasEnSql()
    {
        // Arrange
        var articulo = await SembrarArticuloAsync("Cuaderno", 1000m);
        var lista = await SembrarListaAsync("Lista escolar", (articulo, 1));
        await SembrarListaAsync("Otra lista", (articulo, 1));
        var idTurno = await SembrarTurnoAsync();
        Contexto.Ventas.Add(Venta.Registrar(idTurno, IdUsuario, null, idListaOrigen: lista.Id));
        Contexto.Ventas.Add(Venta.Registrar(idTurno, IdUsuario, null, idListaOrigen: lista.Id));
        await Contexto.SaveChangesAsync();

        // Act
        var resultado = await new ListaQueryService(NuevoContexto()).ListarAsync(new ConsultaListasDto());

        // Assert
        resultado.Items.Single(i => i.IdLista == lista.Id).CantidadVentas.Should().Be(2);
        resultado.Items.Single(i => i.IdLista != lista.Id).CantidadVentas.Should().Be(0);
    }

    [Fact]
    public async Task ListarAsync_BusquedaSinAcentos_EncuentraElNombreConAcento()
    {
        // Arrange
        var articulo = await SembrarArticuloAsync("Cuaderno", 1000m);
        await SembrarListaAsync("Lista de Música", (articulo, 1));
        await SembrarListaAsync("Lista de Plástica", (articulo, 1));

        // Act
        var resultado = await new ListaQueryService(NuevoContexto())
            .ListarAsync(new ConsultaListasDto { TerminoBusqueda = "musica" });

        // Assert
        resultado.Items.Should().ContainSingle().Which.Nombre.Should().Be("Lista de Música");
        resultado.TotalRegistros.Should().Be(1);
    }

    [Fact]
    public async Task ListarAsync_ConVariasPaginas_PaginaEnSqlYOrdenaPorNombre()
    {
        // Arrange
        var articulo = await SembrarArticuloAsync("Cuaderno", 1000m);
        foreach (var nombre in new[] { "E", "C", "A", "D", "B" })
        {
            await SembrarListaAsync($"Lista {nombre}", (articulo, 1));
        }

        var servicio = new ListaQueryService(NuevoContexto());

        // Act
        var primera = await servicio.ListarAsync(new ConsultaListasDto { Pagina = 1, TamanoPagina = 2 });
        var tercera = await servicio.ListarAsync(new ConsultaListasDto { Pagina = 3, TamanoPagina = 2 });

        // Assert
        primera.TotalRegistros.Should().Be(5);
        primera.TotalPaginas.Should().Be(3);
        primera.Items.Select(i => i.Nombre).Should().Equal("Lista A", "Lista B");
        tercera.Items.Select(i => i.Nombre).Should().Equal("Lista E");
    }
}
