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

    [Fact]
    public void GetHashCode_AlSuscribirseAPropertyChanged_NoCambia()
    {
        // Arrange: lo mismo que hace un {Binding} de WPF al dibujar el ítem en un ListBox o DataGrid
        var articulo = CrearArticuloVenta(1, "Lápiz");
        int hashAntes = articulo.GetHashCode();

        // Act
        articulo.PropertyChanged += (_, _) => { };

        // Assert
        articulo.GetHashCode().Should().Be(hashAntes);
    }

    [Fact]
    public void HashSet_ItemEnlazadoDespuesDeAgregarlo_LoSigueEncontrando()
    {
        // Arrange: un Selector de WPF guarda la selección en un diccionario por hash
        var articulo = CrearArticuloVenta(1, "Lápiz");
        var seleccionados = new HashSet<ArticuloVentaDto> { articulo };

        // Act
        articulo.PropertyChanged += (_, _) => { };

        // Assert
        seleccionados.Should().Contain(articulo);
        seleccionados.Remove(articulo).Should().BeTrue();
    }

    [Fact]
    public void Equals_MismasPropiedadesYDistintosSuscriptores_SonIguales()
    {
        // Arrange
        var articulo = CrearArticuloVenta(1, "Lápiz");
        var copia = articulo with { };

        // Act
        articulo.PropertyChanged += (_, _) => { };

        // Assert: la igualdad por valor del record se conserva, sin depender de quién escucha el evento
        articulo.Should().Be(copia);
        articulo.GetHashCode().Should().Be(copia.GetHashCode());
    }

    [Fact]
    public void Equals_DistintasPropiedades_NoSonIguales()
    {
        // Arrange
        var lapiz = CrearArticuloVenta(1, "Lápiz");
        var goma = CrearArticuloVenta(2, "Goma");

        // Assert: la base no debe anular la comparación de las propiedades de los derivados
        lapiz.Should().NotBe(goma);
    }

    [Fact]
    public void Equals_DistintoTipoDeDto_NoSonIguales()
    {
        // Arrange: dos records derivados de BaseDto sin propiedades propias
        var dto = new TestDto();
        var otro = new OtroTestDto();

        // Assert: el EqualityContract (el tipo concreto) sigue formando parte de la igualdad
        dto.Equals(otro).Should().BeFalse();
    }

    private sealed record OtroTestDto : BaseDto;

    private static ArticuloVentaDto CrearArticuloVenta(int id, string descripcion)
    {
        return new ArticuloVentaDto
        {
            IdArticulo = id,
            Descripcion = descripcion,
            PrecioVenta = 300m,
            StockActual = 5,
            EsServicio = false
        };
    }
}
