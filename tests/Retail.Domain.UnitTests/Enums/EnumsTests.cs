using FluentAssertions;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.Domain.UnitTests.Enums;

public class EnumsTests
{
    [Fact]
    public void RolUsuarioEnum_DebeContenerLosTresRolesDefinidos()
    {
        ((int)RolUsuarioEnum.Cajero).Should().Be(1);
        ((int)RolUsuarioEnum.Encargado).Should().Be(2);
        ((int)RolUsuarioEnum.Gerente).Should().Be(3);
    }

    [Fact]
    public void MedioPagoEnum_DebeSoportarCincoMediosDePago()
    {
        ((int)MedioPagoEnum.Efectivo).Should().Be(1);
        ((int)MedioPagoEnum.TarjetaDebito).Should().Be(2);
        ((int)MedioPagoEnum.TarjetaCredito).Should().Be(3);
        ((int)MedioPagoEnum.TransferenciaQr).Should().Be(4);
        ((int)MedioPagoEnum.CuentaCorriente).Should().Be(5);
    }

    [Fact]
    public void EstadoFiscalEnum_DebeContenerEstadosDeContingenciaYEmision()
    {
        ((int)EstadoFiscalEnum.NoAplica).Should().Be(0);
        ((int)EstadoFiscalEnum.Emitido).Should().Be(1);
        ((int)EstadoFiscalEnum.ErrorFiscalReintentable).Should().Be(2);
    }

    [Fact]
    public void CondicionIvaEnum_DebeContenerCategoriasFiscalesPrincipales()
    {
        ((int)CondicionIvaEnum.ConsumidorFinal).Should().Be(1);
        ((int)CondicionIvaEnum.ResponsableInscripto).Should().Be(2);
        ((int)CondicionIvaEnum.Monotributo).Should().Be(3);
        ((int)CondicionIvaEnum.Exento).Should().Be(4);
    }
}
