using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Retail.Application.DTOs.Ventas;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Services;
using Retail.Application.Validators.Ventas;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Retail.Infrastructure.IntegrationTests.TestData;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Repositories;
using Retail.Infrastructure.Persistence.Services;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Concurrencia optimista (Track 6.7, Paquete 1) sobre el turno de caja y el cliente con cuenta corriente: dos
/// contextos (dos terminales) cargan el mismo agregado, ambos lo modifican y el segundo guardado debe fallar con
/// <see cref="ConflictoDeConcurrenciaException"/> en lugar de pisar al primero. Son deterministas: sin hilos ni esperas.
/// </summary>
public class ConcurrenciaCajaTests : ListasPruebaBase
{
    public ConcurrenciaCajaTests()
        : base("ConcCaja")
    {
    }

    [Fact]
    public async Task Turno_CerradoEnUnContextoYVentaEnEfectivoEnOtro_LanzaConflicto()
    {
        // Arrange: la terminal A (encargado) y la terminal B (cajero) cargan el mismo turno abierto.
        var idTurno = await SembrarTurnoAsync();
        await using var contextoA = NuevoContexto();
        await using var contextoB = NuevoContexto();
        var turnoA = await contextoA.TurnosCaja.SingleAsync(t => t.Id == idTurno);
        var turnoB = await contextoB.TurnosCaja.SingleAsync(t => t.Id == idTurno);

        turnoA.CerrarConArqueoCiego(5000m, 0m);
        await new UnitOfWork(contextoA).SaveChangesAsync();

        turnoB.ImputarVenta(1500m, 0m);

        // Act
        var act = () => new UnitOfWork(contextoB).SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<ConflictoDeConcurrenciaException>();
    }

    [Fact]
    public async Task Turno_CerradoEnUnContextoYVentaSoloCuentaCorrienteEnOtro_LanzaConflicto()
    {
        // Arrange (H18): una venta 100 % a cuenta corriente no mueve dinero de la gaveta. Sin CantidadVentas, EF
        // Core no emitiría ningún UPDATE sobre el turno y el rowversion nunca se verificaría.
        var idTurno = await SembrarTurnoAsync();
        await using var contextoA = NuevoContexto();
        await using var contextoB = NuevoContexto();
        var turnoA = await contextoA.TurnosCaja.SingleAsync(t => t.Id == idTurno);
        var turnoB = await contextoB.TurnosCaja.SingleAsync(t => t.Id == idTurno);

        turnoA.CerrarConArqueoCiego(5000m, 0m);
        await new UnitOfWork(contextoA).SaveChangesAsync();

        // Igual que VentaService: se modifica la instancia ya rastreada y se guarda sin llamar a UpdateAsync.
        turnoB.ImputarVenta(0m, 0m);

        // Act
        var act = () => new UnitOfWork(contextoB).SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<ConflictoDeConcurrenciaException>();
    }

    [Fact]
    public async Task Cliente_DosVentasACreditoConcurrentes_LaSegundaLanzaConflicto()
    {
        // Arrange
        var idCliente = await SembrarClienteConCuentaCorrienteAsync();
        await using var contextoA = NuevoContexto();
        await using var contextoB = NuevoContexto();
        var clienteA = await contextoA.Clientes.SingleAsync(c => c.Id == idCliente);
        var clienteB = await contextoB.Clientes.SingleAsync(c => c.Id == idCliente);

        clienteA.DebitarCuentaCorriente(1000m);
        await new UnitOfWork(contextoA).SaveChangesAsync();

        clienteB.DebitarCuentaCorriente(1000m);

        // Act
        var act = () => new UnitOfWork(contextoB).SaveChangesAsync();

        // Assert: sin el token, el segundo UPDATE pisaría el saldo y la deuda quedaría en 1000 en lugar de 2000.
        await act.Should().ThrowAsync<ConflictoDeConcurrenciaException>();
    }

    [Fact]
    public async Task Cliente_EdicionDelLimiteYVentaACreditoConcurrentes_LaSegundaLanzaConflicto()
    {
        // Arrange: la edición ve deuda 0 y baja el límite a 500; la venta a crédito ve el límite viejo (10000).
        var idCliente = await SembrarClienteConCuentaCorrienteAsync();
        await using var contextoEdicion = NuevoContexto();
        await using var contextoVenta = NuevoContexto();
        var clienteEdicion = await contextoEdicion.Clientes.SingleAsync(c => c.Id == idCliente);
        var clienteVenta = await contextoVenta.Clientes.SingleAsync(c => c.Id == idCliente);

        clienteVenta.DebitarCuentaCorriente(3000m);
        await new UnitOfWork(contextoVenta).SaveChangesAsync();

        // La edición validó "límite >= deuda" contra una deuda vieja (0): solo el token de fila lo detecta.
        clienteEdicion.ModificarLimiteCredito(500m);

        // Act
        var act = () => new UnitOfWork(contextoEdicion).SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<ConflictoDeConcurrenciaException>();
    }

    [Fact]
    public async Task RegistrarVentaAsync_SoloCuentaCorrienteConTurnoCerradoEnOtraTerminal_LanzaCajaCerradaYNoGuardaLaVenta()
    {
        // Arrange: la terminal del cajero ya tiene el turno cargado (como el estado del POS, que vive mientras dura
        // la pantalla) cuando un encargado lo cierra desde otra terminal. Repository.GetByIdAsync devuelve la
        // instancia ya rastreada, así que VentaService ve el turno todavía "Abierto" sin necesidad de hilos.
        var idTurno = await SembrarTurnoAsync();
        var articulo = await SembrarArticuloAsync("Cuaderno", 1000m);
        var idCliente = await SembrarClienteConCuentaCorrienteAsync();
        Contexto.ChangeTracker.Clear();

        await using var contextoCajero = NuevoContexto();
        await contextoCajero.TurnosCaja.SingleAsync(t => t.Id == idTurno);
        var ventaService = CrearVentaService(contextoCajero);

        await using (var contextoEncargado = NuevoContexto())
        {
            var turnoDelEncargado = await contextoEncargado.TurnosCaja.SingleAsync(t => t.Id == idTurno);
            turnoDelEncargado.CerrarConArqueoCiego(5000m, 0m);
            await new UnitOfWork(contextoEncargado).SaveChangesAsync();
        }

        var dto = new CrearVentaDto
        {
            IdTurno = idTurno,
            IdUsuario = IdUsuario,
            IdCliente = idCliente,
            Items =
            [
                new DetalleVentaDto
                {
                    IdArticulo = articulo.Id,
                    Descripcion = articulo.Descripcion,
                    Cantidad = 1,
                    PrecioUnitario = articulo.PrecioVenta
                }
            ],
            Pagos = [new PagoVentaDto { MedioPago = MedioPagoEnum.CuentaCorriente, Monto = 1000m }]
        };

        // Act
        var act = () => ventaService.RegistrarVentaAsync(dto);

        // Assert: el rowversion del turno frena la venta aunque no movió efectivo, y no queda nada a medio guardar.
        await act.Should().ThrowAsync<CajaCerradaException>().WithMessage("*cerrado desde otra terminal*");
        Contexto.ChangeTracker.Clear();
        (await Contexto.Ventas.AsNoTracking().CountAsync()).Should().Be(0);
        (await Contexto.Clientes.AsNoTracking().SingleAsync(c => c.Id == idCliente)).SaldoCuentaCorriente.Should().Be(0m);
        (await Contexto.Articulos.AsNoTracking().SingleAsync(a => a.Id == articulo.Id)).StockActual.Should().Be(50);
    }

    private async Task<int> SembrarClienteConCuentaCorrienteAsync()
    {
        var cliente = ClientesDePrueba.Crear(razonSocialONombre: "Librería del Centro");
        cliente.HabilitarCuentaCorriente(10000m);
        Contexto.Clientes.Add(cliente);
        await Contexto.SaveChangesAsync();
        return cliente.Id;
    }

    private static VentaService CrearVentaService(RetailDbContext contexto)
    {
        return new VentaService(
            new Repository<Articulo>(contexto),
            new ArticuloQueryService(contexto),
            new Repository<Venta>(contexto),
            new Repository<TurnoCaja>(contexto),
            new Repository<Cliente>(contexto),
            new UnitOfWork(contexto),
            new CrearVentaValidator(),
            Substitute.For<ITicketPrinterService>(),
            new Repository<Presupuesto>(contexto),
            new PresupuestoQueryService(contexto),
            TimeProvider.System);
    }
}
