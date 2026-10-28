using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Services;
using Retail.App.ViewModels.Articulos;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class AdministrarClasificacionViewModelTests : IDisposable
{
    private const int IdActor = 7;

    private readonly IRubroService _rubroService = Substitute.For<IRubroService>();
    private readonly ICategoriaService _categoriaService = Substitute.For<ICategoriaService>();
    private readonly IMarcaService _marcaService = Substitute.For<IMarcaService>();
    private readonly ITerminoClasificacionService _terminoService = Substitute.For<ITerminoClasificacionService>();
    private readonly IClasificacionDialogService _dialogService = Substitute.For<IClasificacionDialogService>();
    private readonly ICurrentUserSession _session = Substitute.For<ICurrentUserSession>();
    private readonly AdministrarClasificacionViewModel _sut;

    private readonly List<RubroDto> _rubros =
    [
        new() { IdRubro = 1, NombreRubro = "Librería", CantidadEnUso = 2 },
        new() { IdRubro = 2, NombreRubro = "Juguetes", CantidadEnUso = 0 }
    ];

    private readonly List<CategoriaDto> _categoriasRubro1 =
    [
        new() { IdCategoria = 10, NombreCategoria = "Cuadernos", IdRubro = 1, CantidadEnUso = 5 },
        new() { IdCategoria = 11, NombreCategoria = "Lapiceras", IdRubro = 1, CantidadEnUso = 0 }
    ];

    private readonly List<MarcaDto> _marcas =
    [
        new() { IdMarca = 20, NombreMarca = "Bic", CantidadEnUso = 3 }
    ];

    public AdministrarClasificacionViewModelTests()
    {
        _session.IdUsuario.Returns(IdActor);
        _rubroService.ListarAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<RubroDto>>(_rubros));
        _categoriaService.ListarAsync(Arg.Any<string?>(), 1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CategoriaDto>>(_categoriasRubro1));
        _categoriaService.ListarAsync(Arg.Any<string?>(), 2, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CategoriaDto>>([]));
        _marcaService.ListarAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<MarcaDto>>(_marcas));

        _sut = new AdministrarClasificacionViewModel(
            _rubroService, _categoriaService, _marcaService, _terminoService, _dialogService, _session);
    }

    public void Dispose()
    {
        _sut.Dispose();
        GC.SuppressFinalize(this);
    }

    private static async Task EsperarAsync(Func<bool> condicion)
    {
        for (int i = 0; i < 100 && !condicion(); i++)
        {
            await Task.Delay(20);
        }

        condicion().Should().BeTrue("la condición debía cumplirse tras la carga asíncrona");
    }

    [Fact]
    public async Task InicializarAsync_ConRubros_SeleccionaElPrimeroYCargaSusCategoriasConSuIdRubro()
    {
        await _sut.InicializarAsync();

        _sut.Rubros.Should().HaveCount(2);
        _sut.RubroSeleccionado.Should().NotBeNull();
        _sut.RubroSeleccionado!.IdRubro.Should().Be(1);
        _sut.Categorias.Should().HaveCount(2);
        _sut.Marcas.Should().HaveCount(1);
        await _categoriaService.Received(1).ListarAsync(null, 1, Arg.Any<CancellationToken>());
        _sut.NuevaCategoriaCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task RubroSeleccionado_AlCambiar_RecargaLasCategoriasConElNuevoIdRubro()
    {
        await _sut.InicializarAsync();

        _sut.RubroSeleccionado = _sut.Rubros.First(r => r.IdRubro == 2);

        await EsperarAsync(() => _sut.Categorias.Count == 0);
        await _categoriaService.Received().ListarAsync(null, 2, Arg.Any<CancellationToken>());
        _sut.RubroSeleccionado!.IdRubro.Should().Be(2);
    }

    [Fact]
    public async Task FiltroMarca_AlEscribir_ListaMarcasConElFiltroTrasElDebounce()
    {
        await _sut.InicializarAsync();

        _sut.FiltroMarca = "bic";

        await EsperarAsync(() => _marcaService.ReceivedCalls().Any(c => c.GetArguments().FirstOrDefault() is "bic"));
    }

    [Fact]
    public void NuevaCategoria_SinRubroSeleccionado_NoPuedeEjecutarse()
    {
        _sut.NuevaCategoriaCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task NuevaCategoria_AltaExitosa_MarcaHuboCambiosYRecargaRubrosYCategorias()
    {
        await _sut.InicializarAsync();
        _dialogService.AbrirFormularioCategoria(null, Arg.Any<IReadOnlyList<RubroDto>>(), 1).Returns(true);
        _categoriaService.ClearReceivedCalls();

        await _sut.NuevaCategoriaCommand.ExecuteAsync(null);

        _sut.HuboCambios.Should().BeTrue();
        await _categoriaService.Received(1).ListarAsync(null, 1, Arg.Any<CancellationToken>());
        _sut.RubroSeleccionado!.IdRubro.Should().Be(1);
    }

    [Fact]
    public async Task NuevaCategoria_FormularioCancelado_NoMarcaCambiosNiRecarga()
    {
        await _sut.InicializarAsync();
        _dialogService.AbrirFormularioCategoria(null, Arg.Any<IReadOnlyList<RubroDto>>(), 1).Returns(false);
        _categoriaService.ClearReceivedCalls();

        await _sut.NuevaCategoriaCommand.ExecuteAsync(null);

        _sut.HuboCambios.Should().BeFalse();
        await _categoriaService.DidNotReceive().ListarAsync(Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BajaCategoria_Confirmada_LlamaABajaConElActorDeLaSesionYMarcaCambios()
    {
        await _sut.InicializarAsync();
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        var categoria = _sut.Categorias[1];

        await _sut.BajaCategoriaCommand.ExecuteAsync(categoria);

        await _categoriaService.Received(1).BajaAsync(IdActor, 11, Arg.Any<CancellationToken>());
        _sut.HuboCambios.Should().BeTrue();
        _sut.TieneError.Should().BeFalse();
    }

    [Fact]
    public async Task BajaRubro_NoConfirmada_NoLlamaAlServicio()
    {
        await _sut.InicializarAsync();
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await _sut.BajaRubroCommand.ExecuteAsync(_sut.Rubros[0]);

        await _rubroService.DidNotReceive().BajaAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        _sut.HuboCambios.Should().BeFalse();
    }

    [Fact]
    public async Task BajaMarca_EnUso_MuestraElMensajeEnElBannerYNoMarcaCambios()
    {
        await _sut.InicializarAsync();
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _marcaService.BajaAsync(IdActor, 20, Arg.Any<CancellationToken>())
            .ThrowsAsync(new ClasificacionEnUsoException("la marca", "Bic", 3, "artículos"));

        await _sut.BajaMarcaCommand.ExecuteAsync(_sut.Marcas[0]);

        _sut.TieneError.Should().BeTrue();
        _sut.MensajeError.Should().Contain("Bic").And.Contain("3 artículos");
        _sut.HuboCambios.Should().BeFalse();
    }

    [Fact]
    public async Task InicializarAsync_ConMarcas_SeleccionaLaPrimeraSinCargarSuDetalleConLaSeccionOculta()
    {
        await _sut.InicializarAsync();

        _sut.MarcaSeleccionada.Should().BeSameAs(_marcas[0]);
        await _marcaService.DidNotReceiveWithAnyArgs().ListarAliasAsync(default, default, default);
    }

    [Fact]
    public async Task MostrandoMarcas_ConMarcaSeleccionada_CargaElDetalleDeEsaMarca()
    {
        _marcaService.ListarAliasAsync(Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Retail.Application.DTOs.Clasificacion.AliasListadoDto>>(
                [new() { IdMarca = 20, NombreMarca = "Bic", Alias = "BIC CRISTAL" }]));
        _marcaService.ListarPrefijosAsync(Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Retail.Application.DTOs.Clasificacion.PrefijoListadoDto>>([]));
        await _sut.InicializarAsync();

        _sut.MostrandoMarcas = true;

        await EsperarAsync(() => _sut.DetalleMarca.Alias.Count == 1);
        _sut.DetalleMarca.Marca.Should().BeSameAs(_marcas[0]);
        await _marcaService.Received().ListarAliasAsync(null, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CargarMarcas_AlRecargar_ConservaLaMarcaSeleccionadaPorId()
    {
        var otra = new MarcaDto { IdMarca = 21, NombreMarca = "Faber" };
        _marcaService.ListarAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<IReadOnlyList<MarcaDto>>([_marcas[0], otra]),
                Task.FromResult<IReadOnlyList<MarcaDto>>([_marcas[0], new MarcaDto { IdMarca = 21, NombreMarca = "Faber-Castell" }]));
        _dialogService.AbrirFormularioMarca(Arg.Any<MarcaDto?>()).Returns(true);
        await _sut.InicializarAsync();
        _sut.MarcaSeleccionada = _sut.Marcas[1];

        await _sut.EditarMarcaCommand.ExecuteAsync(_sut.Marcas[1]);

        _sut.MarcaSeleccionada!.IdMarca.Should().Be(21);
        _sut.MarcaSeleccionada.NombreMarca.Should().Be("Faber-Castell");
    }

    [Fact]
    public void Secciones_AlAlternar_SonMutuamenteExcluyentes()
    {
        _sut.MostrandoRubros.Should().BeTrue();
        _sut.MostrandoMarcas.Should().BeFalse();

        _sut.MostrandoMarcas = true;

        _sut.MostrandoRubros.Should().BeFalse();
        _sut.MostrandoMarcas.Should().BeTrue();

        _sut.MostrandoRubros = true;

        _sut.MostrandoMarcas.Should().BeFalse();
        _sut.MostrandoRubros.Should().BeTrue();
    }

    [Fact]
    public void Comandos_ConCargaEnCurso_NoSePuedenEjecutar()
    {
        // Arrange: el diálogo comparte el DbContext de la pantalla; ninguna escritura puede solaparse con una carga.
        _sut.RubroSeleccionado = new RubroDto { IdRubro = 1, NombreRubro = "Librería" };

        // Act
        _sut.IsBusy = true;

        // Assert
        _sut.NuevoRubroCommand.CanExecute(null).Should().BeFalse();
        _sut.BajaRubroCommand.CanExecute(null).Should().BeFalse();
        _sut.NuevaCategoriaCommand.CanExecute(null).Should().BeFalse();
        _sut.EditarCategoriaCommand.CanExecute(null).Should().BeFalse();
        _sut.NuevaMarcaCommand.CanExecute(null).Should().BeFalse();
        _sut.BajaMarcaCommand.CanExecute(null).Should().BeFalse();

        _sut.IsBusy = false;
        _sut.NuevoRubroCommand.CanExecute(null).Should().BeTrue();
        _sut.NuevaCategoriaCommand.CanExecute(null).Should().BeTrue();
    }
}
