using FluentAssertions;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Domain.UnitTests.Entities;

public class PresupuestoTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 17);

    private static RenglonAPresupuestar Renglon(
        int idArticulo = 10,
        string descripcion = "Lápiz negro",
        int cantidad = 2,
        decimal precio = 100m)
    {
        return new RenglonAPresupuestar(idArticulo, descripcion, cantidad, precio);
    }

    private static Presupuesto Emitir(
        IReadOnlyList<RenglonAPresupuestar>? renglones = null,
        int? idCliente = 5,
        string? titulo = null,
        decimal descuento = 0m,
        int vigenciaDias = Presupuesto.VigenciaPorDefectoDias)
    {
        return Presupuesto.Emitir(1, idCliente, titulo, renglones ?? [Renglon()], descuento, vigenciaDias, Hoy);
    }

    // ───────────────────── Emisión ─────────────────────

    [Fact]
    public void Emitir_ConRenglonesValidos_CalculaSubtotalDescuentoYTotal()
    {
        // Act
        var presupuesto = Emitir(
            [Renglon(10, "Lápiz negro", 3, 150.50m), Renglon(11, "Cuaderno", 1, 99.99m)],
            descuento: 51.49m);

        // Assert
        presupuesto.Subtotal.Should().Be(551.49m);
        presupuesto.Descuento.Should().Be(51.49m);
        presupuesto.Total.Should().Be(500m);
        presupuesto.Detalles.Should().HaveCount(2);
        presupuesto.Detalles.Single(d => d.IdArticulo == 10).SubtotalItem.Should().Be(451.50m);
        presupuesto.Estado.Should().Be(EstadoPresupuestoEnum.Pendiente);
        presupuesto.IdUsuario.Should().Be(1);
        presupuesto.FechaCierre.Should().BeNull();
        presupuesto.FechaEmision.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        presupuesto.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Emitir_ConConsumidorFinal_DejaIdClienteNulo()
    {
        var presupuesto = Emitir(idCliente: null);

        presupuesto.IdCliente.Should().BeNull();
    }

    [Fact]
    public void Emitir_CalculaFechaVencimientoDesdeHoy()
    {
        var presupuesto = Emitir(vigenciaDias: 30);

        presupuesto.FechaVencimiento.Should().Be(new DateOnly(2026, 12, 17));
    }

    [Theory]
    [InlineData(0, 1, null)]
    [InlineData(-1, 1, null)]
    [InlineData(1, 0, null)]
    [InlineData(1, 1, 0)]
    [InlineData(1, 1, -4)]
    public void Emitir_IdentificadoresInvalidos_LanzaArgumentOutOfRangeException(int idUsuario, int idArticulo, int? idCliente)
    {
        var act = () => Presupuesto.Emitir(idUsuario, idCliente, null, [Renglon(idArticulo)], 0m, 15, Hoy);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Emitir_ConRenglonesNulos_LanzaArgumentNullException()
    {
        var act = () => Presupuesto.Emitir(1, null, null, null!, 0m, 15, Hoy);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Emitir_SinRenglones_LanzaDomainException()
    {
        var act = () => Emitir(renglones: []);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(-1, 100)]
    public void Emitir_ConCantidadMenorAUno_LanzaDomainException(int cantidad, decimal precio)
    {
        var act = () => Emitir([Renglon(cantidad: cantidad, precio: precio)]);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Emitir_ConPrecioNegativo_LanzaDomainException()
    {
        var act = () => Emitir([Renglon(precio: -1m)]);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Emitir_ConDescripcionVacia_LanzaDomainException(string descripcion)
    {
        var act = () => Emitir([Renglon(descripcion: descripcion)]);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Emitir_ConDescripcionDeMasDe200Caracteres_LanzaDomainException()
    {
        var act = () => Emitir([Renglon(descripcion: new string('a', DetallePresupuesto.LongitudMaximaDescripcion + 1))]);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Emitir_ConDescripcionConEspaciosDeMas_LaNormaliza()
    {
        var presupuesto = Emitir([Renglon(descripcion: "  Lápiz    negro  ")]);

        presupuesto.Detalles.Single().Descripcion.Should().Be("Lápiz negro");
    }

    [Fact]
    public void Emitir_ConArticuloRepetido_AgrupaCantidades()
    {
        var presupuesto = Emitir([Renglon(10, cantidad: 2, precio: 100m), Renglon(10, cantidad: 3, precio: 100m)]);

        var detalle = presupuesto.Detalles.Should().ContainSingle().Subject;
        detalle.Cantidad.Should().Be(5);
        detalle.SubtotalItem.Should().Be(500m);
        presupuesto.Subtotal.Should().Be(500m);
    }

    [Fact]
    public void Emitir_ConArticuloRepetidoConDistintoPrecio_LanzaDomainException()
    {
        var act = () => Emitir([Renglon(10, precio: 100m), Renglon(10, precio: 120m)]);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(91)]
    [InlineData(-5)]
    public void Emitir_ConVigenciaFueraDeRango_LanzaDomainException(int vigenciaDias)
    {
        var act = () => Emitir(vigenciaDias: vigenciaDias);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(Presupuesto.VigenciaMinimaDias)]
    [InlineData(Presupuesto.VigenciaMaximaDias)]
    public void Emitir_ConVigenciaEnLosLimites_EmiteElPresupuesto(int vigenciaDias)
    {
        var presupuesto = Emitir(vigenciaDias: vigenciaDias);

        presupuesto.FechaVencimiento.Should().Be(Hoy.AddDays(vigenciaDias));
    }

    [Fact]
    public void Emitir_ConDescuentoMayorAlSubtotal_LanzaDomainException()
    {
        var act = () => Emitir([Renglon(cantidad: 1, precio: 100m)], descuento: 100.01m);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Emitir_ConDescuentoNegativo_LanzaDomainException()
    {
        var act = () => Emitir(descuento: -1m);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Emitir_ConDescuentoIgualAlSubtotal_DejaTotalEnCero()
    {
        var presupuesto = Emitir([Renglon(cantidad: 1, precio: 100m)], descuento: 100m);

        presupuesto.Total.Should().Be(0m);
    }

    // ───────────────────── Título ─────────────────────

    [Fact]
    public void Emitir_ConTituloConEspaciosDeMas_LoNormaliza()
    {
        var presupuesto = Emitir(titulo: "  Lista   escolar  3.º grado ");

        presupuesto.Titulo.Should().Be("Lista escolar 3.º grado");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    public void Emitir_ConTituloEnBlanco_LoGuardaComoNulo(string? titulo)
    {
        var presupuesto = Emitir(titulo: titulo);

        presupuesto.Titulo.Should().BeNull();
    }

    [Fact]
    public void Emitir_ConTituloDeMasDe100Caracteres_LanzaDomainException()
    {
        var act = () => Emitir(titulo: new string('a', Presupuesto.LongitudMaximaTitulo + 1));

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Emitir_ConTituloDeExactamente100Caracteres_LoAcepta()
    {
        var presupuesto = Emitir(titulo: new string('a', Presupuesto.LongitudMaximaTitulo));

        presupuesto.Titulo.Should().HaveLength(Presupuesto.LongitudMaximaTitulo);
    }

    // ───────────────────── Vigencia ─────────────────────

    [Fact]
    public void EstaVigente_ElUltimoDia_DevuelveTrue()
    {
        var presupuesto = Emitir(vigenciaDias: 15);

        presupuesto.EstaVigente(presupuesto.FechaVencimiento).Should().BeTrue();
    }

    [Fact]
    public void EstaVigente_ElDiaSiguiente_DevuelveFalse()
    {
        var presupuesto = Emitir(vigenciaDias: 15);

        presupuesto.EstaVigente(presupuesto.FechaVencimiento.AddDays(1)).Should().BeFalse();
    }

    // ───────────────────── Precio pactado ─────────────────────

    [Fact]
    public void ObtenerPrecioPactado_Vigente_DevuelvePactado()
    {
        var presupuesto = Emitir([Renglon(10, cantidad: 5, precio: 120m)]);

        presupuesto.ObtenerPrecioPactado(10, 3, Hoy).Should().Be(120m);
    }

    [Fact]
    public void ObtenerPrecioPactado_ConLaCantidadPresupuestada_DevuelvePactado()
    {
        var presupuesto = Emitir([Renglon(10, cantidad: 5, precio: 120m)]);

        presupuesto.ObtenerPrecioPactado(10, 5, Hoy).Should().Be(120m);
    }

    [Fact]
    public void ObtenerPrecioPactado_Vencido_DevuelveNull()
    {
        var presupuesto = Emitir([Renglon(10, cantidad: 5, precio: 120m)], vigenciaDias: 15);

        presupuesto.ObtenerPrecioPactado(10, 99, presupuesto.FechaVencimiento.AddDays(1)).Should().BeNull();
    }

    [Fact]
    public void ObtenerPrecioPactado_ArticuloQueNoFigura_DevuelveNull()
    {
        var presupuesto = Emitir([Renglon(10)]);

        presupuesto.ObtenerPrecioPactado(99, 1, Hoy).Should().BeNull();
    }

    [Fact]
    public void ObtenerPrecioPactado_CantidadMayorALaPresupuestada_LanzaDomainException()
    {
        var presupuesto = Emitir([Renglon(10, "Lápiz negro", cantidad: 5)]);

        var act = () => presupuesto.ObtenerPrecioPactado(10, 6, Hoy);

        act.Should().Throw<DomainException>()
            .Which.Message.Should().Contain("Lápiz negro").And.Contain("5").And.Contain("6");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ObtenerPrecioPactado_ConCantidadNoPositiva_LanzaArgumentOutOfRangeException(int cantidad)
    {
        var presupuesto = Emitir();

        var act = () => presupuesto.ObtenerPrecioPactado(10, cantidad, Hoy);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ───────────────────── Transiciones ─────────────────────

    [Fact]
    public void RegistrarConversion_Pendiente_PasaAConvertidoYFijaFechaCierre()
    {
        var presupuesto = Emitir();

        presupuesto.RegistrarConversion();

        presupuesto.Estado.Should().Be(EstadoPresupuestoEnum.Convertido);
        presupuesto.FechaCierre.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RegistrarConversion_VencidoPendiente_PasaAConvertido()
    {
        // Un Pendiente vencido sigue siendo convertible: la vigencia no es un estado (RF-12, PS-08).
        var presupuesto = Emitir(vigenciaDias: 1);

        presupuesto.RegistrarConversion();

        presupuesto.Estado.Should().Be(EstadoPresupuestoEnum.Convertido);
    }

    [Fact]
    public void RegistrarConversion_Convertido_LanzaTransicionPresupuestoInvalidaException()
    {
        var presupuesto = Emitir();
        presupuesto.RegistrarConversion();

        var act = presupuesto.RegistrarConversion;

        act.Should().Throw<TransicionPresupuestoInvalidaException>()
            .Which.EstadoActual.Should().Be(EstadoPresupuestoEnum.Convertido);
    }

    [Fact]
    public void RegistrarConversion_Anulado_LanzaTransicionPresupuestoInvalidaException()
    {
        var presupuesto = Emitir();
        presupuesto.Anular();

        var act = presupuesto.RegistrarConversion;

        act.Should().Throw<TransicionPresupuestoInvalidaException>()
            .Which.EstadoActual.Should().Be(EstadoPresupuestoEnum.Anulado);
    }

    [Fact]
    public void Anular_Pendiente_PasaAAnuladoYFijaFechaCierre()
    {
        var presupuesto = Emitir();

        presupuesto.Anular();

        presupuesto.Estado.Should().Be(EstadoPresupuestoEnum.Anulado);
        presupuesto.FechaCierre.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Anular_Convertido_LanzaTransicionPresupuestoInvalidaException()
    {
        var presupuesto = Emitir();
        presupuesto.RegistrarConversion();

        var act = presupuesto.Anular;

        act.Should().Throw<TransicionPresupuestoInvalidaException>()
            .Which.EstadoActual.Should().Be(EstadoPresupuestoEnum.Convertido);
    }

    [Fact]
    public void Anular_Anulado_LanzaTransicionPresupuestoInvalidaException()
    {
        var presupuesto = Emitir();
        presupuesto.Anular();

        var act = presupuesto.Anular;

        act.Should().Throw<TransicionPresupuestoInvalidaException>();
    }

    // ───────────────────── Corrección (Módulo 5.4) ─────────────────────

    [Fact]
    public void Reemplazar_Pendiente_PasaAReemplazadoYFijaFechaCierre()
    {
        var presupuesto = Emitir();

        presupuesto.Reemplazar();

        presupuesto.Estado.Should().Be(EstadoPresupuestoEnum.Reemplazado);
        presupuesto.FechaCierre.Should().NotBeNull();
    }

    [Fact]
    public void Reemplazar_Convertido_LanzaTransicionPresupuestoInvalidaException()
    {
        var presupuesto = Emitir();
        presupuesto.RegistrarConversion();

        var act = presupuesto.Reemplazar;

        act.Should().Throw<TransicionPresupuestoInvalidaException>()
            .Which.EstadoActual.Should().Be(EstadoPresupuestoEnum.Convertido);
    }

    [Fact]
    public void Reemplazar_Anulado_LanzaTransicionPresupuestoInvalidaException()
    {
        var presupuesto = Emitir();
        presupuesto.Anular();

        var act = presupuesto.Reemplazar;

        act.Should().Throw<TransicionPresupuestoInvalidaException>();
    }

    [Fact]
    public void Reemplazar_Reemplazado_LanzaTransicionPresupuestoInvalidaException()
    {
        var presupuesto = Emitir();
        presupuesto.Reemplazar();

        var act = presupuesto.Reemplazar;

        act.Should().Throw<TransicionPresupuestoInvalidaException>()
            .Which.EstadoActual.Should().Be(EstadoPresupuestoEnum.Reemplazado);
    }

    [Fact]
    public void Emitir_ConIdPresupuestoReemplazado_LoGuarda()
    {
        var presupuesto = Presupuesto.Emitir(1, 5, null, [Renglon()], 0m, Presupuesto.VigenciaPorDefectoDias, Hoy, idPresupuestoReemplazado: 41);

        presupuesto.IdPresupuestoReemplazado.Should().Be(41);
    }

    [Fact]
    public void Emitir_SinIdPresupuestoReemplazado_LoDejaNulo()
    {
        Emitir().IdPresupuestoReemplazado.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Emitir_ConIdPresupuestoReemplazadoInvalido_LanzaArgumentOutOfRangeException(int idReemplazado)
    {
        var act = () => Presupuesto.Emitir(1, 5, null, [Renglon()], 0m, Presupuesto.VigenciaPorDefectoDias, Hoy, idReemplazado);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CambiarTitulo_ConEspaciosDeMas_LoNormaliza()
    {
        var presupuesto = Emitir();

        presupuesto.CambiarTitulo("  Lista   escolar ");

        presupuesto.Titulo.Should().Be("Lista escolar");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CambiarTitulo_VacioONulo_QuitaElTitulo(string? titulo)
    {
        var presupuesto = Emitir(titulo: "Con título");

        presupuesto.CambiarTitulo(titulo);

        presupuesto.Titulo.Should().BeNull();
    }

    [Fact]
    public void CambiarTitulo_ConMasDe100Caracteres_LanzaDomainException()
    {
        var presupuesto = Emitir(titulo: "Original");

        var act = () => presupuesto.CambiarTitulo(new string('a', Presupuesto.LongitudMaximaTitulo + 1));

        act.Should().Throw<DomainException>();
        presupuesto.Titulo.Should().Be("Original");
    }

    [Fact]
    public void CambiarTitulo_Convertido_LoCambia()
    {
        var presupuesto = Emitir();
        presupuesto.RegistrarConversion();

        presupuesto.CambiarTitulo("Cobrado a la docente");

        presupuesto.Titulo.Should().Be("Cobrado a la docente");
        presupuesto.Estado.Should().Be(EstadoPresupuestoEnum.Convertido);
    }
}
