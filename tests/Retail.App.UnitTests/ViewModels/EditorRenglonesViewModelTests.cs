using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Retail.App.Helpers;
using Retail.App.ViewModels.Comun;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Interfaces.Services;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class EditorRenglonesViewModelTests : IDisposable
{
    private readonly IVentaService _ventaService = Substitute.For<IVentaService>();
    private readonly CargaSerializada _acceso = new();

    public void Dispose()
    {
        _acceso.Dispose();
        GC.SuppressFinalize(this);
    }

    private EditorRenglonesViewModel Crear(bool exigirAlMenosUnRenglon = true, params RenglonEditableFila[] renglones)
    {
        var sut = new EditorRenglonesViewModel(
            _ventaService,
            _acceso,
            NullLogger.Instance,
            exigirAlMenosUnRenglon,
            textoQuitar: "Quitar",
            textoUltimoRenglon: "No se puede quitar el último");
        sut.CargarRenglones(renglones);
        return sut;
    }

    private static RenglonEditableFila Fila(int idArticulo, string descripcion, int cantidad, decimal precio, bool disponible = true)
    {
        return new RenglonEditableFila(idArticulo, $"779000{idArticulo}", descripcion, precio, cantidad, disponible);
    }

    private static ArticuloVentaDto Articulo(int idArticulo, string descripcion, decimal precio)
    {
        return new ArticuloVentaDto
        {
            IdArticulo = idArticulo,
            CodigoBarras = $"779000{idArticulo}",
            Descripcion = descripcion,
            PrecioVenta = precio,
            StockActual = 50,
            EsServicio = false
        };
    }

    // ------------------------------------------------------------------ //
    // Renglones                                                           //
    // ------------------------------------------------------------------ //

    [Fact]
    public void AgregarArticulo_ArticuloNuevo_EntraConCantidadUnoAlPrecioDelArticulo()
    {
        var sut = Crear();

        sut.AgregarArticuloCommand.Execute(Articulo(1, "Cuaderno", 900m));

        var renglon = sut.Renglones.Should().ContainSingle().Subject;
        renglon.IdArticulo.Should().Be(1);
        renglon.Cantidad.Should().Be(1);
        renglon.PrecioUnitario.Should().Be(900m);
        sut.Subtotal.Should().Be(900m);
        sut.SinRenglones.Should().BeFalse();
    }

    [Fact]
    public void AgregarArticulo_ArticuloYaPresente_SumaUnoALaCantidadSinRepetirElRenglon()
    {
        var sut = Crear(true, Fila(1, "Cuaderno", 2, 900m));

        sut.AgregarArticuloCommand.Execute(Articulo(1, "Cuaderno", 900m));

        sut.Renglones.Should().ContainSingle().Which.Cantidad.Should().Be(3);
        sut.Subtotal.Should().Be(2700m);
    }

    [Fact]
    public void Cantidad_SeModifica_ActualizaElSubtotalYAvisaAlAnfitrion()
    {
        var sut = Crear(true, Fila(1, "Cuaderno", 1, 900m), Fila(2, "Lápiz", 1, 300m));
        var avisos = 0;
        sut.ContenidoCambiado += (_, _) => avisos++;

        sut.Renglones[1].Cantidad = 10;

        sut.Renglones[1].Subtotal.Should().Be(3000m);
        sut.Subtotal.Should().Be(3900m);
        avisos.Should().BeGreaterThan(0);
    }

    [Fact]
    public void QuitarRenglon_HayMasDeUno_LoQuitaYAvisaAlAnfitrion()
    {
        var sut = Crear(true, Fila(1, "Cuaderno", 1, 900m), Fila(2, "Lápiz", 1, 300m));
        var aQuitar = sut.Renglones[0];
        var avisos = 0;
        sut.ContenidoCambiado += (_, _) => avisos++;

        sut.QuitarRenglonCommand.CanExecute(aQuitar).Should().BeTrue();
        sut.QuitarRenglonCommand.Execute(aQuitar);

        sut.Renglones.Should().ContainSingle().Which.IdArticulo.Should().Be(2);
        sut.Subtotal.Should().Be(300m);
        avisos.Should().BeGreaterThan(0);
    }

    [Fact]
    public void QuitarRenglon_EsElUltimoYSeExigeUno_NoSePuedeQuitarYElTooltipLoExplica()
    {
        var sut = Crear(true, Fila(1, "Cuaderno", 1, 900m));
        var ultimo = sut.Renglones.Single();

        sut.QuitarRenglonCommand.CanExecute(ultimo).Should().BeFalse();
        sut.QuitarRenglonCommand.Execute(ultimo);

        sut.Renglones.Should().ContainSingle();
        sut.TextoQuitarRenglon.Should().Be("No se puede quitar el último");
    }

    [Fact]
    public void QuitarRenglon_EsElUltimoPeroNoSeExigeUno_LoQuita()
    {
        var sut = Crear(false, Fila(1, "Cuaderno", 1, 900m));
        var ultimo = sut.Renglones.Single();

        sut.QuitarRenglonCommand.CanExecute(ultimo).Should().BeTrue();
        sut.QuitarRenglonCommand.Execute(ultimo);

        sut.Renglones.Should().BeEmpty();
        sut.SinRenglones.Should().BeTrue();
        sut.TextoQuitarRenglon.Should().Be("Quitar");
    }

    [Fact]
    public void Subtotal_ConUnArticuloDadoDeBaja_NoLoSumaYLoInforma()
    {
        var sut = Crear(true, Fila(1, "Cuaderno", 2, 900m), Fila(2, "Lápiz descontinuado", 5, 300m, disponible: false));

        sut.Subtotal.Should().Be(1800m);
        sut.CantidadNoDisponibles.Should().Be(1);
        sut.HayNoDisponibles.Should().BeTrue();
        sut.TextoNoDisponibles.Should().StartWith("1 artículo dado de baja");
        sut.Renglones[1].EstaDadoDeBaja.Should().BeTrue();
    }

    [Fact]
    public void CargarRenglones_YaHabiaRenglones_LosReemplaza()
    {
        var sut = Crear(true, Fila(1, "Cuaderno", 1, 900m));

        sut.CargarRenglones([Fila(2, "Lápiz", 3, 300m)]);

        sut.Renglones.Should().ContainSingle().Which.IdArticulo.Should().Be(2);
        sut.Subtotal.Should().Be(900m);
    }

    [Fact]
    public void ActualizarPrecios_ArticulosConPrecioNuevo_CambiaSoloEsosYRecalculaElSubtotal()
    {
        var sut = Crear(true, Fila(1, "Cuaderno", 2, 900m), Fila(2, "Lápiz", 10, 300m), Fila(3, "Goma", 1, 100m));
        var avisos = 0;
        sut.ContenidoCambiado += (_, _) => avisos++;

        sut.ActualizarPrecios(new Dictionary<int, decimal> { [1] = 1000m, [3] = 150m });

        sut.Renglones.Select(r => r.PrecioUnitario).Should().Equal(1000m, 300m, 150m);
        sut.Renglones[0].Subtotal.Should().Be(2000m);
        sut.Subtotal.Should().Be(2000m + 3000m + 150m);
        avisos.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ActualizarPrecios_ArticuloDadoDeBaja_CambiaElPrecioPeroNoSuma()
    {
        var sut = Crear(true, Fila(1, "Cuaderno", 1, 900m), Fila(2, "Lápiz descontinuado", 5, 300m, disponible: false));

        sut.ActualizarPrecios(new Dictionary<int, decimal> { [2] = 400m });

        sut.Renglones[1].Subtotal.Should().Be(2000m);
        sut.Subtotal.Should().Be(900m);
    }

    // ------------------------------------------------------------------ //
    // Buscador                                                            //
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task TextoBusqueda_DosCaracteres_MuestraLosResultadosDelServicioDeVentas()
    {
        _ventaService.BuscarPorTextoAsync("cu", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([Articulo(1, "Cuaderno", 900m), Articulo(2, "Cuadernola", 1200m)]);
        var sut = Crear();

        sut.TextoBusqueda = "cu";

        await EsperarAsync(() => sut.ResultadosBusqueda.Count == 2);
        sut.MostrarResultados.Should().BeTrue();
        sut.ResultadoSeleccionado!.IdArticulo.Should().Be(1);
    }

    [Fact]
    public async Task TextoBusqueda_UnSoloCaracter_NoBuscaYOcultaElDesplegable()
    {
        var sut = Crear();

        sut.TextoBusqueda = "c";
        await Task.Delay(400);

        sut.MostrarResultados.Should().BeFalse();
        await _ventaService.DidNotReceiveWithAnyArgs().BuscarPorTextoAsync(default!, default, default);
    }

    [Fact]
    public async Task AgregarResultadoSeleccionado_ConResultadosAbiertos_AgregaElResaltadoYLimpiaElBuscador()
    {
        _ventaService.BuscarPorTextoAsync("cu", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([Articulo(1, "Cuaderno", 900m)]);
        var sut = Crear();
        sut.TextoBusqueda = "cu";
        await EsperarAsync(() => sut.MostrarResultados);

        await sut.AgregarResultadoSeleccionadoCommand.ExecuteAsync(null);

        sut.Renglones.Should().ContainSingle().Which.IdArticulo.Should().Be(1);
        sut.TextoBusqueda.Should().BeEmpty();
    }

    [Fact]
    public async Task AgregarResultadoSeleccionado_CodigoEscaneadoSinResultadosTodavia_ResuelveElArticuloPorCodigo()
    {
        _ventaService.BuscarArticuloParaVentaAsync("7790001", Arg.Any<CancellationToken>()).Returns(Articulo(1, "Cuaderno", 900m));
        var sut = Crear();
        sut.TextoBusqueda = "7790001";

        await sut.AgregarResultadoSeleccionadoCommand.ExecuteAsync(null);

        sut.Renglones.Should().ContainSingle().Which.IdArticulo.Should().Be(1);
    }

    [Fact]
    public async Task AgregarResultadoSeleccionado_CodigoInexistente_EmiteElAvisoYNoAgrega()
    {
        _ventaService.BuscarArticuloParaVentaAsync("999", Arg.Any<CancellationToken>()).Returns((ArticuloVentaDto?)null);
        var sut = Crear();
        string? mensaje = null;
        sut.MensajeEmitido += (_, texto) => mensaje = texto;
        sut.TextoBusqueda = "999";

        await sut.AgregarResultadoSeleccionadoCommand.ExecuteAsync(null);

        sut.Renglones.Should().BeEmpty();
        mensaje.Should().Contain("999");
    }

    [Fact]
    public void AgregarArticulo_DespuesDeUnAviso_LimpiaElAviso()
    {
        var sut = Crear();
        string? mensaje = "aviso anterior";
        sut.MensajeEmitido += (_, texto) => mensaje = texto;

        sut.AgregarArticuloCommand.Execute(Articulo(1, "Cuaderno", 900m));

        mensaje.Should().BeNull();
    }

    private static async Task EsperarAsync(Func<bool> condicion)
    {
        for (int i = 0; i < 100 && !condicion(); i++)
        {
            await Task.Delay(20);
        }

        condicion().Should().BeTrue("la condición debía cumplirse tras la carga asíncrona");
    }
}
