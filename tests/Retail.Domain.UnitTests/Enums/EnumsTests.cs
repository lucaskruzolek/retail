using FluentAssertions;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.Domain.UnitTests.Enums;

public class EnumsTests
{
    [Fact]
    public void RolUsuarioEnum_DebeContenerLosCuatroRolesDefinidos()
    {
        // Los valores son los identificadores de la tabla de roles: renumerarlos cambiaría el rol de los usuarios guardados.
        ((int)RolUsuarioEnum.Cajero).Should().Be(1);
        ((int)RolUsuarioEnum.Encargado).Should().Be(2);
        ((int)RolUsuarioEnum.Gerente).Should().Be(3);
        ((int)RolUsuarioEnum.Administrador).Should().Be(4);
        Enum.GetValues<RolUsuarioEnum>().Should().HaveCount(4);
    }

    [Fact]
    public void TipoBackupEnum_DebeContenerLosCuatroOrigenes()
    {
        ((int)TipoBackupEnum.Manual).Should().Be(1);
        ((int)TipoBackupEnum.Automatico).Should().Be(2);
        ((int)TipoBackupEnum.PreRestauracion).Should().Be(3);
        ((int)TipoBackupEnum.PreMigracion).Should().Be(4);
        Enum.GetValues<TipoBackupEnum>().Should().HaveCount(4);
    }

    [Fact]
    public void OrigenRestauracionEnum_DebeContenerLaAplicacionYLaConsola()
    {
        ((int)OrigenRestauracionEnum.Aplicacion).Should().Be(1);
        ((int)OrigenRestauracionEnum.Consola).Should().Be(2);
        Enum.GetValues<OrigenRestauracionEnum>().Should().HaveCount(2);
    }

    [Fact]
    public void EstadoBackupEnum_DebeContenerElCicloDeVidaCompleto()
    {
        ((int)EstadoBackupEnum.EnCurso).Should().Be(1);
        ((int)EstadoBackupEnum.Exitoso).Should().Be(2);
        ((int)EstadoBackupEnum.Verificado).Should().Be(3);
        ((int)EstadoBackupEnum.Fallido).Should().Be(4);
        ((int)EstadoBackupEnum.ArchivoPurgado).Should().Be(5);
        Enum.GetValues<EstadoBackupEnum>().Should().HaveCount(5);
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
