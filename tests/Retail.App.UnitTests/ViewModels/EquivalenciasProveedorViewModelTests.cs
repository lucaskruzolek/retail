using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Services;
using Retail.App.ViewModels.Proveedores;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class EquivalenciasProveedorViewModelTests : IDisposable
{
    private const int IdActor = 7;
    private const int IdProveedor = 3;

    private readonly IEquivalenciaClasificacionService _equivalenciaService = Substitute.For<IEquivalenciaClasificacionService>();
    private readonly ICategoriaService _categoriaService = Substitute.For<ICategoriaService>();
    private readonly IMarcaService _marcaService = Substitute.For<IMarcaService>();
    private readonly IClasificacionDialogService _dialogService = Substitute.For<IClasificacionDialogService>();
    private readonly ICurrentUserSession _session = Substitute.For<ICurrentUserSession>();
    private readonly EquivalenciasProveedorViewModel _sut;

    private readonly List<EquivalenciaDto> _equivalencias =
    [
        new()
        {
            Tipo = TipoEquivalenciaEnum.Categoria,
            TextoRubro = "ESCOLAR",
            Texto = "CUADERNOS",
            IdCategoria = 10,
            NombreCategoria = "Cuadernos"
        },
        new() { Tipo = TipoEquivalenciaEnum.Marca, Texto = "RIVADAVIA S.A." }
    ];

    public EquivalenciasProveedorViewModelTests()
    {
        _session.IdUsuario.Returns(IdActor);
        _session.EsGerente.Returns(true);

        _categoriaService.ListarAsync(Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CategoriaDto>>(
            [
                new CategoriaDto { IdCategoria = 10, NombreCategoria = "Cuadernos", IdRubro = 1 },
                new CategoriaDto { IdCategoria = 11, NombreCategoria = "Lapiceras", IdRubro = 1 }
            ]));
        _marcaService.ListarAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<MarcaDto>>(
            [
                new MarcaDto { IdMarca = 20, NombreMarca = "Rivadavia" }
            ]));
        _equivalenciaService.ListarAsync(IdProveedor, Arg.Any<TipoEquivalenciaEnum?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<EquivalenciaDto>>(_equivalencias));

        _sut = new EquivalenciasProveedorViewModel(
            _equivalenciaService,
            _categoriaService,
            _marcaService,
            _dialogService,
            _session);
        _sut.Configurar(new ProveedorDto { IdProveedor = IdProveedor, RazonSocial = "Distribuidora Norte", Cuit = "30-11111111-1" });
    }

    public void Dispose()
    {
        _sut.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task InicializarAsync_ConEquivalencias_ListaTipoTextoYDestino()
    {
        await _sut.InicializarAsync();

        _sut.Equivalencias.Should().HaveCount(2);
        _sut.Equivalencias[0].TipoTexto.Should().Be("Categoría");
        _sut.Equivalencias[0].TextoProveedor.Should().Be("ESCOLAR › CUADERNOS");
        _sut.Equivalencias[0].DestinoTexto.Should().Be("Cuadernos");
        _sut.Equivalencias[1].TipoTexto.Should().Be("Marca");
        _sut.Equivalencias[1].DestinoTexto.Should().Be("Ignorada");
        _sut.TituloVentana.Should().Contain("Distribuidora Norte");
        _sut.IsBusy.Should().BeFalse();
        _sut.TieneError.Should().BeFalse();
    }

    [Fact]
    public async Task InicializarAsync_TipoCategoria_OfreceIgnorarYLasCategorias()
    {
        await _sut.InicializarAsync();

        _sut.Destinos.Select(d => d.Nombre).Should().Equal("Ignorar (no traducir)", "Cuadernos", "Lapiceras");
        _sut.DestinoSeleccionado!.IdDestino.Should().BeNull();
    }

    [Fact]
    public async Task TipoSeleccionado_CambiaAMarca_OfreceIgnorarYLasMarcas()
    {
        await _sut.InicializarAsync();

        _sut.TipoSeleccionado = _sut.TiposDisponibles.Single(t => t.Tipo == TipoEquivalenciaEnum.Marca);

        _sut.Destinos.Select(d => d.Nombre).Should().Equal("Ignorar (no traducir)", "Rivadavia");
        _sut.EsTipoCategoria.Should().BeFalse();
    }

    [Fact]
    public async Task FilaSeleccionada_AlElegirUnaFila_VuelcaSusDatosAlFormulario()
    {
        await _sut.InicializarAsync();

        _sut.FilaSeleccionada = _sut.Equivalencias[0];

        _sut.TipoSeleccionado.Tipo.Should().Be(TipoEquivalenciaEnum.Categoria);
        _sut.TextoRubro.Should().Be("ESCOLAR");
        _sut.Texto.Should().Be("CUADERNOS");
        _sut.DestinoSeleccionado!.IdDestino.Should().Be(10);
    }

    [Fact]
    public async Task AsignarCommand_ConDestino_LlamaAlServicioYRefrescaLaLista()
    {
        await _sut.InicializarAsync();
        _sut.Texto = "  LAPICERAS  ";
        _sut.TextoRubro = "ESCOLAR";
        _sut.DestinoSeleccionado = _sut.Destinos.Single(d => d.IdDestino == 11);

        await _sut.AsignarCommand.ExecuteAsync(null);

        await _equivalenciaService.Received(1).AsignarAsync(
            IdActor,
            Arg.Is<AsignarEquivalenciaDto>(d =>
                d.IdProveedor == IdProveedor &&
                d.Tipo == TipoEquivalenciaEnum.Categoria &&
                d.TextoRubro == "ESCOLAR" &&
                d.Texto == "LAPICERAS" &&
                d.IdDestino == 11),
            Arg.Any<CancellationToken>());
        // Una lectura al inicializar y otra al refrescar.
        await _equivalenciaService.Received(2).ListarAsync(IdProveedor, Arg.Any<TipoEquivalenciaEnum?>(), Arg.Any<CancellationToken>());
        _sut.Texto.Should().BeEmpty();
        _sut.MensajeEstado.Should().Be("Equivalencia guardada.");
        _sut.TieneError.Should().BeFalse();
    }

    [Fact]
    public async Task AsignarCommand_ConIgnorar_EnviaDestinoNulo()
    {
        await _sut.InicializarAsync();
        _sut.TipoSeleccionado = _sut.TiposDisponibles.Single(t => t.Tipo == TipoEquivalenciaEnum.Marca);
        _sut.Texto = "MARCA RARA";

        await _sut.AsignarCommand.ExecuteAsync(null);

        await _equivalenciaService.Received(1).AsignarAsync(
            IdActor,
            Arg.Is<AsignarEquivalenciaDto>(d =>
                d.Tipo == TipoEquivalenciaEnum.Marca &&
                d.TextoRubro == null &&
                d.Texto == "MARCA RARA" &&
                d.IdDestino == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AsignarCommand_ConDomainException_MuestraElErrorYNoLanza()
    {
        await _sut.InicializarAsync();
        _sut.Texto = "LAPICERAS";
        _equivalenciaService
            .AsignarAsync(Arg.Any<int>(), Arg.Any<AsignarEquivalenciaDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("La categoría de destino ya no existe."));

        await _sut.AsignarCommand.ExecuteAsync(null);

        _sut.MensajeError.Should().Be("La categoría de destino ya no existe.");
        _sut.TieneError.Should().BeTrue();
        _sut.Texto.Should().Be("LAPICERAS", "el formulario conserva lo escrito para que el usuario corrija");
        _sut.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task AsignarCommand_ConValidationException_MuestraLosMensajesDeValidacion()
    {
        await _sut.InicializarAsync();
        _sut.Texto = "LAPICERAS";
        _equivalenciaService
            .AsignarAsync(Arg.Any<int>(), Arg.Any<AsignarEquivalenciaDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ValidationException([new ValidationFailure("Texto", "El texto del proveedor es demasiado largo.")]));

        await _sut.AsignarCommand.ExecuteAsync(null);

        _sut.MensajeError.Should().Be("El texto del proveedor es demasiado largo.");
    }

    [Fact]
    public async Task QuitarCommand_Confirmado_LlamaAlServicioYRefrescaLaLista()
    {
        await _sut.InicializarAsync();
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        var fila = _sut.Equivalencias[0];

        await _sut.QuitarCommand.ExecuteAsync(fila);

        await _equivalenciaService.Received(1).QuitarAsync(
            IdActor,
            Arg.Is<QuitarEquivalenciaDto>(d =>
                d.IdProveedor == IdProveedor &&
                d.Tipo == TipoEquivalenciaEnum.Categoria &&
                d.TextoRubro == "ESCOLAR" &&
                d.Texto == "CUADERNOS"),
            Arg.Any<CancellationToken>());
        await _equivalenciaService.Received(2).ListarAsync(IdProveedor, Arg.Any<TipoEquivalenciaEnum?>(), Arg.Any<CancellationToken>());
        _sut.MensajeEstado.Should().Be("Equivalencia quitada.");
    }

    [Fact]
    public async Task QuitarCommand_SinConfirmacion_NoLlamaAlServicio()
    {
        await _sut.InicializarAsync();
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await _sut.QuitarCommand.ExecuteAsync(_sut.Equivalencias[0]);

        await _equivalenciaService.DidNotReceive().QuitarAsync(
            Arg.Any<int>(), Arg.Any<QuitarEquivalenciaDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QuitarCommand_ConDomainException_MuestraElErrorYNoLanza()
    {
        await _sut.InicializarAsync();
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _equivalenciaService
            .QuitarAsync(Arg.Any<int>(), Arg.Any<QuitarEquivalenciaDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperacionNoAutorizadaException("Solo un Gerente o un Encargado puede hacerlo."));

        await _sut.QuitarCommand.ExecuteAsync(_sut.Equivalencias[0]);

        _sut.MensajeError.Should().Be("Solo un Gerente o un Encargado puede hacerlo.");
        _sut.Equivalencias.Should().HaveCount(2);
    }

    [Fact]
    public async Task SinPermiso_ListaPeroEsSoloLecturaYNoPermiteEscribir()
    {
        _session.EsGerente.Returns(false);
        _session.EsEncargado.Returns(false);
        await _sut.InicializarAsync();
        _sut.Texto = "LAPICERAS";

        _sut.Equivalencias.Should().HaveCount(2);
        _sut.PuedeEditar.Should().BeFalse();
        _sut.SoloLectura.Should().BeTrue();
        _sut.AsignarCommand.CanExecute(null).Should().BeFalse();
        _sut.QuitarCommand.CanExecute(_sut.Equivalencias[0]).Should().BeFalse();
    }

    [Fact]
    public async Task Encargado_PuedeEscribir()
    {
        _session.EsGerente.Returns(false);
        _session.EsEncargado.Returns(true);
        await _sut.InicializarAsync();
        _sut.Texto = "LAPICERAS";

        _sut.PuedeEditar.Should().BeTrue();
        _sut.AsignarCommand.CanExecute(null).Should().BeTrue();
    }
}
