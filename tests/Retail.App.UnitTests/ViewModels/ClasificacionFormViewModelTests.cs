using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Services;
using Retail.App.ViewModels.Articulos;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class ClasificacionFormViewModelTests
{
    private const int IdActor = 7;

    private readonly IRubroService _rubroService = Substitute.For<IRubroService>();
    private readonly ICategoriaService _categoriaService = Substitute.For<ICategoriaService>();
    private readonly IMarcaService _marcaService = Substitute.For<IMarcaService>();
    private readonly ICurrentUserSession _session = Substitute.For<ICurrentUserSession>();
    private readonly ClasificacionFormViewModel _sut;

    private readonly List<RubroDto> _rubros = new()
    {
        new RubroDto { IdRubro = 1, NombreRubro = "Librería" },
        new RubroDto { IdRubro = 2, NombreRubro = "Juguetes" }
    };

    public ClasificacionFormViewModelTests()
    {
        _session.IdUsuario.Returns(IdActor);
        _sut = new ClasificacionFormViewModel(_rubroService, _categoriaService, _marcaService, _session);
    }

    [Fact]
    public async Task Guardar_AltaDeRubro_LlamaACrearConElActorDeLaSesion()
    {
        _sut.ConfigurarAltaRubro();
        _sut.Nombre = "Papelería";

        await _sut.GuardarCommand.ExecuteAsync(null);

        await _rubroService.Received(1).CrearAsync(
            IdActor,
            Arg.Is<CrearRubroDto>(d => d.Nombre == "Papelería"),
            Arg.Any<CancellationToken>());
        _sut.DialogResult.Should().BeTrue();
        _sut.TituloVentana.Should().Be("Nuevo rubro");
    }

    [Fact]
    public async Task Guardar_EdicionDeRubro_LlamaARenombrarConElIdDelElemento()
    {
        _sut.ConfigurarEdicionRubro(new RubroDto { IdRubro = 5, NombreRubro = "Viejo" });
        _sut.Nombre = "Nuevo";

        await _sut.GuardarCommand.ExecuteAsync(null);

        await _rubroService.Received(1).RenombrarAsync(
            IdActor,
            Arg.Is<RenombrarRubroDto>(d => d.IdRubro == 5 && d.Nombre == "Nuevo"),
            Arg.Any<CancellationToken>());
        _sut.TituloVentana.Should().Be("Editar rubro");
    }

    [Fact]
    public async Task Guardar_AltaDeCategoria_LlamaACrearConElRubroSeleccionado()
    {
        _sut.ConfigurarAltaCategoria(_rubros, 2);
        _sut.Nombre = "Muñecas";

        await _sut.GuardarCommand.ExecuteAsync(null);

        await _categoriaService.Received(1).CrearAsync(
            IdActor,
            Arg.Is<CrearCategoriaDto>(d => d.Nombre == "Muñecas" && d.IdRubro == 2),
            Arg.Any<CancellationToken>());
        _sut.TituloVentana.Should().Be("Nueva categoría");
    }

    [Fact]
    public async Task Guardar_EdicionDeCategoria_LlamaAActualizarConNombreYRubro()
    {
        var categoria = new CategoriaDto { IdCategoria = 9, NombreCategoria = "Lápices", IdRubro = 1 };
        _sut.ConfigurarEdicionCategoria(categoria, _rubros);
        _sut.Nombre = "Lápices de colores";
        _sut.RubroSeleccionado = _rubros[1];

        await _sut.GuardarCommand.ExecuteAsync(null);

        await _categoriaService.Received(1).ActualizarAsync(
            IdActor,
            Arg.Is<ActualizarCategoriaDto>(d => d.IdCategoria == 9 && d.Nombre == "Lápices de colores" && d.IdRubro == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Guardar_AltaDeMarca_LlamaACrearMarca()
    {
        _sut.ConfigurarAltaMarca();
        _sut.Nombre = "Faber";

        await _sut.GuardarCommand.ExecuteAsync(null);

        await _marcaService.Received(1).CrearAsync(
            IdActor,
            Arg.Is<CrearMarcaDto>(d => d.Nombre == "Faber"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Guardar_EdicionDeMarca_LlamaARenombrarMarca()
    {
        _sut.ConfigurarEdicionMarca(new MarcaDto { IdMarca = 3, NombreMarca = "Viejo" });
        _sut.Nombre = "Nuevo";

        await _sut.GuardarCommand.ExecuteAsync(null);

        await _marcaService.Received(1).RenombrarAsync(
            IdActor,
            Arg.Is<RenombrarMarcaDto>(d => d.IdMarca == 3 && d.Nombre == "Nuevo"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Guardar_SinSesion_UsaActorCero()
    {
        _session.IdUsuario.Returns((int?)null);
        _sut.ConfigurarAltaMarca();
        _sut.Nombre = "Faber";

        await _sut.GuardarCommand.ExecuteAsync(null);

        await _marcaService.Received(1).CrearAsync(0, Arg.Any<CrearMarcaDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void MostrarRubro_SoloEsVerdaderoParaCategoria()
    {
        _sut.ConfigurarAltaRubro();
        _sut.MostrarRubro.Should().BeFalse();

        _sut.ConfigurarAltaMarca();
        _sut.MostrarRubro.Should().BeFalse();

        _sut.ConfigurarAltaCategoria(_rubros, null);
        _sut.MostrarRubro.Should().BeTrue();
    }

    [Fact]
    public void ConfigurarEdicionCategoria_PreseleccionaElRubroDeLaCategoria()
    {
        var categoria = new CategoriaDto { IdCategoria = 9, NombreCategoria = "Muñecas", IdRubro = 2 };

        _sut.ConfigurarEdicionCategoria(categoria, _rubros);

        _sut.Nombre.Should().Be("Muñecas");
        _sut.Rubros.Should().HaveCount(2);
        _sut.RubroSeleccionado.Should().Be(_rubros[1]);
    }

    [Fact]
    public void ConfigurarAltaCategoria_ConRubroPreseleccionado_SeleccionaEseRubro()
    {
        _sut.ConfigurarAltaCategoria(_rubros, 1);

        _sut.RubroSeleccionado.Should().Be(_rubros[0]);
        _sut.Nombre.Should().BeEmpty();
    }

    [Fact]
    public void ConfigurarAltaCategoria_SinRubroPreseleccionado_NoSeleccionaNinguno()
    {
        _sut.ConfigurarAltaCategoria(_rubros, null);

        _sut.RubroSeleccionado.Should().BeNull();
    }

    [Fact]
    public async Task Guardar_ConValidationException_MuestraLosMensajesUnidosYNoCierra()
    {
        _rubroService.CrearAsync(Arg.Any<int>(), Arg.Any<CrearRubroDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ValidationException(new[]
            {
                new ValidationFailure("Nombre", "El nombre es obligatorio."),
                new ValidationFailure("Nombre", "El nombre es muy corto.")
            }));
        _sut.ConfigurarAltaRubro();

        await _sut.GuardarCommand.ExecuteAsync(null);

        _sut.MensajeError.Should().Be("El nombre es obligatorio.\nEl nombre es muy corto.");
        _sut.TieneError.Should().BeTrue();
        _sut.DialogResult.Should().BeFalse();
        _sut.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task Guardar_ConDomainException_MuestraElMensajeYNoCierra()
    {
        _marcaService.CrearAsync(Arg.Any<int>(), Arg.Any<CrearMarcaDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("Ya existe una marca activa con el nombre 'Faber'."));
        _sut.ConfigurarAltaMarca();

        await _sut.GuardarCommand.ExecuteAsync(null);

        _sut.MensajeError.Should().Be("Ya existe una marca activa con el nombre 'Faber'.");
        _sut.DialogResult.Should().BeFalse();
    }

    [Fact]
    public async Task Guardar_ConOperacionNoAutorizada_MuestraElMensajeYNoCierra()
    {
        _categoriaService.CrearAsync(Arg.Any<int>(), Arg.Any<CrearCategoriaDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperacionNoAutorizadaException("No tiene autoridad para administrar la clasificación."));
        _sut.ConfigurarAltaCategoria(_rubros, 1);

        await _sut.GuardarCommand.ExecuteAsync(null);

        _sut.MensajeError.Should().Be("No tiene autoridad para administrar la clasificación.");
        _sut.DialogResult.Should().BeFalse();
    }

    [Fact]
    public async Task Guardar_ConExcepcionInesperada_MuestraMensajeGenericoYNoCierra()
    {
        _rubroService.CrearAsync(Arg.Any<int>(), Arg.Any<CrearRubroDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));
        _sut.ConfigurarAltaRubro();

        await _sut.GuardarCommand.ExecuteAsync(null);

        _sut.MensajeError.Should().Be("Error inesperado al guardar: boom");
        _sut.DialogResult.Should().BeFalse();
    }

    [Fact]
    public void Cancelar_SinVentana_DejaDialogResultEnFalso()
    {
        _sut.ConfigurarAltaRubro();

        _sut.CancelarCommand.Execute(null);

        _sut.DialogResult.Should().BeFalse();
    }
}
