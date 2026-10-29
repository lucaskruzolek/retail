using FluentAssertions;
using Retail.App.ViewModels.Pedidos;
using Retail.Application.DTOs.Pedidos;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class PedirArticulosViewModelTests
{
    private static RenglonVistaPreviaDto CrearRenglon(int idCatalogo, int idProveedor, decimal costo = 100m, int stock = 5, int minimo = 2, int enBorrador = 0)
    {
        return new RenglonVistaPreviaDto
        {
            IdCatalogoProveedor = idCatalogo,
            IdProveedor = idProveedor,
            CodigoProveedor = $"COD-{idCatalogo}",
            Descripcion = $"Ítem {idCatalogo}",
            CostoActual = costo,
            IdArticulo = idCatalogo + 100,
            StockActual = stock,
            StockMinimo = minimo,
            CantidadEnBorrador = enBorrador
        };
    }

    /// <summary>Proveedor 1 con borrador #7 (2 renglones) y dos ítems; proveedor 2 sin borrador y un ítem.</summary>
    private static VistaPreviaPedidoDto CrearVistaPrevia(params RenglonOmitidoDto[] omitidos)
    {
        return new VistaPreviaPedidoDto
        {
            Destinos = new[]
            {
                new DestinoPedidoDto { IdProveedor = 1, RazonSocialProveedor = "Alfa", IdBorrador = 7, CantidadRenglonesBorrador = 2 },
                new DestinoPedidoDto { IdProveedor = 2, RazonSocialProveedor = "Zeta" }
            },
            Renglones = new[]
            {
                CrearRenglon(10, 1, costo: 1.005m),
                CrearRenglon(20, 2, costo: 50m, stock: 1, minimo: 3),
                CrearRenglon(11, 1, costo: 2.005m, enBorrador: 4)
            },
            Omitidos = omitidos
        };
    }

    [Fact]
    public void Constructor_VistaPrevia_AgrupaPorProveedorConElDestinoInformado()
    {
        var sut = new PedirArticulosViewModel(CrearVistaPrevia());

        sut.Grupos.Select(g => g.RazonSocialProveedor).Should().Equal("Alfa", "Zeta");
        sut.Grupos[0].Renglones.Select(r => r.IdCatalogoProveedor).Should().Equal(10, 11);
        sut.Grupos[0].TextoDestino.Should().Be("Se agregará al borrador #7 (2 renglones actualmente)");
        sut.Grupos[1].TextoDestino.Should().Be("Se creará un borrador nuevo");
        sut.CantidadRenglones.Should().Be(3);
    }

    [Fact]
    public void Constructor_Renglones_CantidadUnoPorDefectoYDatosDeStock()
    {
        var sut = new PedirArticulosViewModel(CrearVistaPrevia());
        var renglones = sut.Grupos.SelectMany(g => g.Renglones).ToList();

        renglones.Should().OnlyContain(r => r.Cantidad == 1);
        renglones.Single(r => r.IdCatalogoProveedor == 20).StockBajo.Should().BeTrue();
        renglones.Single(r => r.IdCatalogoProveedor == 11).TieneCantidadEnBorrador.Should().BeTrue();
    }

    [Fact]
    public void TotalEstimado_CambiaUnaCantidad_RecalculaSubtotalDelGrupoYTotalConRedondeoUnico()
    {
        var sut = new PedirArticulosViewModel(CrearVistaPrevia());
        var cambios = new List<string?>();
        sut.PropertyChanged += (_, e) => cambios.Add(e.PropertyName);

        sut.Grupos[0].Renglones[0].Cantidad = 2;

        // 2 × 1,005 + 1 × 2,005 = 4,015 → 4,02 (se redondea una vez, no renglón por renglón).
        sut.Grupos[0].Subtotal.Should().Be(4.02m);
        sut.TotalEstimado.Should().Be(54.02m);
        cambios.Should().Contain(nameof(PedirArticulosViewModel.TotalEstimado));
    }

    [Fact]
    public void PuedeConfirmar_UnaCantidadEnCero_NoPermiteConfirmar()
    {
        var sut = new PedirArticulosViewModel(CrearVistaPrevia());

        sut.Grupos[1].Renglones[0].Cantidad = 0;

        sut.PuedeConfirmar.Should().BeFalse();
    }

    [Fact]
    public void QuitarRenglon_UltimoDelGrupo_QuitaElGrupo()
    {
        var sut = new PedirArticulosViewModel(CrearVistaPrevia());

        sut.QuitarRenglonCommand.Execute(sut.Grupos[1].Renglones[0]);

        sut.Grupos.Should().ContainSingle().Which.RazonSocialProveedor.Should().Be("Alfa");
        sut.CantidadRenglones.Should().Be(2);
    }

    [Fact]
    public void QuitarRenglon_TodosLosRenglones_NoPermiteConfirmar()
    {
        var sut = new PedirArticulosViewModel(CrearVistaPrevia());

        foreach (var renglon in sut.Grupos.SelectMany(g => g.Renglones).ToList())
        {
            sut.QuitarRenglonCommand.Execute(renglon);
        }

        sut.Grupos.Should().BeEmpty();
        sut.PuedeConfirmar.Should().BeFalse();
    }

    [Fact]
    public void ObtenerRespuesta_ConCantidadesEditadas_ConfirmaElLoteEnElOrdenMostrado()
    {
        var sut = new PedirArticulosViewModel(CrearVistaPrevia());
        sut.Grupos[0].Renglones[1].Cantidad = 6;

        var respuesta = sut.ObtenerRespuesta();

        respuesta.Accion.Should().Be(AccionPedirArticulosEnum.Confirmar);
        respuesta.Renglones.Should().BeEquivalentTo(
            new[]
            {
                new RenglonAAgregarDto { IdCatalogoProveedor = 10, Cantidad = 1 },
                new RenglonAAgregarDto { IdCatalogoProveedor = 11, Cantidad = 6 },
                new RenglonAAgregarDto { IdCatalogoProveedor = 20, Cantidad = 1 }
            },
            o => o.WithStrictOrdering());
    }

    [Fact]
    public void Omitidos_InexistenteYSinIncorporar_DescribeCadaUnoConSuMotivo()
    {
        var sut = new PedirArticulosViewModel(CrearVistaPrevia(
            new RenglonOmitidoDto { IdCatalogoProveedor = 99, Motivo = MotivoOmisionPedidoEnum.ItemInexistente },
            new RenglonOmitidoDto
            {
                IdCatalogoProveedor = 30,
                CodigoProveedor = "COD-30",
                Descripcion = "Lápiz HB",
                Motivo = MotivoOmisionPedidoEnum.SinIncorporar
            }));

        sut.TieneOmitidos.Should().BeTrue();
        sut.Omitidos.Should().Equal(
            "Ítem 99: ya no existe en el catálogo",
            "COD-30 · Lápiz HB: no está incorporado a la tienda");
    }

    [Fact]
    public void Omitidos_ConAvisosPrevios_LosMuestraAntesQueLosDeLaVistaPrevia()
    {
        var sut = new PedirArticulosViewModel(
            CrearVistaPrevia(new RenglonOmitidoDto { IdCatalogoProveedor = 99, Motivo = MotivoOmisionPedidoEnum.ItemInexistente }),
            avisosPrevios: ["Señalador: no tiene proveedor vinculado"]);

        sut.Omitidos.Should().Equal(
            "Señalador: no tiene proveedor vinculado",
            "Ítem 99: ya no existe en el catálogo");
    }

    [Fact]
    public void PuedeIncorporarPrimero_SeOfreceYHaySinIncorporar_EsVerdadero()
    {
        var sut = new PedirArticulosViewModel(CrearVistaPrevia(CrearSinIncorporar(30), CrearSinIncorporar(31)), ofrecerIncorporar: true);

        sut.PuedeIncorporarPrimero.Should().BeTrue();
        sut.TextoIncorporarPrimero.Should().Be("Incorporar los 2 ítems y volver");
    }

    [Fact]
    public void PuedeIncorporarPrimero_NoSeOfrece_EsFalso()
    {
        var sut = new PedirArticulosViewModel(CrearVistaPrevia(CrearSinIncorporar(30)), ofrecerIncorporar: false);

        sut.PuedeIncorporarPrimero.Should().BeFalse();
    }

    [Fact]
    public void PuedeIncorporarPrimero_SoloOmitidosInexistentes_EsFalso()
    {
        var sut = new PedirArticulosViewModel(
            CrearVistaPrevia(new RenglonOmitidoDto { IdCatalogoProveedor = 99, Motivo = MotivoOmisionPedidoEnum.ItemInexistente }),
            ofrecerIncorporar: true);

        sut.PuedeIncorporarPrimero.Should().BeFalse();
    }

    [Fact]
    public void ObtenerRespuesta_SolicitoIncorporar_DevuelveIncorporarPrimeroSinRenglones()
    {
        var sut = new PedirArticulosViewModel(CrearVistaPrevia(CrearSinIncorporar(30)), ofrecerIncorporar: true);

        sut.SolicitarIncorporacion();
        var respuesta = sut.ObtenerRespuesta();

        respuesta.Accion.Should().Be(AccionPedirArticulosEnum.IncorporarPrimero);
        respuesta.Renglones.Should().BeEmpty();
    }

    [Fact]
    public void ObtenerRespuesta_SolicitoIncorporarSinOferta_IgnoraLaSolicitudYConfirma()
    {
        var sut = new PedirArticulosViewModel(CrearVistaPrevia(CrearSinIncorporar(30)), ofrecerIncorporar: false);

        sut.SolicitarIncorporacion();

        sut.ObtenerRespuesta().Accion.Should().Be(AccionPedirArticulosEnum.Confirmar);
    }

    [Fact]
    public void ObtenerRespuesta_SinRenglonesPedibles_DevuelveCancelada()
    {
        var soloOmitidos = new VistaPreviaPedidoDto { Omitidos = [CrearSinIncorporar(30)] };
        var sut = new PedirArticulosViewModel(soloOmitidos);

        sut.ObtenerRespuesta().Should().Be(RespuestaPedirArticulos.Cancelada);
    }

    private static RenglonOmitidoDto CrearSinIncorporar(int idCatalogo)
    {
        return new RenglonOmitidoDto
        {
            IdCatalogoProveedor = idCatalogo,
            CodigoProveedor = $"COD-{idCatalogo}",
            Descripcion = $"Ítem {idCatalogo}",
            Motivo = MotivoOmisionPedidoEnum.SinIncorporar
        };
    }
}
