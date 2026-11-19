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
        sut.Editor.Renglones.Should().BeEmpty();
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
        sut.Editor.Renglones.Select(r => r.Descripcion).Should().Equal("Cuaderno", "Lápiz");
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
    // Renglones (la lógica del editor se prueba en EditorRenglonesViewModelTests)
    // ------------------------------------------------------------------ //

    [Fact]
    public void Editor_SeAgregaUnArticulo_ActualizaElTotalDeHoyYHabilitaGuardar()
    {
        var sut = CrearNueva();
        sut.Nombre = "Lista";

        sut.Editor.AgregarArticuloCommand.Execute(Articulo(1, "Cuaderno", 900m));

        sut.TotalDeHoy.Should().Be(900m);
        sut.GuardarCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void Editor_CantidadSeModifica_ActualizaElTotalDeHoy()
    {
        var sut = CrearEdicion(Renglon(1, "Cuaderno", 1, 900m), Renglon(2, "Lápiz", 1, 300m));

        sut.Editor.Renglones[1].Cantidad = 10;

        sut.TotalDeHoy.Should().Be(3900m);
    }

    [Fact]
    public void Editor_EsElUltimoRenglon_NoSePuedeQuitarYElTooltipLoExplica()
    {
        var sut = CrearEdicion(Renglon(1, "Cuaderno", 1, 900m));
        var ultimo = sut.Editor.Renglones.Single();

        sut.Editor.QuitarRenglonCommand.CanExecute(ultimo).Should().BeFalse();
        sut.Editor.QuitarRenglonCommand.Execute(ultimo);

        sut.Editor.Renglones.Should().ContainSingle();
        sut.Editor.TextoQuitarRenglon.Should().Be("La lista debe tener al menos un artículo");
    }

    [Fact]
    public void TotalDeHoy_ConUnArticuloDadoDeBaja_NoLoSuma()
    {
        var sut = CrearEdicion(
            Renglon(1, "Cuaderno", 2, 900m),
            Renglon(2, "Lápiz descontinuado", 5, 300m, disponible: false));

        sut.TotalDeHoy.Should().Be(1800m);
        sut.Editor.CantidadNoDisponibles.Should().Be(1);
        sut.Editor.Renglones[1].EstaDadoDeBaja.Should().BeTrue();
    }

    [Fact]
    public async Task Editor_CodigoInexistente_MuestraElAvisoEnElDialogo()
    {
        _ventaService.BuscarArticuloParaVentaAsync("999", Arg.Any<CancellationToken>()).Returns((ArticuloVentaDto?)null);
        var sut = CrearNueva();
        sut.Editor.TextoBusqueda = "999";

        await sut.Editor.AgregarResultadoSeleccionadoCommand.ExecuteAsync(null);

        sut.MensajeError.Should().Contain("999");
        sut.TieneMensajeError.Should().BeTrue();
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
        sut.Editor.AgregarArticuloCommand.Execute(Articulo(1, "Cuaderno", 900m));
        sut.Editor.AgregarArticuloCommand.Execute(Articulo(2, "Lápiz", 300m));
        sut.Editor.Renglones[1].Cantidad = 12;
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
        sut.Editor.AgregarArticuloCommand.Execute(Articulo(1, "Cuaderno", 900m));
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
        sut.Editor.AgregarArticuloCommand.Execute(Articulo(1, "Cuaderno", 900m));

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
        sut.Editor.AgregarArticuloCommand.Execute(Articulo(1, "Cuaderno", 900m));

        await sut.GuardarCommand.ExecuteAsync(null);

        await _listaService.DidNotReceiveWithAnyArgs().CrearAsync(default!, default);
        sut.TieneMensajeError.Should().BeTrue();
        sut.Guardada.Should().BeFalse();
    }
}
