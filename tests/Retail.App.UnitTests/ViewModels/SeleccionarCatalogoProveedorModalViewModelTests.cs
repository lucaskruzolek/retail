using FluentAssertions;
using NSubstitute;
using Retail.App.ViewModels.Articulos;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

/// <summary>
/// Reglas del selector de "Agregar fuente" (Track 4.3, A-19): ocultar lo que el formulario ya tiene, deshabilitar lo
/// que es fuente de otro artículo (A-1) y admitir lo que el usuario quitó sin guardar (M6).
/// </summary>
public class SeleccionarCatalogoProveedorModalViewModelTests
{
    private const int IdArticuloActual = 7;

    private readonly IProveedorService _proveedorService = Substitute.For<IProveedorService>();

    private static CatalogoProveedorDto Item(
        int id,
        bool estaVinculado = false,
        int? idArticuloVinculado = null,
        string? nombreArticulo = null)
    {
        return new CatalogoProveedorDto
        {
            Id = id,
            IdProveedor = 1,
            ProveedorRazonSocial = "Distribuidora Norte",
            CodigoProveedor = $"COD-{id}",
            DescripcionProveedor = $"Ítem {id}",
            CostoReposicion = 100m,
            FechaActualizacion = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
            EstaVinculado = estaVinculado,
            IdArticuloVinculado = idArticuloVinculado,
            NombreArticuloTienda = nombreArticulo
        };
    }

    private SeleccionarCatalogoProveedorModalViewModel CrearSut(params CatalogoProveedorDto[] items)
    {
        _proveedorService.ListarProveedoresAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ProveedorDto>());
        _proveedorService.ListarItemsCatalogoAsync(Arg.Any<ConsultaCatalogoProveedorDto>(), Arg.Any<CancellationToken>())
            .Returns(new CatalogoPaginadoDto { Items = items, TotalRegistros = items.Length });
        return new SeleccionarCatalogoProveedorModalViewModel(_proveedorService);
    }

    private static ContextoSelectorFuente Contexto(int idArticulo = IdArticuloActual, params int[] idsEnFormulario)
    {
        return new ContextoSelectorFuente(idArticulo, "Cuaderno A4", "7790001112223", idsEnFormulario);
    }

    [Fact]
    public async Task InicializarAsync_ConItemsYaEnElFormulario_NoLosMuestra()
    {
        var sut = CrearSut(Item(1), Item(2), Item(3));

        await sut.InicializarAsync(Contexto(IdArticuloActual, 2));

        sut.ItemsCatalogo.Select(f => f.Item.Id).Should().Equal(1, 3);
    }

    [Fact]
    public async Task InicializarAsync_ConItemDeOtroArticulo_LoMuestraDeshabilitadoConElNombreDelDueno()
    {
        var sut = CrearSut(Item(1, estaVinculado: true, idArticuloVinculado: 99, nombreArticulo: "Lapicera Bic"));

        await sut.InicializarAsync(Contexto());

        var fila = sut.ItemsCatalogo.Should().ContainSingle().Subject;
        fila.EsSeleccionable.Should().BeFalse();
        fila.TextoEstado.Should().Be("Fuente de: Lapicera Bic");
    }

    [Fact]
    public async Task InicializarAsync_ConItemDeOtroArticuloSinNombre_UsaElTextoGenerico()
    {
        var sut = CrearSut(Item(1, estaVinculado: true, idArticuloVinculado: 99));

        await sut.InicializarAsync(Contexto());

        sut.ItemsCatalogo.Single().TextoEstado.Should().Be("Fuente de otro artículo");
    }

    [Fact]
    public async Task InicializarAsync_ConItemVinculadoAEsteArticulo_LoMuestraSeleccionable()
    {
        var sut = CrearSut(Item(1, estaVinculado: true, idArticuloVinculado: IdArticuloActual, nombreArticulo: "Cuaderno A4"));

        await sut.InicializarAsync(Contexto());

        var fila = sut.ItemsCatalogo.Single();
        fila.EsSeleccionable.Should().BeTrue();
        fila.TextoEstado.Should().Be("Disponible");
    }

    [Fact]
    public async Task InicializarAsync_EnElAltaConItemVinculado_NuncaLoHaceSeleccionable()
    {
        var sut = CrearSut(Item(1, estaVinculado: true, idArticuloVinculado: 0));

        await sut.InicializarAsync(Contexto(idArticulo: 0));

        sut.ItemsCatalogo.Single().EsSeleccionable.Should().BeFalse();
    }

    [Fact]
    public async Task InicializarAsync_ConFilasMezcladas_PreseleccionaLaPrimeraSeleccionable()
    {
        var sut = CrearSut(
            Item(1, estaVinculado: true, idArticuloVinculado: 99),
            Item(2),
            Item(3));

        await sut.InicializarAsync(Contexto());

        sut.ItemSeleccionado.Should().NotBeNull();
        sut.ItemSeleccionado!.Id.Should().Be(2);
        sut.HayItemSeleccionado.Should().BeTrue();
    }

    [Fact]
    public async Task InicializarAsync_SinFilasSeleccionables_NoPreselecciona()
    {
        var sut = CrearSut(Item(1, estaVinculado: true, idArticuloVinculado: 99));

        await sut.InicializarAsync(Contexto());

        sut.FilaSeleccionada.Should().BeNull();
        sut.HayItemSeleccionado.Should().BeFalse();
    }

    [Fact]
    public async Task Seleccionar_ConFilaNoSeleccionable_NoCierraYInformaElError()
    {
        var sut = CrearSut(Item(1, estaVinculado: true, idArticuloVinculado: 99, nombreArticulo: "Lapicera Bic"));
        await sut.InicializarAsync(Contexto());
        sut.FilaSeleccionada = sut.ItemsCatalogo.Single();

        sut.SeleccionarCommand.Execute(null);

        sut.DialogResult.Should().BeFalse();
        sut.MensajeError.Should().Contain("Lapicera Bic");
    }

    [Fact]
    public async Task Seleccionar_SinSeleccion_NoCierraYInformaElError()
    {
        var sut = CrearSut();
        await sut.InicializarAsync(Contexto());

        sut.SeleccionarCommand.Execute(null);

        sut.DialogResult.Should().BeFalse();
        sut.MensajeError.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Seleccionar_ConFilaSeleccionable_ConfirmaYExponeElItem()
    {
        var sut = CrearSut(Item(5));
        await sut.InicializarAsync(Contexto());

        sut.SeleccionarCommand.Execute(null);

        sut.DialogResult.Should().BeTrue();
        sut.ItemSeleccionado!.Id.Should().Be(5);
    }

    [Fact]
    public async Task InicializarAsync_ConContexto_ExponeLaTarjetaDelArticulo()
    {
        var sut = CrearSut();

        await sut.InicializarAsync(new ContextoSelectorFuente(0, "  ", null, []));

        sut.TituloArticulo.Should().Be("Artículo nuevo");
        sut.DescripcionArticulo.Should().Be("(sin descripción todavía)");
        sut.TieneCodigoBarrasArticulo.Should().BeFalse();
    }
}
