using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Retail.App.Services;
using Retail.App.ViewModels.Proveedores;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class AsociarValoresCatalogoViewModelTests : IDisposable
{
    private readonly ICategoriaService _categoriaService = Substitute.For<ICategoriaService>();
    private readonly IMarcaService _marcaService = Substitute.For<IMarcaService>();
    private readonly ICurrentUserSession _session = Substitute.For<ICurrentUserSession>();
    private readonly AsociarValoresCatalogoViewModel _sut;

    private static readonly ProveedorDto Proveedor = new() { IdProveedor = 4, RazonSocial = "Distribuidora Norte", Cuit = "30-70123456-7" };

    private static readonly IReadOnlyList<ValorSinTraducirDto> Valores =
    [
        new ValorSinTraducirDto { Tipo = TipoEquivalenciaEnum.Categoria, TextoRubro = "ESCOLAR", Texto = "CUADERNOS", CantidadDeItems = 14 },
        new ValorSinTraducirDto { Tipo = TipoEquivalenciaEnum.Marca, Texto = "BIC ARGENTINA", CantidadDeItems = 8 }
    ];

    public AsociarValoresCatalogoViewModelTests()
    {
        _sut = new AsociarValoresCatalogoViewModel(
            Substitute.For<IEquivalenciaClasificacionService>(),
            _categoriaService,
            _marcaService,
            _session,
            NullLogger<AsociarValoresCatalogoViewModel>.Instance);
    }

    public void Dispose()
    {
        _sut.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Inicializar_ConGerente_CargaLosValoresParaAsociar()
    {
        // Arrange
        _session.EsGerente.Returns(true);

        // Act
        _sut.Inicializar(Proveedor, Valores);

        // Assert
        _sut.Proveedor.Should().Be(Proveedor);
        _sut.Paso.Valores.Should().HaveCount(2);
        _sut.Paso.PuedeAdministrar.Should().BeTrue();
        _sut.Paso.SoloLectura.Should().BeFalse();
    }

    [Fact]
    public void Inicializar_SinAutoridadDeGerenteNiEncargado_QuedaDeSoloLectura()
    {
        // Arrange (D-5): un Cajero no escribe diccionarios.
        _session.EsGerente.Returns(false);
        _session.EsEncargado.Returns(false);

        // Act
        _sut.Inicializar(Proveedor, Valores);

        // Assert
        _sut.PuedeAdministrar.Should().BeFalse();
        _sut.Paso.SoloLectura.Should().BeTrue();
        _sut.Paso.Valores.Should().HaveCount(2);
    }

    [Fact]
    public void ClasificarCommand_MarcaElDialogoComoAceptado()
    {
        // Arrange
        _sut.Inicializar(Proveedor, Valores);

        // Act
        _sut.ClasificarCommand.Execute(null);

        // Assert
        _sut.DialogResult.Should().BeTrue();
    }

    [Fact]
    public void CancelarCommand_MarcaElDialogoComoCancelado()
    {
        // Arrange (M-80): cancelar aborta la clasificación.
        _sut.Inicializar(Proveedor, Valores);
        _sut.ClasificarCommand.Execute(null);

        // Act
        _sut.CancelarCommand.Execute(null);

        // Assert
        _sut.DialogResult.Should().BeFalse();
    }

    [Fact]
    public void Inicializar_TrasUnaAperturaAceptada_ReiniciaElResultado()
    {
        // Arrange
        _sut.Inicializar(Proveedor, Valores);
        _sut.ClasificarCommand.Execute(null);

        // Act
        _sut.Inicializar(Proveedor, Valores);

        // Assert
        _sut.DialogResult.Should().BeFalse();
    }

    [Fact]
    public async Task CargarDestinosAsync_CargaLasCategoriasYMarcasDeLaTienda()
    {
        // Arrange
        _categoriaService.ListarAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<CategoriaDto> { new() { IdCategoria = 1, NombreCategoria = "Cuadernos", IdRubro = 1 } });
        _marcaService.ListarAsync(null, Arg.Any<CancellationToken>())
            .Returns(new List<MarcaDto> { new() { IdMarca = 2, NombreMarca = "Bic" } });

        // Act
        await _sut.CargarDestinosAsync();

        // Assert
        _sut.Paso.Categorias.Should().ContainSingle().Which.NombreCategoria.Should().Be("Cuadernos");
        _sut.Paso.Marcas.Should().ContainSingle().Which.NombreMarca.Should().Be("Bic");
        _sut.Paso.TieneError.Should().BeFalse();
    }

    [Fact]
    public async Task CargarDestinosAsync_FallaLaLectura_InformaElErrorEnElPaso()
    {
        // Arrange
        _categoriaService.ListarAsync(null, null, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<CategoriaDto>>(_ => throw new InvalidOperationException("sin conexión"));

        // Act
        await _sut.CargarDestinosAsync();

        // Assert
        _sut.Paso.TieneError.Should().BeTrue();
        _sut.Paso.Categorias.Should().BeEmpty();
    }
}
