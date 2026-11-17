using System.Globalization;
using FluentAssertions;
using Retail.Application.DTOs.Presupuestos;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.IntegrationTests.TestData;
using Retail.Infrastructure.Persistence.Services;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Consultas de <see cref="PresupuestoQueryService"/> (RF-11, RF-12) contra SQL Server Express: filtros por vigencia
/// derivada de la fecha, búsqueda insensible a acentos y paginación, todo evaluado en SQL (Ley 8).
/// </summary>
public class PresupuestoQueryServiceTests : PedidosPruebaBase
{
    private static readonly DateOnly Hoy = new(2026, 11, 17);

    private Articulo? _articuloSembrado;

    public PresupuestoQueryServiceTests()
        : base("PresupuestoQuery")
    {
    }

    [Fact]
    public async Task ListarAsync_ConVariasPaginas_PaginaEnSqlYOrdenaPorEmisionDescendente()
    {
        // Arrange
        var ids = new List<int>();
        for (var i = 0; i < 5; i++)
        {
            ids.Add((await EmitirAsync(venceEn: 10)).Id);
        }

        var servicio = new PresupuestoQueryService(NuevoContexto());

        // Act
        var primera = await servicio.ListarAsync(new ConsultaPresupuestosDto { Pagina = 1, TamanoPagina = 2 }, Hoy);
        var tercera = await servicio.ListarAsync(new ConsultaPresupuestosDto { Pagina = 3, TamanoPagina = 2 }, Hoy);

        // Assert
        primera.TotalRegistros.Should().Be(5);
        primera.TotalPaginas.Should().Be(3);
        primera.Items.Select(p => p.IdPresupuesto).Should().Equal(ids[4], ids[3]);
        tercera.Items.Select(p => p.IdPresupuesto).Should().Equal(ids[0]);
    }

    [Fact]
    public async Task ListarAsync_FiltroVigentesYVencidos_RespetaElLimiteDelDia()
    {
        // Arrange: vence hoy (todavía vigente) y venció ayer (vencido)
        var venceHoy = await EmitirAsync(venceEn: 0);
        var venciAyer = await EmitirAsync(venceEn: -1);
        var venceManana = await EmitirAsync(venceEn: 1);
        var servicio = new PresupuestoQueryService(NuevoContexto());

        // Act
        var vigentes = await servicio.ListarAsync(new ConsultaPresupuestosDto { Filtro = FiltroEstadoPresupuestoEnum.Vigentes }, Hoy);
        var vencidos = await servicio.ListarAsync(new ConsultaPresupuestosDto { Filtro = FiltroEstadoPresupuestoEnum.Vencidos }, Hoy);

        // Assert
        vigentes.Items.Select(p => p.IdPresupuesto).Should().BeEquivalentTo([venceHoy.Id, venceManana.Id]);
        vigentes.Items.Should().OnlyContain(p => p.EstaVigente);
        vencidos.Items.Select(p => p.IdPresupuesto).Should().Equal(venciAyer.Id);
        vencidos.Items.Should().OnlyContain(p => !p.EstaVigente);
    }

    [Fact]
    public async Task ListarAsync_FiltrosPorEstado_SeparanConvertidosAnuladosYTodos()
    {
        // Arrange
        var pendiente = await EmitirAsync(venceEn: 5);
        var convertido = await EmitirAsync(venceEn: 5);
        convertido.RegistrarConversion();
        var anulado = await EmitirAsync(venceEn: 5);
        anulado.Anular();
        var vencidoAnulado = await EmitirAsync(venceEn: -3);
        vencidoAnulado.Anular();
        await Contexto.SaveChangesAsync();
        var servicio = new PresupuestoQueryService(NuevoContexto());

        // Act
        var convertidos = await servicio.ListarAsync(new ConsultaPresupuestosDto { Filtro = FiltroEstadoPresupuestoEnum.Convertidos }, Hoy);
        var anulados = await servicio.ListarAsync(new ConsultaPresupuestosDto { Filtro = FiltroEstadoPresupuestoEnum.Anulados }, Hoy);
        var vigentes = await servicio.ListarAsync(new ConsultaPresupuestosDto { Filtro = FiltroEstadoPresupuestoEnum.Vigentes }, Hoy);
        var vencidos = await servicio.ListarAsync(new ConsultaPresupuestosDto { Filtro = FiltroEstadoPresupuestoEnum.Vencidos }, Hoy);
        var todos = await servicio.ListarAsync(new ConsultaPresupuestosDto { Filtro = FiltroEstadoPresupuestoEnum.Todos }, Hoy);

        // Assert: Vigentes y Vencidos solo miran los pendientes
        convertidos.Items.Select(p => p.IdPresupuesto).Should().Equal(convertido.Id);
        anulados.Items.Select(p => p.IdPresupuesto).Should().BeEquivalentTo([anulado.Id, vencidoAnulado.Id]);
        vigentes.Items.Select(p => p.IdPresupuesto).Should().Equal(pendiente.Id);
        vencidos.Items.Should().BeEmpty();
        todos.TotalRegistros.Should().Be(4);
    }

    [Fact]
    public async Task ListarAsync_BusquedaPorNumero_EncuentraElPresupuesto()
    {
        // Arrange
        var buscado = await EmitirAsync(venceEn: 5);
        await EmitirAsync(venceEn: 5);
        var servicio = new PresupuestoQueryService(NuevoContexto());

        // Act
        var resultado = await servicio.ListarAsync(new ConsultaPresupuestosDto { TerminoBusqueda = buscado.Id.ToString(CultureInfo.InvariantCulture) }, Hoy);

        // Assert
        resultado.Items.Select(p => p.IdPresupuesto).Should().Equal(buscado.Id);
    }

    [Fact]
    public async Task ListarAsync_BusquedaPorTitulo_IgnoraTildesYMayusculas()
    {
        // Arrange
        var conTilde = await EmitirAsync(venceEn: 5, titulo: "Lista de lápices y útiles");
        await EmitirAsync(venceEn: 5, titulo: "Otra cosa");
        var servicio = new PresupuestoQueryService(NuevoContexto());

        // Act
        var sinTilde = await servicio.ListarAsync(new ConsultaPresupuestosDto { TerminoBusqueda = "LAPICES" }, Hoy);
        var conTildeBuscada = await servicio.ListarAsync(new ConsultaPresupuestosDto { TerminoBusqueda = "lápices" }, Hoy);

        // Assert
        sinTilde.Items.Select(p => p.IdPresupuesto).Should().Equal(conTilde.Id);
        conTildeBuscada.Items.Select(p => p.IdPresupuesto).Should().Equal(conTilde.Id);
        sinTilde.Items.Single().Titulo.Should().Be("Lista de lápices y útiles");
    }

    [Fact]
    public async Task ListarAsync_BusquedaPorCliente_EncuentraPorNombreYMuestraConsumidorFinalComoNulo()
    {
        // Arrange
        var cliente = ClientesDePrueba.Crear(razonSocialONombre: "Instituto Rivadavia");
        Contexto.Clientes.Add(cliente);
        await Contexto.SaveChangesAsync();
        var delCliente = await EmitirAsync(venceEn: 5, idCliente: cliente.Id);
        await EmitirAsync(venceEn: 5);
        var servicio = new PresupuestoQueryService(NuevoContexto());

        // Act
        var resultado = await servicio.ListarAsync(new ConsultaPresupuestosDto { TerminoBusqueda = "rivadavia" }, Hoy);
        var todos = await servicio.ListarAsync(new ConsultaPresupuestosDto(), Hoy);

        // Assert
        resultado.Items.Should().ContainSingle();
        resultado.Items[0].IdPresupuesto.Should().Be(delCliente.Id);
        resultado.Items[0].ClienteNombre.Should().Be("Instituto Rivadavia");
        resultado.Items[0].UsuarioNombre.Should().Be("Ana Perez");
        resultado.Items[0].CantidadRenglones.Should().Be(1);
        todos.Items.Count(p => p.ClienteNombre is null).Should().Be(1);
    }

    [Fact]
    public async Task ObtenerAsync_PresupuestoExistente_DevuelveRenglonesYDatosDeCabecera()
    {
        // Arrange
        var cliente = ClientesDePrueba.Crear(razonSocialONombre: "Instituto Rivadavia");
        Contexto.Clientes.Add(cliente);
        await Contexto.SaveChangesAsync();
        var emitido = await EmitirAsync(venceEn: 5, titulo: "Nota interna", idCliente: cliente.Id);
        var servicio = new PresupuestoQueryService(NuevoContexto());

        // Act
        var dto = await servicio.ObtenerAsync(emitido.Id, Hoy);

        // Assert
        dto.Should().NotBeNull();
        dto!.IdPresupuesto.Should().Be(emitido.Id);
        dto.ClienteNombre.Should().Be("Instituto Rivadavia");
        dto.UsuarioNombre.Should().Be("Ana Perez");
        dto.Titulo.Should().Be("Nota interna");
        dto.FechaVencimiento.Should().Be(Hoy.AddDays(5));
        dto.EstaVigente.Should().BeTrue();
        dto.Estado.Should().Be(EstadoPresupuestoEnum.Pendiente);
        dto.Total.Should().Be(2400m);
        var item = dto.Items.Should().ContainSingle().Subject;
        item.IdArticulo.Should().Be(_articuloSembrado!.Id);
        item.CodigoBarras.Should().Be("7791000000011");
        item.Descripcion.Should().Be("Cuaderno rayado");
        item.Cantidad.Should().Be(2);
        item.PrecioUnitarioPactado.Should().Be(1200m);
    }

    [Fact]
    public async Task ObtenerAsync_ConClienteDadoDeBajaDespuesDeEmitir_ConservaElNombreDelCliente()
    {
        // Arrange
        var cliente = ClientesDePrueba.Crear(razonSocialONombre: "Instituto Rivadavia");
        Contexto.Clientes.Add(cliente);
        await Contexto.SaveChangesAsync();
        var emitido = await EmitirAsync(venceEn: 5, idCliente: cliente.Id);
        cliente.MarkAsDeleted();
        await Contexto.SaveChangesAsync();
        var servicio = new PresupuestoQueryService(NuevoContexto());

        // Act
        var dto = await servicio.ObtenerAsync(emitido.Id, Hoy);
        var listado = await servicio.ListarAsync(new ConsultaPresupuestosDto { TerminoBusqueda = "rivadavia" }, Hoy);

        // Assert
        dto!.ClienteNombre.Should().Be("Instituto Rivadavia", "el documento emitido debe seguir diciendo a quién se le cotizó");
        listado.Items.Should().ContainSingle().Which.ClienteNombre.Should().Be("Instituto Rivadavia");
    }

    [Fact]
    public async Task ObtenerAsync_PresupuestoVencido_MarcaEstaVigenteEnFalse()
    {
        // Arrange
        var emitido = await EmitirAsync(venceEn: -1);
        var servicio = new PresupuestoQueryService(NuevoContexto());

        // Act
        var dto = await servicio.ObtenerAsync(emitido.Id, Hoy);

        // Assert
        dto!.EstaVigente.Should().BeFalse();
    }

    [Fact]
    public async Task ObtenerAsync_PresupuestoInexistente_DevuelveNull()
    {
        // Arrange
        var servicio = new PresupuestoQueryService(NuevoContexto());

        // Act
        var dto = await servicio.ObtenerAsync(9999, Hoy);

        // Assert
        dto.Should().BeNull();
    }

    /// <summary>
    /// Emite y guarda un presupuesto de 2 cuadernos a 1200. <paramref name="venceEn"/> son los días desde <c>Hoy</c> hasta el
    /// vencimiento (0 = vence hoy, -1 = venció ayer); se logra emitiendo con una fecha de emisión desplazada.
    /// </summary>
    private async Task<Presupuesto> EmitirAsync(int venceEn, string? titulo = null, int? idCliente = null)
    {
        var articulo = await ObtenerArticuloAsync();
        var presupuesto = Presupuesto.Emitir(
            IdUsuario,
            idCliente,
            titulo,
            [new RenglonAPresupuestar(articulo.Id, articulo.Descripcion, 2, 1200m)],
            descuento: 0m,
            vigenciaDias: Presupuesto.VigenciaMinimaDias,
            hoy: Hoy.AddDays(venceEn - Presupuesto.VigenciaMinimaDias));
        Contexto.Presupuestos.Add(presupuesto);
        await Contexto.SaveChangesAsync();
        return presupuesto;
    }

    private async Task<Articulo> ObtenerArticuloAsync()
    {
        if (_articuloSembrado is null)
        {
            _articuloSembrado = new Articulo
            {
                Descripcion = "Cuaderno rayado",
                CodigoBarras = "7791000000011",
                CostoReposicion = 600m,
                PorcentajeGanancia = 100m,
                PrecioVenta = 1200m,
                StockActual = 10,
                StockMinimo = 1
            };
            Contexto.Articulos.Add(_articuloSembrado);
            await Contexto.SaveChangesAsync();
        }

        return _articuloSembrado;
    }
}
