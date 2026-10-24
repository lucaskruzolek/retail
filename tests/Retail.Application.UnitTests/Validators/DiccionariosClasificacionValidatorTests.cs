using FluentAssertions;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Validators.Clasificacion;
using Retail.Domain.Common;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.Application.UnitTests.Validators;

/// <summary>
/// Validadores de los comandos de administración de los diccionarios del motor de catalogación (Track 6.6, Fase 4b).
/// </summary>
public class DiccionariosClasificacionValidatorTests
{
    private static readonly string TextoLargo = new('A', ReglasClasificacion.LongitudMaximaTextoDeProveedor + 1);

    [Theory]
    [InlineData("mochila", 3)]
    [InlineData("SET", null)]
    [InlineData("100", 3)]
    public void RegistrarTerminoValidator_DatosValidos_DebePasarValidacion(string termino, int? idCategoria)
    {
        var dto = new RegistrarTerminoDto { Termino = termino, IdCategoria = idCategoria };

        new RegistrarTerminoValidator().Validate(dto).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("DOS PALABRAS")]
    public void RegistrarTerminoValidator_TerminoInvalido_DebeFallar(string termino)
    {
        var result = new RegistrarTerminoValidator().Validate(new RegistrarTerminoDto { Termino = termino });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(RegistrarTerminoDto.Termino));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RegistrarTerminoValidator_CategoriaNoPositiva_DebeFallar(int idCategoria)
    {
        var dto = new RegistrarTerminoDto { Termino = "MOCHILA", IdCategoria = idCategoria };

        var result = new RegistrarTerminoValidator().Validate(dto);

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(RegistrarTerminoDto.IdCategoria));
    }

    [Fact]
    public void AliasMarcaValidator_DatosValidos_DebePasarValidacion()
    {
        var dto = new AliasMarcaDto { IdMarca = 1, Alias = "Artística Dibu" };

        new AliasMarcaValidator().Validate(dto).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, "HUSAR", nameof(AliasMarcaDto.IdMarca))]
    [InlineData(1, "", nameof(AliasMarcaDto.Alias))]
    [InlineData(1, "   ", nameof(AliasMarcaDto.Alias))]
    public void AliasMarcaValidator_DatoInvalido_DebeFallarEnEseCampo(int idMarca, string alias, string campo)
    {
        var result = new AliasMarcaValidator().Validate(new AliasMarcaDto { IdMarca = idMarca, Alias = alias });

        result.Errors.Should().ContainSingle(e => e.PropertyName == campo);
    }

    [Fact]
    public void PrefijoMarcaValidator_PrefijoDeSieteDigitos_DebePasarValidacion()
    {
        var dto = new PrefijoMarcaDto { IdMarca = 1, Prefijo = "7791234" };

        new PrefijoMarcaValidator().Validate(dto).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("779123")]
    [InlineData("77912345")]
    [InlineData("779A234")]
    [InlineData("2791234")]
    public void PrefijoMarcaValidator_PrefijoInvalido_DebeFallar(string prefijo)
    {
        var result = new PrefijoMarcaValidator().Validate(new PrefijoMarcaDto { IdMarca = 1, Prefijo = prefijo });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(PrefijoMarcaDto.Prefijo));
    }

    [Theory]
    [InlineData(TipoEquivalenciaEnum.Categoria, "Escolar", "Cuadernos", 4)]
    [InlineData(TipoEquivalenciaEnum.Categoria, null, "Varios", null)]
    [InlineData(TipoEquivalenciaEnum.Marca, null, "Rivadavia", 7)]
    public void AsignarEquivalenciaValidator_DatosValidos_DebePasarValidacion(
        TipoEquivalenciaEnum tipo,
        string? textoRubro,
        string texto,
        int? idDestino)
    {
        var dto = new AsignarEquivalenciaDto
        {
            IdProveedor = 1,
            Tipo = tipo,
            TextoRubro = textoRubro,
            Texto = texto,
            IdDestino = idDestino
        };

        new AsignarEquivalenciaValidator().Validate(dto).IsValid.Should().BeTrue();
    }

    [Fact]
    public void AsignarEquivalenciaValidator_MarcaConRubro_DebeFallar()
    {
        var dto = new AsignarEquivalenciaDto
        {
            IdProveedor = 1,
            Tipo = TipoEquivalenciaEnum.Marca,
            TextoRubro = "ESCOLAR",
            Texto = "RIVADAVIA"
        };

        var result = new AsignarEquivalenciaValidator().Validate(dto);

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(AsignarEquivalenciaDto.TextoRubro));
    }

    [Fact]
    public void AsignarEquivalenciaValidator_RubroDemasiadoLargo_DebeFallar()
    {
        var dto = new AsignarEquivalenciaDto
        {
            IdProveedor = 1,
            Tipo = TipoEquivalenciaEnum.Categoria,
            TextoRubro = TextoLargo,
            Texto = "CUADERNOS"
        };

        var result = new AsignarEquivalenciaValidator().Validate(dto);

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(AsignarEquivalenciaDto.TextoRubro));
    }

    [Fact]
    public void AsignarEquivalenciaValidator_DatosInvalidos_DebeFallarEnCadaCampo()
    {
        var dto = new AsignarEquivalenciaDto
        {
            IdProveedor = 0,
            Tipo = (TipoEquivalenciaEnum)99,
            Texto = "   ",
            IdDestino = 0
        };

        var result = new AsignarEquivalenciaValidator().Validate(dto);

        result.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(
            [
                nameof(AsignarEquivalenciaDto.IdProveedor),
                nameof(AsignarEquivalenciaDto.Tipo),
                nameof(AsignarEquivalenciaDto.Texto),
                nameof(AsignarEquivalenciaDto.IdDestino)
            ]);
    }

    [Fact]
    public void QuitarEquivalenciaValidator_DatosValidos_DebePasarValidacion()
    {
        var dto = new QuitarEquivalenciaDto
        {
            IdProveedor = 1,
            Tipo = TipoEquivalenciaEnum.Categoria,
            TextoRubro = "ESCOLAR",
            Texto = "CUADERNOS"
        };

        new QuitarEquivalenciaValidator().Validate(dto).IsValid.Should().BeTrue();
    }

    [Fact]
    public void QuitarEquivalenciaValidator_MarcaConRubroYTextoVacio_DebeFallarEnAmbos()
    {
        var dto = new QuitarEquivalenciaDto
        {
            IdProveedor = 1,
            Tipo = TipoEquivalenciaEnum.Marca,
            TextoRubro = "ESCOLAR",
            Texto = ""
        };

        var result = new QuitarEquivalenciaValidator().Validate(dto);

        result.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(
            [nameof(QuitarEquivalenciaDto.Texto), nameof(QuitarEquivalenciaDto.TextoRubro)]);
    }
}
