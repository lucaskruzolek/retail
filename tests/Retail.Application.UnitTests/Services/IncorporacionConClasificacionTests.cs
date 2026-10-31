using System.Linq.Expressions;
using FluentAssertions;
using NSubstitute;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Services;
using Retail.Application.Validators.Proveedores;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.Application.UnitTests.Services;

/// <summary>
/// Incorporación a la tienda con la clasificación por ítem (Track 6.6, R-7): el servidor deriva el origen comparando lo
/// confirmado con lo sugerido (M-34).
/// </summary>
public class IncorporacionConClasificacionTests
{
    private readonly ICatalogoProveedorQueryService _catalogo = Substitute.For<ICatalogoProveedorQueryService>();
    private readonly IRepository<Articulo> _articulos = Substitute.For<IRepository<Articulo>>();
    private readonly ProveedorService _servicio;
    private readonly List<Articulo> _agregados = new();

    public IncorporacionConClasificacionTests()
    {
        _catalogo.ObtenerPorIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(llamada => llamada.Arg<IEnumerable<int>>()
                .Select(id => new CatalogoProveedor
                {
                    Id = id,
                    IdProveedor = 1,
                    CodigoProveedor = $"P-{id}",
                    DescripcionProveedor = $"Producto {id}",
                    CostoReposicion = 100m
                })
                .ToList());
        _articulos.FindAsync(Arg.Any<Expression<Func<Articulo, bool>>>(), false, Arg.Any<CancellationToken>())
            .Returns(new List<Articulo>());
        _articulos.AddAsync(Arg.Do<Articulo>(_agregados.Add), Arg.Any<CancellationToken>());

        _servicio = new ProveedorService(
            Substitute.For<IRepository<Proveedor>>(),
            _catalogo,
            _articulos,
            Substitute.For<IUnitOfWork>(),
            Substitute.For<IExcelCatalogParser>(),
            new CrearProveedorValidator(),
            new ActualizarProveedorValidator(),
            new MapeoColumnasValidator(),
            new IncorporarCatalogoArticulosValidator(),
            new ConsultaCatalogoProveedorValidator());
    }

    [Theory]
    [InlineData(null, OrigenClasificacionEnum.Manual)]
    [InlineData(3, OrigenClasificacionEnum.SugerenciaAceptada)]
    [InlineData(9, OrigenClasificacionEnum.SugerenciaCorregida)]
    public async Task Incorporar_CategoriaConfirmada_DerivaElOrigenDeLaSugerencia(int? sugerida, OrigenClasificacionEnum esperado)
    {
        await IncorporarAsync(new ItemIncorporacionArticuloDto
        {
            IdCatalogo = 1,
            PorcentajeGanancia = 40m,
            IdCategoria = 3,
            IdCategoriaSugerida = sugerida
        });

        var articulo = _agregados.Single();
        articulo.IdCategoria.Should().Be(3);
        articulo.OrigenCategoria.Should().Be(esperado);
    }

    [Fact]
    public async Task Incorporar_SinCategoriaPeseAUnaSugerencia_QuedaSinClasificar()
    {
        await IncorporarAsync(new ItemIncorporacionArticuloDto { IdCatalogo = 1, PorcentajeGanancia = 40m, IdCategoriaSugerida = 3 });

        var articulo = _agregados.Single();
        articulo.IdCategoria.Should().BeNull();
        articulo.OrigenCategoria.Should().BeNull();
    }

    [Theory]
    [InlineData(null, OrigenClasificacionEnum.Manual)]
    [InlineData(7, OrigenClasificacionEnum.SugerenciaCorregida)]
    public async Task Incorporar_SinMarcaConfirmada_RegistraElOrigenConMarcaNula(int? sugerida, OrigenClasificacionEnum esperado)
    {
        await IncorporarAsync(new ItemIncorporacionArticuloDto
        {
            IdCatalogo = 1,
            PorcentajeGanancia = 40m,
            SinMarcaConfirmada = true,
            IdMarcaSugerida = sugerida
        });

        var articulo = _agregados.Single();
        articulo.IdMarca.Should().BeNull();
        articulo.OrigenMarca.Should().Be(esperado);
    }

    [Fact]
    public async Task Incorporar_MarcaAceptadaYOtraSinClasificar_CadaItemConSuOrigen()
    {
        await IncorporarAsync(
            new ItemIncorporacionArticuloDto { IdCatalogo = 1, PorcentajeGanancia = 40m, IdMarca = 7, IdMarcaSugerida = 7 },
            new ItemIncorporacionArticuloDto { IdCatalogo = 2, PorcentajeGanancia = 40m, IdMarcaSugerida = 7 });

        var aceptada = _agregados.Single(a => a.IdCatalogoProveedor == 1);
        aceptada.IdMarca.Should().Be(7);
        aceptada.OrigenMarca.Should().Be(OrigenClasificacionEnum.SugerenciaAceptada);

        var sinClasificar = _agregados.Single(a => a.IdCatalogoProveedor == 2);
        sinClasificar.IdMarca.Should().BeNull();
        sinClasificar.OrigenMarca.Should().BeNull();
    }

    private Task<ResultadoIncorporacionDto> IncorporarAsync(params ItemIncorporacionArticuloDto[] items)
    {
        return _servicio.IncorporarArticulosATiendaAsync(new IncorporarCatalogoArticulosDto { Items = items });
    }
}
