using System.Linq.Expressions;
using FluentAssertions;
using NSubstitute;
using Retail.Application.DTOs.Clientes;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Services;
using Retail.Application.Validators.Clientes;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.Application.UnitTests.Services;

public class ClienteServiceTests
{
    private readonly IRepository<Cliente> _clienteRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CrearClienteValidator _crearValidator;
    private readonly ActualizarClienteValidator _actualizarValidator;
    private readonly RegistrarCobranzaValidator _cobranzaValidator;
    private readonly Retail.Application.Interfaces.Services.ICajaService _cajaService;
    private readonly Retail.Application.Interfaces.Infrastructure.ITicketPrinterService _ticketPrinterService;
    private readonly ClienteService _sut;

    public ClienteServiceTests()
    {
        _clienteRepository = Substitute.For<IRepository<Cliente>>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _crearValidator = new CrearClienteValidator();
        _actualizarValidator = new ActualizarClienteValidator();
        _cobranzaValidator = new RegistrarCobranzaValidator();
        _cajaService = Substitute.For<Retail.Application.Interfaces.Services.ICajaService>();
        _ticketPrinterService = Substitute.For<Retail.Application.Interfaces.Infrastructure.ITicketPrinterService>();

        _sut = new ClienteService(
            _clienteRepository,
            _unitOfWork,
            _crearValidator,
            _actualizarValidator,
            _cobranzaValidator,
            _cajaService,
            _ticketPrinterService);
    }

    [Fact]
    public async Task BuscarClientesAsync_TerminoVacio_RetornaTodosOrdenadosPorNombre()
    {
        // Arrange
        var clientes = new List<Cliente>
        {
            new() { Id = 1, RazonSocialONombre = "Zeta Librería", NumeroDocumento = "30-11111111-1" },
            new() { Id = 2, RazonSocialONombre = "Alfa Papelería", NumeroDocumento = "30-22222222-2" }
        };

        _clienteRepository.ListAllAsync(false, Arg.Any<CancellationToken>())
            .Returns(clientes);

        // Act
        var result = await _sut.BuscarClientesAsync("");

        // Assert
        result.Should().HaveCount(2);
        result[0].RazonSocialONombre.Should().Be("Alfa Papelería");
        result[1].RazonSocialONombre.Should().Be("Zeta Librería");
    }

    [Fact]
    public async Task BuscarClientesAsync_FiltroPorDocumentoONombre_RetornaFiltrados()
    {
        // Arrange
        var clientes = new List<Cliente>
        {
            new() { Id = 1, RazonSocialONombre = "Librería Central", NumeroDocumento = "30-11111111-1" },
            new() { Id = 2, RazonSocialONombre = "Papelería Sur", NumeroDocumento = "30-99999999-9" }
        };

        _clienteRepository.ListAllAsync(false, Arg.Any<CancellationToken>())
            .Returns(clientes);

        // Act
        var result = await _sut.BuscarClientesAsync("9999");

        // Assert
        result.Should().ContainSingle(c => c.RazonSocialONombre == "Papelería Sur");
    }

    [Fact]
    public async Task ObtenerClientePorIdAsync_ClienteExiste_RetornaDto()
    {
        // Arrange
        var cliente = new Cliente
        {
            Id = 5,
            RazonSocialONombre = "Cliente Uno",
            NumeroDocumento = "30-12345678-9",
            TieneCuentaCorriente = true,
            LimiteCredito = 20000m,
            SaldoCuentaCorriente = 5000m
        };

        _clienteRepository.GetByIdAsync(5, Arg.Any<CancellationToken>())
            .Returns(cliente);

        // Act
        var result = await _sut.ObtenerClientePorIdAsync(5);

        // Assert
        result.Should().NotBeNull();
        result!.IdCliente.Should().Be(5);
        result.CreditoDisponible.Should().Be(15000m);
    }

    [Fact]
    public async Task ObtenerClientePorIdAsync_ClienteNoExiste_RetornaNull()
    {
        // Arrange
        _clienteRepository.GetByIdAsync(99, Arg.Any<CancellationToken>())
            .Returns((Cliente?)null);

        // Act
        var result = await _sut.ObtenerClientePorIdAsync(99);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CrearClienteAsync_DatosValidos_AgregaPersisteYRetornaDto()
    {
        // Arrange
        var dto = new CrearClienteDto
        {
            RazonSocialONombre = "Distribuidora Norte",
            TipoDocumento = TipoDocumentoEnum.Cuit,
            NumeroDocumento = "30-33445566-7",
            CondicionIva = CondicionIvaEnum.ResponsableInscripto,
            TieneCuentaCorriente = true,
            LimiteCredito = 60000m
        };

        _clienteRepository.FindAsync(Arg.Any<Expression<Func<Cliente, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Cliente>());

        // Act
        var result = await _sut.CrearClienteAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.RazonSocialONombre.Should().Be("Distribuidora Norte");
        result.LimiteCredito.Should().Be(60000m);
        await _clienteRepository.Received(1).AddAsync(Arg.Any<Cliente>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CrearClienteAsync_DocumentoDuplicado_LanzaInvalidOperationException()
    {
        // Arrange
        var dto = new CrearClienteDto
        {
            RazonSocialONombre = "Distribuidora Norte",
            TipoDocumento = TipoDocumentoEnum.Cuit,
            NumeroDocumento = "30-33445566-7",
            CondicionIva = CondicionIvaEnum.ResponsableInscripto
        };

        _clienteRepository.FindAsync(Arg.Any<Expression<Func<Cliente, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Cliente> { new() { Id = 1, NumeroDocumento = "30-33445566-7" } });

        // Act
        var act = async () => await _sut.CrearClienteAsync(dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Ya existe un cliente activo*");
        await _clienteRepository.DidNotReceive().AddAsync(Arg.Any<Cliente>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActualizarClienteAsync_DatosValidos_ActualizaYPersiste()
    {
        // Arrange
        var clienteExistente = new Cliente
        {
            Id = 3,
            RazonSocialONombre = "Nombre Original",
            NumeroDocumento = "30-11111111-1",
            TieneCuentaCorriente = false,
            LimiteCredito = 0m
        };

        var dto = new ActualizarClienteDto
        {
            IdCliente = 3,
            RazonSocialONombre = "Nombre Modificado",
            TipoDocumento = TipoDocumentoEnum.Cuit,
            NumeroDocumento = "30-11111111-1",
            CondicionIva = CondicionIvaEnum.ResponsableInscripto,
            TieneCuentaCorriente = true,
            LimiteCredito = 25000m
        };

        _clienteRepository.GetByIdAsync(3, Arg.Any<CancellationToken>())
            .Returns(clienteExistente);

        _clienteRepository.FindAsync(Arg.Any<Expression<Func<Cliente, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Cliente>());

        // Act
        await _sut.ActualizarClienteAsync(dto);

        // Assert
        clienteExistente.RazonSocialONombre.Should().Be("Nombre Modificado");
        clienteExistente.TieneCuentaCorriente.Should().BeTrue();
        clienteExistente.LimiteCredito.Should().Be(25000m);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActualizarClienteAsync_ClienteInexistente_LanzaKeyNotFoundException()
    {
        // Arrange
        var dto = new ActualizarClienteDto
        {
            IdCliente = 99,
            RazonSocialONombre = "No Existe",
            TipoDocumento = TipoDocumentoEnum.Dni,
            NumeroDocumento = "12345678",
            CondicionIva = CondicionIvaEnum.ConsumidorFinal,
            TieneCuentaCorriente = false,
            LimiteCredito = 0m
        };

        _clienteRepository.GetByIdAsync(99, Arg.Any<CancellationToken>())
            .Returns((Cliente?)null);

        // Act
        var act = async () => await _sut.ActualizarClienteAsync(dto);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ActualizarClienteAsync_DocumentoColisionaConOtroCliente_LanzaInvalidOperationException()
    {
        // Arrange
        var clienteExistente = new Cliente
        {
            Id = 3,
            RazonSocialONombre = "Cliente Tres",
            NumeroDocumento = "30-11111111-1"
        };

        var dto = new ActualizarClienteDto
        {
            IdCliente = 3,
            RazonSocialONombre = "Cliente Tres",
            TipoDocumento = TipoDocumentoEnum.Cuit,
            NumeroDocumento = "30-22222222-2",
            CondicionIva = CondicionIvaEnum.ResponsableInscripto,
            TieneCuentaCorriente = false,
            LimiteCredito = 0m
        };

        _clienteRepository.GetByIdAsync(3, Arg.Any<CancellationToken>())
            .Returns(clienteExistente);

        _clienteRepository.FindAsync(Arg.Any<Expression<Func<Cliente, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Cliente> { new() { Id = 4, NumeroDocumento = "30-22222222-2" } });

        // Act
        var act = async () => await _sut.ActualizarClienteAsync(dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Ya existe otro cliente activo*");
    }

    [Fact]
    public async Task BajaClienteAsync_ClienteValidoSinDeuda_MarcaComoEliminadoYPersiste()
    {
        // Arrange
        var cliente = new Cliente
        {
            Id = 7,
            RazonSocialONombre = "Cliente Para Borrar",
            SaldoCuentaCorriente = 0m
        };

        _clienteRepository.GetByIdAsync(7, Arg.Any<CancellationToken>())
            .Returns(cliente);

        // Act
        await _sut.BajaClienteAsync(7);

        // Assert
        cliente.IsDeleted.Should().BeTrue();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DebitarCuentaCorrienteAsync_ClienteValido_ImputaDebitoYPersiste()
    {
        // Arrange
        var cliente = new Cliente
        {
            Id = 8,
            TieneCuentaCorriente = true,
            LimiteCredito = 50000m,
            SaldoCuentaCorriente = 10000m
        };

        _clienteRepository.GetByIdAsync(8, Arg.Any<CancellationToken>())
            .Returns(cliente);

        // Act
        await _sut.DebitarCuentaCorrienteAsync(8, 15000m);

        // Assert
        cliente.SaldoCuentaCorriente.Should().Be(25000m);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarCobranzaAsync_ConTurnoActivoValido_ReduceSaldoImputaEnCajaEmiteTicketYRetornaResultado()
    {
        // Arrange
        var cliente = new Cliente
        {
            Id = 10,
            RazonSocialONombre = "Papelería Central",
            TieneCuentaCorriente = true,
            LimiteCredito = 50000m,
            SaldoCuentaCorriente = 20000m
        };

        var turno = new Retail.Application.DTOs.Caja.TurnoCajaDto
        {
            IdTurno = 1,
            IdUsuario = 1,
            NombreUsuario = "admin",
            FechaApertura = DateTime.UtcNow.Date,
            SaldoInicial = 5000m,
            Estado = EstadoTurnoEnum.Abierto
        };

        _cajaService.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>())
            .Returns(turno);

        _clienteRepository.GetByIdWithIncludesAsync(10, Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Cliente, object>>[]>())
            .Returns(cliente);

        var dto = new RegistrarCobranzaDto
        {
            IdCliente = 10,
            IdTurno = 1,
            IdUsuario = 1,
            Monto = 8000m,
            MedioPago = MedioPagoEnum.Efectivo,
            Referencia = "Recibo #101"
        };

        // Act
        var resultado = await _sut.RegistrarCobranzaAsync(dto);

        // Assert
        resultado.Should().NotBeNull();
        resultado.IdCliente.Should().Be(10);
        resultado.ClienteNombre.Should().Be("Papelería Central");
        resultado.MontoAbonado.Should().Be(8000m);
        resultado.SaldoAnterior.Should().Be(20000m);
        resultado.NuevoSaldo.Should().Be(12000m);
        resultado.MedioPago.Should().Be(MedioPagoEnum.Efectivo);
        resultado.Referencia.Should().Be("Recibo #101");

        cliente.SaldoCuentaCorriente.Should().Be(12000m);
        cliente.Cobranzas.Should().ContainSingle();

        await _cajaService.Received(1).RegistrarIngresoCobranzaAsync(1, 8000m, MedioPagoEnum.Efectivo, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _ticketPrinterService.Received(1).ImprimirReciboCobranzaAsync(resultado, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarCobranzaAsync_SinTurnoActivo_LanzaInvalidOperationException()
    {
        // Arrange
        _cajaService.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>())
            .Returns((Retail.Application.DTOs.Caja.TurnoCajaDto?)null);

        var dto = new RegistrarCobranzaDto
        {
            IdCliente = 1,
            IdTurno = 1,
            IdUsuario = 1,
            Monto = 1000m,
            MedioPago = MedioPagoEnum.Efectivo
        };

        // Act
        var act = async () => await _sut.RegistrarCobranzaAsync(dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*sin un turno de caja activo*");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarCobranzaAsync_TurnoDiferenteAlActivo_LanzaInvalidOperationException()
    {
        // Arrange
        var turno = new Retail.Application.DTOs.Caja.TurnoCajaDto
        {
            IdTurno = 2,
            IdUsuario = 1,
            FechaApertura = DateTime.UtcNow.Date,
            SaldoInicial = 1000m,
            Estado = EstadoTurnoEnum.Abierto
        };

        _cajaService.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>())
            .Returns(turno);

        var dto = new RegistrarCobranzaDto
        {
            IdCliente = 1,
            IdTurno = 1, // Difiere del activo (2)
            IdUsuario = 1,
            Monto = 1000m,
            MedioPago = MedioPagoEnum.Efectivo
        };

        // Act
        var act = async () => await _sut.RegistrarCobranzaAsync(dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no coincide con el turno activo*");
    }

    [Fact]
    public async Task RegistrarCobranzaAsync_SiFallaTicketPrinter_NoRevierteLaTransaccionFinanciera()
    {
        // Arrange
        var cliente = new Cliente
        {
            Id = 4,
            RazonSocialONombre = "Cliente Impresión Falla",
            TieneCuentaCorriente = true,
            SaldoCuentaCorriente = 5000m
        };

        var turno = new Retail.Application.DTOs.Caja.TurnoCajaDto
        {
            IdTurno = 1,
            IdUsuario = 1,
            FechaApertura = DateTime.UtcNow.Date,
            SaldoInicial = 1000m,
            Estado = EstadoTurnoEnum.Abierto
        };

        _cajaService.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns(turno);
        _clienteRepository.GetByIdWithIncludesAsync(4, Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Cliente, object>>[]>())
            .Returns(cliente);

        _ticketPrinterService.When(x => x.ImprimirReciboCobranzaAsync(Arg.Any<CobranzaResultadoDto>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new IOException("Impresora de tickets fuera de línea."));

        var dto = new RegistrarCobranzaDto
        {
            IdCliente = 4,
            IdTurno = 1,
            IdUsuario = 1,
            Monto = 2000m,
            MedioPago = MedioPagoEnum.Efectivo
        };

        // Act
        var resultado = await _sut.RegistrarCobranzaAsync(dto);

        // Assert
        resultado.NuevoSaldo.Should().Be(3000m);
        cliente.SaldoCuentaCorriente.Should().Be(3000m);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListarHistorialCobranzasAsync_ConCobranzas_RetornaListaOrdenadaDescendente()
    {
        // Arrange
        var cliente = new Cliente
        {
            Id = 6,
            TieneCuentaCorriente = true,
            SaldoCuentaCorriente = 0m
        };

        var cobranzaAntigua = new CobranzaCliente
        {
            Id = 1,
            IdCliente = 6,
            IdTurno = 1,
            IdUsuario = 1,
            Monto = 1000m,
            MedioPago = MedioPagoEnum.Efectivo,
            FechaHora = DateTime.UtcNow.AddDays(-2)
        };

        var cobranzaReciente = new CobranzaCliente
        {
            Id = 2,
            IdCliente = 6,
            IdTurno = 1,
            IdUsuario = 1,
            Monto = 2000m,
            MedioPago = MedioPagoEnum.TransferenciaQr,
            FechaHora = DateTime.UtcNow
        };

        cliente.Cobranzas.Add(cobranzaAntigua);
        cliente.Cobranzas.Add(cobranzaReciente);

        _clienteRepository.GetByIdWithIncludesAsync(6, Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Cliente, object>>[]>())
            .Returns(cliente);

        // Act
        var historial = await _sut.ListarHistorialCobranzasAsync(6);

        // Assert
        historial.Should().HaveCount(2);
        historial[0].IdCobranza.Should().Be(2); // La más reciente primero
        historial[1].IdCobranza.Should().Be(1);
    }
}
