using FluentAssertions;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Validators.Proveedores;
using Xunit;

namespace Retail.Application.UnitTests.Validators;

/// <summary>
/// Contrato de incorporación con la clasificación por ítem (Track 6.6, R-7, M-34).
/// </summary>
public class IncorporarCatalogoArticulosValidatorTests
{
    private readonly IncorporarCatalogoArticulosValidator _validator = new();

    [Fact]
    public void Validar_ItemConClasificacionCompleta_EsValido()
    {
        var dto = Dto(new ItemIncorporacionArticuloDto
        {
            IdCatalogo = 1,
            PorcentajeGanancia = 40m,
            IdCategoria = 3,
            IdCategoriaSugerida = 3,
            SinMarcaConfirmada = true,
            IdMarcaSugerida = 7
        });

        _validator.Validate(dto).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validar_SinItems_EsInvalido()
    {
        _validator.Validate(new IncorporarCatalogoArticulosDto()).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validar_MarcaYSinMarcaALaVez_EsInvalido()
    {
        var dto = Dto(new ItemIncorporacionArticuloDto { IdCatalogo = 1, PorcentajeGanancia = 40m, IdMarca = 7, SinMarcaConfirmada = true });

        var resultado = _validator.Validate(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Contain("Sin marca");
    }

    [Fact]
    public void Validar_ItemRepetido_EsInvalido()
    {
        var item = new ItemIncorporacionArticuloDto { IdCatalogo = 1, PorcentajeGanancia = 40m };

        _validator.Validate(Dto(item, item)).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, null, null, null)]
    [InlineData(null, 0, null, null)]
    [InlineData(null, null, -1, null)]
    [InlineData(null, null, null, 0)]
    public void Validar_IdentificadorDeClasificacionNoPositivo_EsInvalido(int? categoria, int? categoriaSugerida, int? marca, int? marcaSugerida)
    {
        var dto = Dto(new ItemIncorporacionArticuloDto
        {
            IdCatalogo = 1,
            PorcentajeGanancia = 40m,
            IdCategoria = categoria,
            IdCategoriaSugerida = categoriaSugerida,
            IdMarca = marca,
            IdMarcaSugerida = marcaSugerida
        });

        _validator.Validate(dto).IsValid.Should().BeFalse();
    }

    private static IncorporarCatalogoArticulosDto Dto(params ItemIncorporacionArticuloDto[] items)
    {
        return new IncorporarCatalogoArticulosDto { Items = items };
    }
}
