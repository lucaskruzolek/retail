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
        var vigentes = await servicio.ListarAsync(new ConsultaPresupuestosDto { Estados = [FiltroEstadoPresupuestoEnum.Vigentes] }, Hoy);
        var vencidos = await servicio.ListarAsync(new ConsultaPresupuestosDto { Estados = [FiltroEstadoPresupuestoEnum.Vencidos] }, Hoy);

        // Assert
        vigentes.Items.Select(p => p.IdPresupuesto).Should().BeEquivalentTo([venceHoy.Id, venceManana.Id]);
        vigentes.Items.Should().OnlyContain(p => p.EstaVigente);
        vencidos.Items.Select(p => p.IdPresupuesto).Should().Equal(venciAyer.Id);
        vencidos.Items.Should().OnlyContain(p => !p.EstaVigente);
    }

    [Fact]
    public async Task ListarAsync_FiltrosPorEstado_SeparanConvertidosAnuladosYSinFiltro()
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
        var convertidos = await servicio.ListarAsync(new ConsultaPresupuestosDto { Estados = [FiltroEstadoPresupuestoEnum.Convertidos] }, Hoy);
        var anulados = await servicio.ListarAsync(new ConsultaPresupuestosDto { Estados = [FiltroEstadoPresupuestoEnum.Anulados] }, Hoy);
        var vigentes = await servicio.ListarAsync(new ConsultaPresupuestosDto { Estados = [FiltroEstadoPresupuestoEnum.Vigentes] }, Hoy);
        var vencidos = await servicio.ListarAsync(new ConsultaPresupuestosDto { Estados = [FiltroEstadoPresupuestoEnum.Vencidos] }, Hoy);
        var todos = await servicio.ListarAsync(new ConsultaPresupuestosDto(), Hoy);

        // Assert: Vigentes y Vencidos solo miran los pendientes
        convertidos.Items.Select(p => p.IdPresupuesto).Should().Equal(convertido.Id);
        anulados.Items.Select(p => p.IdPresupuesto).Should().BeEquivalentTo([anulado.Id, vencidoAnulado.Id]);
        vigentes.Items.Select(p => p.IdPresupuesto).Should().Equal(pendiente.Id);
        vencidos.Items.Should().BeEmpty();
        todos.TotalRegistros.Should().Be(4);
    }

    [Fact]
    public async Task ListarAsync_FiltroReemplazados_DevuelveSoloLosReemplazadosYLosVigentesLosExcluyen()
    {
        // Arrange
        var original = await EmitirAsync(venceEn: 5);
        original.Reemplazar();
        var nuevo = await EmitirReemplazoAsync(original.Id);
        await Contexto.SaveChangesAsync();
        var servicio = new PresupuestoQueryService(NuevoContexto());

        // Act
        var reemplazados = await servicio.ListarAsync(new ConsultaPresupuestosDto { Estados = [FiltroEstadoPresupuestoEnum.Reemplazados] }, Hoy);
        var pendientes = await servicio.ListarAsync(new ConsultaPresupuestosDto { Estados = [FiltroEstadoPresupuestoEnum.Vigentes, FiltroEstadoPresupuestoEnum.Vencidos] }, Hoy);

        // Assert: Vigentes y Vencidos solo miran los pendientes
        reemplazados.Items.Select(p => p.IdPresupuesto).Should().Equal(original.Id);
        pendientes.Items.Select(p => p.IdPresupuesto).Should().Equal(nuevo.Id);
    }

    [Fact]
    public async Task ListarAsync_PresupuestoReemplazado_ResuelveElReemplazanteYElReemplazado()
    {
        // Arrange
        var original = await EmitirAsync(venceEn: 5);
        original.Reemplazar();
        var nuevo = await EmitirReemplazoAsync(original.Id);
        var sinRelacion = await EmitirAsync(venceEn: 5);
        await Contexto.SaveChangesAsync();
        var servicio = new PresupuestoQueryService(NuevoContexto());

        // Act
        var pagina = await servicio.ListarAsync(new ConsultaPresupuestosDto(), Hoy);

        // Assert
        var filaOriginal = pagina.Items.Single(p => p.IdPresupuesto == original.Id);
        filaOriginal.IdPresupuestoReemplazante.Should().Be(nuevo.Id);
        filaOriginal.IdPresupuestoReemplazado.Should().BeNull();
        var filaNueva = pagina.Items.Single(p => p.IdPresupuesto == nuevo.Id);
        filaNueva.IdPresupuestoReemplazado.Should().Be(original.Id);
        filaNueva.IdPresupuestoReemplazante.Should().BeNull();
        pagina.Items.Single(p => p.IdPresupuesto == sinRelacion.Id).IdPresupuestoReemplazante.Should().BeNull();
    }

    [Fact]
    public async Task ObtenerAsync_PresupuestoReemplazado_ResuelveElReemplazante()
    {
        // Arrange
        var original = await EmitirAsync(venceEn: 5);
        original.Reemplazar();
        var nuevo = await EmitirReemplazoAsync(original.Id);
        await Contexto.SaveChangesAsync();
        var servicio = new PresupuestoQueryService(NuevoContexto());

        // Act
        var dtoOriginal = await servicio.ObtenerAsync(original.Id, Hoy);
        var dtoNuevo = await servicio.ObtenerAsync(nuevo.Id, Hoy);

        // Assert
        dtoOriginal!.Estado.Should().Be(EstadoPresupuestoEnum.Reemplazado);
        dtoOriginal.IdPresupuestoReemplazante.Should().Be(nuevo.Id);
        dtoNuevo!.IdPresupuestoReemplazado.Should().Be(original.Id);
        dtoNuevo.IdPresupuestoReemplazante.Should().BeNull();
    }

    [Fact]
    public async Task ListarAsync_VigentesYVencidosJuntos_CombinaLosEstadosConO()
    {
        // Arrange
        var vigente = await EmitirAsync(venceEn: 3);
        var vencido = await EmitirAsync(venceEn: -2);
        var convertido = await EmitirAsync(venceEn: 3);
        convertido.RegistrarConversion();
        await Contexto.SaveChangesAsync();
        var servicio = new PresupuestoQueryService(NuevoContexto());

        // Act
        var resultado = await servicio.ListarAsync(
            new ConsultaPresupuestosDto { Estados = [FiltroEstadoPresupuestoEnum.Vigentes, FiltroEstadoPresupuestoEnum.Vencidos] },
            Hoy);

        // Assert
        resultado.Items.Select(p => p.IdPresupuesto).Should().BeEquivalentTo([vigente.Id, vencido.Id]);
        resultado.TotalRegistros.Should().Be(2);
    }

    [Fact]
    public async Task ListarAsync_ConvertidosYAnuladosJuntos_ExcluyeLosPendientes()
    {
        // Arrange
        await EmitirAsync(venceEn: 3);
        await EmitirAsync(venceEn: -2);
        var convertido = await EmitirAsync(venceEn: 3);
        convertido.RegistrarConversion();
        var anulado = await EmitirAsync(venceEn: 3);
        anulado.Anular();
        await Contexto.SaveChangesAsync();
        var servicio = new PresupuestoQueryService(NuevoContexto());

        // Act
        var resultado = await servicio.ListarAsync(
            new ConsultaPresupuestosDto { Estados = [FiltroEstadoPresupuestoEnum.Convertidos, FiltroEstadoPresupuestoEnum.Anulados] },
            Hoy);

        // Assert
        resultado.Items.Select(p => p.IdPresupuesto).Should().BeEquivalentTo([convertido.Id, anulado.Id]);
    }

    [Fact]
    public async Task ListarAsync_FiltroPorClientePorId_DevuelveSoloLosDeEseCliente()
    {
        // Arrange
        var ana = await CrearClienteAsync("Ana Pérez");
        var beto = await CrearClienteAsync("Beto Gómez");
        var deAna = await EmitirAsync(venceEn: 5, idCliente: ana.Id);
        await EmitirAsync(venceEn: 5, idCliente: beto.Id);
        await EmitirAsync(venceEn: 5);
        var servicio = new PresupuestoQueryService(NuevoContexto());

        // Act
        var resultado = await servicio.ListarAsync(new ConsultaPresupuestosDto { IdsCliente = [ana.Id] }, Hoy);

        // Assert
        resultado.Items.Select(p => p.IdPresupuesto).Should().Equal(deAna.Id);
    }

    [Fact]
    public async Task ListarAsync_SoloConsumidorFinal_DevuelveLosPresupuestosSinCliente()
    {
        // Arrange
        var ana = await CrearClienteAsync("Ana Pérez");
        await EmitirAsync(venceEn: 5, idCliente: ana.Id);
        var sinCliente = await EmitirAsync(venceEn: 5);
        var servicio = new PresupuestoQueryService(NuevoContexto());

        // Act
        var resultado = await servicio.ListarAsync(new ConsultaPresupuestosDto { IncluirConsumidorFinal = true }, Hoy);

        // Assert
        resultado.Items.Select(p => p.IdPresupuesto).Should().Equal(sinCliente.Id);
    }

    [Fact]
    public async Task ListarAsync_ClienteMasConsumidorFinal_CombinaAmbosConO()
    {
        // Arrange
        var ana = await CrearClienteAsync("Ana Pérez");
        var beto = await CrearClienteAsync("Beto Gómez");
        var deAna = await EmitirAsync(venceEn: 5, idCliente: ana.Id);
        await EmitirAsync(venceEn: 5, idCliente: beto.Id);
        var sinCliente = await EmitirAsync(venceEn: 5);
        var servicio = new PresupuestoQueryService(NuevoContexto());

        // Act
        var resultado = await servicio.ListarAsync(
            new ConsultaPresupuestosDto { IdsCliente = [ana.Id], IncluirConsumidorFinal = true },
            Hoy);

        // Assert
        resultado.Items.Select(p => p.IdPresupuesto).Should().BeEquivalentTo([deAna.Id, sinCliente.Id]);
    }

    [Fact]
    public async Task ListarAsync_RangoDeEmision_RespetaLosLimitesDelIntervaloSemiabierto()
    {
        // Arrange: la emisión es "ahora"; se fija a mano para probar los bordes
        var desde = new DateTime(2026, 11, 10, 3, 0, 0, DateTimeKind.Utc);
        var hasta = new DateTime(2026, 11, 12, 3, 0, 0, DateTimeKind.Utc);
        var antes = await EmitirConEmisionAsync(desde.AddMinutes(-1));
        var enElInicio = await EmitirConEmisionAsync(desde);
        var enElMedio = await EmitirConEmisionAsync(desde.AddDays(1));
        var enElFinExclusivo = await EmitirConEmisionAsync(hasta);
        var servicio = new PresupuestoQueryService(NuevoContexto());

        // Act
        var rango = await servicio.ListarAsync(new ConsultaPresupuestosDto { EmisionDesdeUtc = desde, EmisionHastaUtc = hasta }, Hoy);
        var soloDesde = await servicio.ListarAsync(new ConsultaPresupuestosDto { EmisionDesdeUtc = desde }, Hoy);
        var soloHasta = await servicio.ListarAsync(new ConsultaPresupuestosDto { EmisionHastaUtc = hasta }, Hoy);

        // Assert: el inicio entra y el fin no
        rango.Items.Select(p => p.IdPresupuesto).Should().BeEquivalentTo([enElInicio.Id, enElMedio.Id]);
        soloDesde.Items.Select(p => p.IdPresupuesto).Should().BeEquivalentTo([enElInicio.Id, enElMedio.Id, enElFinExclusivo.Id]);
        soloHasta.Items.Select(p => p.IdPresupuesto).Should().BeEquivalentTo([antes.Id, enElInicio.Id, enElMedio.Id]);
    }

    [Fact]
    public async Task ListarAsync_RangoDeVencimiento_IncluyeAmbosExtremos()
    {
        // Arrange
        var antes = await EmitirAsync(venceEn: 4);
        var enElInicio = await EmitirAsync(venceEn: 5);
        var enElMedio = await EmitirAsync(venceEn: 6);
        var enElFin = await EmitirAsync(venceEn: 7);
        var despues = await EmitirAsync(venceEn: 8);
        var servicio = new PresupuestoQueryService(NuevoContexto());

        // Act
        var rango = await servicio.ListarAsync(new ConsultaPresupuestosDto { VenceDesde = Hoy.AddDays(5), VenceHasta = Hoy.AddDays(7) }, Hoy);
        var soloDesde = await servicio.ListarAsync(new ConsultaPresupuestosDto { VenceDesde = Hoy.AddDays(7) }, Hoy);
        var soloHasta = await servicio.ListarAsync(new ConsultaPresupuestosDto { VenceHasta = Hoy.AddDays(4) }, Hoy);

        // Assert
        rango.Items.Select(p => p.IdPresupuesto).Should().BeEquivalentTo([enElInicio.Id, enElMedio.Id, enElFin.Id]);
        soloDesde.Items.Select(p => p.IdPresupuesto).Should().BeEquivalentTo([enElFin.Id, despues.Id]);
        soloHasta.Items.Select(p => p.IdPresupuesto).Should().Equal(antes.Id);
    }

    [Fact]
    public async Task ListarAsync_EstadoClienteYFechaCombinados_CombinaLosFiltrosConY()
    {
        // Arrange
        var ana = await CrearClienteAsync("Ana Pérez");
        var beto = await CrearClienteAsync("Beto Gómez");
        var buscado = await EmitirAsync(venceEn: 6, idCliente: ana.Id);
        await EmitirAsync(venceEn: 6, idCliente: beto.Id);               // otro cliente
        await EmitirAsync(venceEn: -6, idCliente: ana.Id);               // vencido
        await EmitirAsync(venceEn: 20, idCliente: ana.Id);               // vence fuera del rango
        var anulado = await EmitirAsync(venceEn: 6, idCliente: ana.Id);  // no es vigente
        anulado.Anular();
        await Contexto.SaveChangesAsync();
        var servicio = new PresupuestoQueryService(NuevoContexto());

        // Act
        var resultado = await servicio.ListarAsync(
            new ConsultaPresupuestosDto
            {
                Estados = [FiltroEstadoPresupuestoEnum.Vigentes],
                IdsCliente = [ana.Id],
                VenceDesde = Hoy.AddDays(5),
                VenceHasta = Hoy.AddDays(10)
            },
            Hoy);

        // Assert
        resultado.Items.Select(p => p.IdPresupuesto).Should().Equal(buscado.Id);
        resultado.TotalRegistros.Should().Be(1);
    }

    [Fact]
    public async Task ListarClientesConPresupuestosAsync_ConVariosPresupuestos_DevuelveClientesDistintosOrdenadosYSinConsumidorFinal()
    {
        // Arrange
        var zulema = await CrearClienteAsync("Zulema Díaz");
        var ana = await CrearClienteAsync("Ana Pérez");
        var sinPresupuestos = await CrearClienteAsync("Carlos Sin Presupuestos");
        await EmitirAsync(venceEn: 5, idCliente: zulema.Id);
        await EmitirAsync(venceEn: 5, idCliente: ana.Id);
        await EmitirAsync(venceEn: 5, idCliente: ana.Id);
        await EmitirAsync(venceEn: 5);
        var servicio = new PresupuestoQueryService(NuevoContexto());

        // Act
        var clientes = await servicio.ListarClientesConPresupuestosAsync();

        // Assert
        clientes.Select(c => c.Nombre).Should().Equal("Ana Pérez", "Zulema Díaz");
        clientes.Select(c => c.IdCliente).Should().Equal(ana.Id, zulema.Id);
        clientes.Should().NotContain(c => c.IdCliente == sinPresupuestos.Id);
    }

    [Fact]
    public async Task ListarClientesConPresupuestosAsync_ConClienteDadoDeBaja_LoIncluye()
    {
        // Arrange
        var ana = await CrearClienteAsync("Ana Pérez");
        var baja = await CrearClienteAsync("Beto Gómez");
        await EmitirAsync(venceEn: 5, idCliente: ana.Id);
        await EmitirAsync(venceEn: 5, idCliente: baja.Id);
        baja.MarkAsDeleted();
        await Contexto.SaveChangesAsync();
        var servicio = new PresupuestoQueryService(NuevoContexto());

        // Act
        var clientes = await servicio.ListarClientesConPresupuestosAsync();

        // Assert: su presupuesto sigue mostrando su nombre, así que sigue siendo una opción
        clientes.Select(c => c.Nombre).Should().Equal("Ana Pérez", "Beto Gómez");
    }

    [Fact]
    public async Task ListarClientesConPresupuestosAsync_SinPresupuestos_DevuelveListaVacia()
    {
        // Arrange
        await CrearClienteAsync("Ana Pérez");
        var servicio = new PresupuestoQueryService(NuevoContexto());

        // Act
        var clientes = await servicio.ListarClientesConPresupuestosAsync();

        // Assert
        clientes.Should().BeEmpty();
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

    private async Task<Presupuesto> EmitirReemplazoAsync(int idReemplazado)
    {
        var articulo = await ObtenerArticuloAsync();
        var presupuesto = Presupuesto.Emitir(
            IdUsuario,
            idCliente: null,
            titulo: null,
            [new RenglonAPresupuestar(articulo.Id, articulo.Descripcion, 2, 1200m)],
            descuento: 0m,
            vigenciaDias: 15,
            hoy: Hoy,
            idPresupuestoReemplazado: idReemplazado);
        Contexto.Presupuestos.Add(presupuesto);
        await Contexto.SaveChangesAsync();
        return presupuesto;
    }

    private async Task<Cliente> CrearClienteAsync(string nombre)
    {
        var cliente = ClientesDePrueba.Crear(razonSocialONombre: nombre);
        Contexto.Clientes.Add(cliente);
        await Contexto.SaveChangesAsync();
        return cliente;
    }

    // La fecha de emisión la fija el dominio con "ahora": para probar el rango se reescribe antes de consultar
    private async Task<Presupuesto> EmitirConEmisionAsync(DateTime emisionUtc)
    {
        var presupuesto = await EmitirAsync(venceEn: 5);
        Contexto.Entry(presupuesto).Property(p => p.FechaEmision).CurrentValue = emisionUtc;
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
