using System.Globalization;
using System.Windows.Data;
using FluentAssertions;
using Retail.App.Converters;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.App.UnitTests.Converters;

public class EnumATextoConverterTests
{
    private readonly EnumATextoConverter _converter = new();

    [Theory]
    [InlineData(MedioPagoEnum.TarjetaDebito, "Tarjeta de débito")]
    [InlineData(MedioPagoEnum.TarjetaCredito, "Tarjeta de crédito")]
    [InlineData(MedioPagoEnum.TransferenciaQr, "Transferencia / QR")]
    [InlineData(MedioPagoEnum.CuentaCorriente, "Cuenta corriente")]
    public void Convert_MedioDePago_DevuelveTextoLegible(MedioPagoEnum medio, string esperado)
    {
        // Act
        var texto = _converter.Convert(medio, typeof(string), null!, CultureInfo.InvariantCulture);

        // Assert
        texto.Should().Be(esperado);
    }

    [Fact]
    public void Convert_TipoDocumentoYCondicionIva_DevuelvenTextoLegible()
    {
        // Act + Assert
        EnumATextoConverter.ATexto(TipoDocumentoEnum.Cuit).Should().Be("CUIT");
        EnumATextoConverter.ATexto(CondicionIvaEnum.ResponsableInscripto).Should().Be("Responsable inscripto");
        EnumATextoConverter.ATexto(CondicionIvaEnum.ConsumidorFinal).Should().Be("Consumidor final");
    }

    [Theory]
    [MemberData(nameof(TodosLosValoresMostrados))]
    public void ATexto_CadaValorDeLosEnumsMostrados_NoDevuelveElNombreCompuestoDelCodigo(Enum valor)
    {
        // Act
        var texto = EnumATextoConverter.ATexto(valor);

        // Assert: un nombre compuesto del código ("TarjetaDebito") delata un valor nuevo sin texto definido
        texto.Should().NotMatchRegex("^[A-Z][a-z]+[A-Z]", $"el valor {valor} necesita un texto legible");
    }

    [Fact]
    public void Convert_ValorQueNoEsEnum_LoDevuelveSinCambios()
    {
        // Act
        var resultado = _converter.Convert("texto", typeof(string), null!, CultureInfo.InvariantCulture);

        // Assert
        resultado.Should().Be("texto");
    }

    [Fact]
    public void ConvertBack_NoSeUsa_DevuelveDoNothing()
    {
        // Act
        var resultado = _converter.ConvertBack("Efectivo", typeof(MedioPagoEnum), null!, CultureInfo.InvariantCulture);

        // Assert
        resultado.Should().Be(Binding.DoNothing);
    }

    public static TheoryData<Enum> TodosLosValoresMostrados()
    {
        var datos = new TheoryData<Enum>();
        foreach (var valor in Enum.GetValues<MedioPagoEnum>())
        {
            datos.Add(valor);
        }

        foreach (var valor in Enum.GetValues<TipoDocumentoEnum>())
        {
            datos.Add(valor);
        }

        foreach (var valor in Enum.GetValues<CondicionIvaEnum>())
        {
            datos.Add(valor);
        }

        return datos;
    }
}
