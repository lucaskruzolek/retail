using FluentAssertions;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Domain.UnitTests.Entities;

/// <summary>
/// Equivalencias de clasificación del proveedor (Track 6.6, M-13, M-31, M-32): reglas de traducción de su rubro,
/// categoría o marca a la taxonomía del comercio.
/// </summary>
public class ProveedorEquivalenciasTests
{
    [Fact]
    public void AsignarEquivalenciaCategoria_ValorNuevo_LaCreaConLaClaveNormalizada()
    {
        var proveedor = CrearProveedor();

        var equivalencia = proveedor.AsignarEquivalenciaCategoria(" Escolar ", "Cuadernos  t/d", 4);

        equivalencia.Tipo.Should().Be(TipoEquivalenciaEnum.Categoria);
        equivalencia.TextoRubro.Should().Be("ESCOLAR");
        equivalencia.Texto.Should().Be("CUADERNOS T/D");
        equivalencia.IdCategoria.Should().Be(4);
        equivalencia.IdMarca.Should().BeNull();
        equivalencia.EsIgnorada.Should().BeFalse();
        proveedor.Equivalencias.Should().ContainSingle();
    }

    [Fact]
    public void AsignarEquivalenciaCategoria_MismoValorEscritoDistinto_ReemplazaElDestinoSinDuplicar()
    {
        var proveedor = CrearProveedor();
        var original = proveedor.AsignarEquivalenciaCategoria("ESCOLAR", "CUADERNOS T/D", 4);

        var reasignada = proveedor.AsignarEquivalenciaCategoria("escolar", "Cuadernos T/D", 9);

        reasignada.Should().BeSameAs(original);
        reasignada.IdCategoria.Should().Be(9);
        proveedor.Equivalencias.Should().ContainSingle();
    }

    [Fact]
    public void AsignarEquivalenciaCategoria_MismaCategoriaEnOtroRubro_EsOtraEquivalencia()
    {
        var proveedor = CrearProveedor();

        proveedor.AsignarEquivalenciaCategoria("ESCOLAR", "CUADERNOS", 4);
        proveedor.AsignarEquivalenciaCategoria("OFICINA", "CUADERNOS", 5);
        proveedor.AsignarEquivalenciaCategoria(null, "CUADERNOS", 6);

        proveedor.Equivalencias.Select(e => (e.TextoRubro, e.IdCategoria)).Should().BeEquivalentTo(
            new (string?, int?)[] { ("ESCOLAR", 4), ("OFICINA", 5), (null, 6) });
    }

    [Fact]
    public void AsignarEquivalenciaCategoria_RubroEnBlanco_SeGuardaSinRubro()
    {
        var proveedor = CrearProveedor();

        var equivalencia = proveedor.AsignarEquivalenciaCategoria("   ", "VARIOS", 4);

        equivalencia.TextoRubro.Should().BeNull();
    }

    [Fact]
    public void AsignarEquivalenciaCategoria_SinDestino_QuedaIgnorada()
    {
        var proveedor = CrearProveedor();
        proveedor.AsignarEquivalenciaCategoria(null, "VARIOS", 4);

        var equivalencia = proveedor.AsignarEquivalenciaCategoria(null, "VARIOS", null);

        equivalencia.EsIgnorada.Should().BeTrue();
        equivalencia.IdCategoria.Should().BeNull();
    }

    [Fact]
    public void AsignarEquivalenciaMarca_TextoValido_LaCreaSinRubroNiCategoria()
    {
        var proveedor = CrearProveedor();

        var equivalencia = proveedor.AsignarEquivalenciaMarca("Rivadavia", 7);

        equivalencia.Tipo.Should().Be(TipoEquivalenciaEnum.Marca);
        equivalencia.Texto.Should().Be("RIVADAVIA");
        equivalencia.TextoRubro.Should().BeNull();
        equivalencia.IdMarca.Should().Be(7);
        equivalencia.IdCategoria.Should().BeNull();
    }

    [Fact]
    public void AsignarEquivalencias_MismoTextoDeDistintoTipo_SonIndependientes()
    {
        var proveedor = CrearProveedor();

        proveedor.AsignarEquivalenciaCategoria(null, "RIVADAVIA", 4);
        proveedor.AsignarEquivalenciaMarca("RIVADAVIA", 7);

        proveedor.Equivalencias.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AsignarEquivalencia_DestinoNoPositivo_LanzaDomainException(int idDestino)
    {
        var proveedor = CrearProveedor();

        var categoria = () => proveedor.AsignarEquivalenciaCategoria(null, "VARIOS", idDestino);
        var marca = () => proveedor.AsignarEquivalenciaMarca("RIVADAVIA", idDestino);

        categoria.Should().Throw<DomainException>();
        marca.Should().Throw<DomainException>();
        proveedor.Equivalencias.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AsignarEquivalenciaCategoria_TextoVacio_LanzaDomainException(string texto)
    {
        var proveedor = CrearProveedor();

        var act = () => proveedor.AsignarEquivalenciaCategoria("ESCOLAR", texto, 4);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AsignarEquivalenciaMarca_TextoDemasiadoLargo_LanzaDomainException()
    {
        var proveedor = CrearProveedor();

        var act = () => proveedor.AsignarEquivalenciaMarca(new string('A', 101), 7);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void QuitarEquivalencia_Existente_LaDaDeBajaYPermiteVolverAAsignarla()
    {
        var proveedor = CrearProveedor();
        var original = proveedor.AsignarEquivalenciaCategoria("ESCOLAR", "CUADERNOS", 4);

        proveedor.QuitarEquivalencia(TipoEquivalenciaEnum.Categoria, "escolar", "cuadernos");
        var nueva = proveedor.AsignarEquivalenciaCategoria("ESCOLAR", "CUADERNOS", 5);

        original.IsDeleted.Should().BeTrue();
        nueva.Should().NotBeSameAs(original);
        nueva.IdCategoria.Should().Be(5);
    }

    [Fact]
    public void QuitarEquivalencia_Inexistente_LanzaDomainException()
    {
        var proveedor = CrearProveedor();

        var act = () => proveedor.QuitarEquivalencia(TipoEquivalenciaEnum.Marca, null, "RIVADAVIA");

        act.Should().Throw<DomainException>();
    }

    private static Proveedor CrearProveedor()
    {
        return Proveedor.Crear("Distribuidora Valida", "20111111112");
    }
}
