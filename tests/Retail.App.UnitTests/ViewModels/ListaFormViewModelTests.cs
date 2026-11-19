using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.App.ViewModels.Listas;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Listas;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class ListaFormViewModelTests : IDisposable
{
    private const int IdUsuario = 3;
    private const int IdLista = 7;

    private static readonly DateTime FechaLeida = new(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);

    private readonly IListaService _listaService = Substitute.For<IListaService>();
    private readonly IVentaService _ventaService = Substitute.For<IVentaService>();
    private readonly ICurrentUserSession _sesion = Substitute.For<ICurrentUserSession>();
    private readonly CargaSerializada _acceso = new();

    public ListaFormViewModelTests()
    {
        _sesion.IdUsuario.Returns(IdUsuario);
    }

    public void Dispose()
    {
        _acceso.Dispose();
        GC.SuppressFinalize(this);
    }

    private ListaFormViewModel CrearNueva()
    {
        return Crear(null);
    }

    private ListaFormViewModel CrearEdicion(params RenglonListaDto[] renglones)
    {
        return Crear(new ListaDto
        {
            IdLista = IdLista,
            Nombre = "Jardín sala de 5",
            FechaModificacion = FechaLeida,
            Renglones = renglones,
            TotalEstimado = renglones.Where(r => r.EstaDisponible).Sum(r => r.Subtotal)
        });
    }

    private ListaFormViewModel Crear(ListaDto? lista)
    {
        return new ListaFormViewModel(lista, _listaService, _ventaService, _sesion, _acceso, NullLogger.Instance);
    }

    private static RenglonListaDto Renglon(int idArticulo, string descripcion, int cantidad, decimal precio, bool disponible = true)
    {
        return new RenglonListaDto
        {
            IdArticulo = idArticulo,
            CodigoBarras = $"779000{idArticulo}",
            Descripcion = descripcion,
            Cantidad = cantidad,
            PrecioActual = precio,
            Subtotal = cantidad * precio,
            EstaDisponible = disponible
        };
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

    private static ListaDto ListaGuardada()
    {
        return new ListaDto
        {
            IdLista = IdLista,
            Nombre = "guardada",
            FechaModificacion = FechaLeida.AddMinutes(5),
            Renglones = [],
            TotalEstimado = 0m
        };
    }

    // ------------------------------------------------------------------ //
    // Apertura                                                            //
    // ------------------------------------------------------------------ //

    [Fact]
    public void Constructor_ListaNueva_AbreVaciaYConElTituloDeAlta()
    {
        var sut = CrearNueva();

        sut.EsNueva.Should().BeTrue();
        sut.TituloVentana.Should().Be("Nueva lista");
        sut.Nombre.Should().BeEmpty();
        sut.Renglones.Should().BeEmpty();
        sut.TieneErrorNombre.Should().BeFalse("el aviso no aparece antes de que el usuario toque el nombre");
        sut.GuardarCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Constructor_ListaExistente_CargaNombreYRenglones()
    {
        var sut = CrearEdicion(Renglon(1, "Cuaderno", 2, 900m), Renglon(2, "Lápiz", 10, 300m));

        sut.EsNueva.Should().BeFalse();
        sut.TituloVentana.Should().Be("Editar lista");
        sut.Nombre.Should().Be("Jardín sala de 5");
        sut.Renglones.Select(r => r.Descripcion).Should().Equal("Cuaderno", "Lápiz");
        sut.TotalDeHoy.Should().Be(4800m);
    }

    // ------------------------------------------------------------------ //
    // Nombre                                                              //
    // ------------------------------------------------------------------ //

    [Fact]
    public void Nombre_SeBorraDespuesDeEscribirlo_MuestraElAvisoYDeshabilitaGuardar()
    {
        var sut = CrearEdicion(Renglon(1, "Cuaderno", 1, 900m));

        sut.Nombre = string.Empty;

        sut.TieneErrorNombre.Should().BeTrue();
        sut.GuardarCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Guardar_NombreSoloConEspacios_NoSePuedeEjecutar()
    {
        var sut = CrearEdicion(Renglon(1, "Cuaderno", 1, 900m));

        sut.Nombre = "   ";

        sut.GuardarCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Guardar_NombreValidoPeroSinRenglones_NoSePuedeEjecutar()
    {
        var sut = CrearNueva();

        sut.Nombre = "Lista vacía";

        sut.GuardarCommand.CanExecute(null).Should().BeFalse();
    }

    // ------------------------------------------------------------------ //
    // Renglones                                                           //
    // ------------------------------------------------------------------ //

    [Fact]
    public void AgregarArticulo_ArticuloNuevo_EntraConCantidadUnoAlPrecioDeHoy()
    {
        var sut = CrearNueva();

        sut.AgregarArticuloCommand.Execute(Articulo(1, "Cuaderno", 900m));

        var renglon = sut.Renglones.Should().ContainSingle().Subject;
        renglon.IdArticulo.Should().Be(1);
        renglon.Cantidad.Should().Be(1);
        renglon.PrecioActual.Should().Be(900m);
        sut.TotalDeHoy.Should().Be(900m);
    }

    [Fact]
    public void AgregarArticulo_ArticuloYaPresente_SumaUnoALaCantidadSinRepetirElRenglon()
    {
        var sut = CrearEdicion(Renglon(1, "Cuaderno", 2, 900m));

        sut.AgregarArticuloCommand.Execute(Articulo(1, "Cuaderno", 900m));

        sut.Renglones.Should().ContainSingle().Which.Cantidad.Should().Be(3);
        sut.TotalDeHoy.Should().Be(2700m);
    }

    [Fact]
    public void Cantidad_SeModifica_ActualizaElSubtotalYElTotalDeHoy()
    {
        var sut = CrearEdicion(Renglon(1, "Cuaderno", 1, 900m), Renglon(2, "Lápiz", 1, 300m));

        sut.Renglones[1].Cantidad = 10;

        sut.Renglones[1].Subtotal.Should().Be(3000m);
        sut.TotalDeHoy.Should().Be(3900m);
    }

    [Fact]
    public void QuitarRenglon_HayMasDeUno_LoQuita()
    {
        var sut = CrearEdicion(Renglon(1, "Cuaderno", 1, 900m), Renglon(2, "Lápiz", 1, 300m));
        var aQuitar = sut.Renglones[0];

        sut.QuitarRenglonCommand.CanExecute(aQuitar).Should().BeTrue();
        sut.QuitarRenglonCommand.Execute(aQuitar);

        sut.Renglones.Should().ContainSingle().Which.IdArticulo.Should().Be(2);
        sut.TotalDeHoy.Should().Be(300m);
    }

    [Fact]
    public void QuitarRenglon_EsElUltimo_NoSePuedeQuitarYElTooltipLoExplica()
    {
        var sut = CrearEdicion(Renglon(1, "Cuaderno", 1, 900m));
        var ultimo = sut.Renglones.Single();

        sut.QuitarRenglonCommand.CanExecute(ultimo).Should().BeFalse();
        sut.QuitarRenglonCommand.Execute(ultimo);

        sut.Renglones.Should().ContainSingle();
        sut.TextoQuitarRenglon.Should().Be("La lista debe tener al menos un artículo");
    }

    [Fact]
    public void TotalDeHoy_ConUnArticuloDadoDeBaja_NoLoSuma()
    {
        var sut = CrearEdicion(
            Renglon(1, "Cuaderno", 2, 900m),
            Renglon(2, "Lápiz descontinuado", 5, 300m, disponible: false));

        sut.TotalDeHoy.Should().Be(1800m);
        sut.CantidadNoDisponibles.Should().Be(1);
        sut.HayNoDisponibles.Should().BeTrue();
        sut.Renglones[1].EstaDadoDeBaja.Should().BeTrue();
    }

    // ------------------------------------------------------------------ //
    // Buscador                                                            //
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task TextoBusqueda_DosCaracteres_MuestraLosResultadosDelServicioDeVentas()
    {
        _ventaService.BuscarPorTextoAsync("cu", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([Articulo(1, "Cuaderno", 900m), Articulo(2, "Cuadernola", 1200m)]);
        var sut = CrearNueva();

        sut.TextoBusqueda = "cu";

        await EsperarAsync(() => sut.ResultadosBusqueda.Count == 2);
        sut.MostrarResultados.Should().BeTrue();
        sut.ResultadoSeleccionado!.IdArticulo.Should().Be(1);
    }

    [Fact]
    public async Task TextoBusqueda_UnSoloCaracter_NoBuscaYOcultaElDesplegable()
    {
        var sut = CrearNueva();

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
        var sut = CrearNueva();
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
        var sut = CrearNueva();
        sut.TextoBusqueda = "7790001";

        await sut.AgregarResultadoSeleccionadoCommand.ExecuteAsync(null);

        sut.Renglones.Should().ContainSingle().Which.IdArticulo.Should().Be(1);
    }

    [Fact]
    public async Task AgregarResultadoSeleccionado_CodigoInexistente_InformaYNoAgrega()
    {
        _ventaService.BuscarArticuloParaVentaAsync("999", Arg.Any<CancellationToken>()).Returns((ArticuloVentaDto?)null);
        var sut = CrearNueva();
        sut.TextoBusqueda = "999";

        await sut.AgregarResultadoSeleccionadoCommand.ExecuteAsync(null);

        sut.Renglones.Should().BeEmpty();
        sut.MensajeError.Should().Contain("999");
    }

    private static async Task EsperarAsync(Func<bool> condicion)
    {
        for (int i = 0; i < 100 && !condicion(); i++)
        {
            await Task.Delay(20);
        }

        condicion().Should().BeTrue("la condición debía cumplirse tras la carga asíncrona");
    }

    // ------------------------------------------------------------------ //
    // Guardar                                                             //
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task Guardar_ListaNueva_CreaConElNombreNormalizadoElUsuarioYLosRenglones()
    {
        _listaService.CrearAsync(Arg.Any<CrearListaDto>(), Arg.Any<CancellationToken>()).Returns(ListaGuardada());
        var sut = CrearNueva();
        sut.Nombre = "  3.º grado — Colegio San Martín  ";
        sut.AgregarArticuloCommand.Execute(Articulo(1, "Cuaderno", 900m));
        sut.AgregarArticuloCommand.Execute(Articulo(2, "Lápiz", 300m));
        sut.Renglones[1].Cantidad = 12;
        var cerrada = false;
        sut.CierreSolicitado += (_, _) => cerrada = true;

        await sut.GuardarCommand.ExecuteAsync(null);

        await _listaService.Received(1).CrearAsync(
            Arg.Is<CrearListaDto>(d =>
                d.IdUsuario == IdUsuario
                && d.Nombre == "3.º grado — Colegio San Martín"
                && d.Renglones.Count == 2
                && d.Renglones[0].IdArticulo == 1 && d.Renglones[0].Cantidad == 1
                && d.Renglones[1].IdArticulo == 2 && d.Renglones[1].Cantidad == 12),
            Arg.Any<CancellationToken>());
        sut.Guardada.Should().BeTrue();
        cerrada.Should().BeTrue();
    }

    [Fact]
    public async Task Guardar_ListaExistente_ActualizaConLaFechaDeModificacionLeidaAlAbrir()
    {
        _listaService.ActualizarAsync(IdLista, Arg.Any<ActualizarListaDto>(), Arg.Any<CancellationToken>()).Returns(ListaGuardada());
        var sut = CrearEdicion(Renglon(1, "Cuaderno", 2, 900m));
        sut.Nombre = "Jardín sala de 5 (2027)";

        await sut.GuardarCommand.ExecuteAsync(null);

        await _listaService.Received(1).ActualizarAsync(
            IdLista,
            Arg.Is<ActualizarListaDto>(d =>
                d.Nombre == "Jardín sala de 5 (2027)"
                && d.FechaModificacionLeida == FechaLeida
                && d.Renglones.Count == 1 && d.Renglones[0].IdArticulo == 1 && d.Renglones[0].Cantidad == 2),
            Arg.Any<CancellationToken>());
        await _listaService.DidNotReceiveWithAnyArgs().CrearAsync(default!, default);
        sut.Guardada.Should().BeTrue();
    }

    [Fact]
    public async Task Guardar_NombreRepetido_MuestraElMensajeYMantieneElDialogoAbierto()
    {
        _listaService.CrearAsync(Arg.Any<CrearListaDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("Ya existe una lista llamada «Jardín»."));
        var sut = CrearNueva();
        sut.Nombre = "Jardín";
        sut.AgregarArticuloCommand.Execute(Articulo(1, "Cuaderno", 900m));
        var cerrada = false;
        sut.CierreSolicitado += (_, _) => cerrada = true;

        await sut.GuardarCommand.ExecuteAsync(null);

        sut.MensajeError.Should().Be("Ya existe una lista llamada «Jardín».");
        sut.TieneMensajeError.Should().BeTrue();
        sut.Guardada.Should().BeFalse();
        cerrada.Should().BeFalse();
        sut.IsBusy.Should().BeFalse();
        sut.GuardarCommand.CanExecute(null).Should().BeTrue("el usuario puede corregir y reintentar");
    }

    [Fact]
    public async Task Guardar_ModificadaEnOtraTerminal_MuestraElMensajeYMantieneElDialogoAbierto()
    {
        _listaService.ActualizarAsync(IdLista, Arg.Any<ActualizarListaDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("La lista «Jardín sala de 5» fue modificada en otra terminal."));
        var sut = CrearEdicion(Renglon(1, "Cuaderno", 1, 900m));

        await sut.GuardarCommand.ExecuteAsync(null);

        sut.MensajeError.Should().Contain("otra terminal");
        sut.Guardada.Should().BeFalse();
    }

    [Fact]
    public async Task Guardar_ErrorInesperado_MuestraUnMensajeGenericoSinCerrar()
    {
        _listaService.CrearAsync(Arg.Any<CrearListaDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));
        var sut = CrearNueva();
        sut.Nombre = "Lista";
        sut.AgregarArticuloCommand.Execute(Articulo(1, "Cuaderno", 900m));

        await sut.GuardarCommand.ExecuteAsync(null);

        sut.MensajeError.Should().Contain("No se pudo guardar la lista");
        sut.Guardada.Should().BeFalse();
    }

    [Fact]
    public async Task Guardar_SinSesion_NoLlamaAlServicio()
    {
        _sesion.IdUsuario.Returns((int?)null);
        var sut = CrearNueva();
        sut.Nombre = "Lista";
        sut.AgregarArticuloCommand.Execute(Articulo(1, "Cuaderno", 900m));

        await sut.GuardarCommand.ExecuteAsync(null);

        await _listaService.DidNotReceiveWithAnyArgs().CrearAsync(default!, default);
        sut.TieneMensajeError.Should().BeTrue();
        sut.Guardada.Should().BeFalse();
    }
}
