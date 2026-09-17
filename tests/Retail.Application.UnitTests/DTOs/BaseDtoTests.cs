namespace Retail.Application.UnitTests.DTOs;

using System.ComponentModel;
using FluentAssertions;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clientes;
using Retail.Application.DTOs.Common;
using Retail.Application.DTOs.Usuarios;
using Xunit;

public class BaseDtoTests
{
    private sealed record TestDto : BaseDto
    {
        public void NotificarPropiedadCambiada(string nombrePropiedad)
        {
            OnPropertyChanged(nombrePropiedad);
        }
    }

    [Fact]
    public void BaseDto_DebeImplementarINotifyPropertyChanged()
    {
        // Arrange & Act
        var dto = new TestDto();

        // Assert
        dto.Should().BeAssignableTo<INotifyPropertyChanged>();
    }

    [Fact]
    public void BaseDto_OnPropertyChanged_DebeDispararEventoDeCambio()
    {
        // Arrange
        var dto = new TestDto();
        string? propiedadNotificada = null;
        dto.PropertyChanged += (sender, args) =>
        {
            propiedadNotificada = args.PropertyName;
        };

        // Act
        dto.NotificarPropiedadCambiada("TestPropiedad");

        // Assert
        propiedadNotificada.Should().Be("TestPropiedad");
    }

    [Theory]
    [InlineData(typeof(ArticuloDto))]
    // [InlineData(typeof(CategoriaDto))] // Excluido para permitir correcto funcionamiento de ComboBox en WPF
    // [InlineData(typeof(MarcaDto))]     // Excluido para permitir correcto funcionamiento de ComboBox en WPF
    [InlineData(typeof(ArticuloVentaDto))]
    [InlineData(typeof(AlertaStockDto))]
    [InlineData(typeof(ClienteDto))]
    [InlineData(typeof(UsuarioDto))]
    public void DtosDePresentacion_DebenHeredarDeBaseDtoParaPrevenirBindingLeaks(Type tipoDto)
    {
        // Assert
        typeof(BaseDto).IsAssignableFrom(tipoDto).Should().BeTrue(
            $"el DTO {tipoDto.Name} debe heredar de BaseDto para evitar fugas de memoria en WPF (TypeDescriptor.AddValueChanged)");
        typeof(INotifyPropertyChanged).IsAssignableFrom(tipoDto).Should().BeTrue();
    }
}
