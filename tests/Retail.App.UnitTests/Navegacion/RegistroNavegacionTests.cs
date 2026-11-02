using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Retail.App.Navegacion;
using Retail.App.Services;
using Retail.App.ViewModels;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.App.UnitTests.Navegacion;

/// <summary>
/// Mapa de navegación y política de acceso: qué ve cada rol, dónde aterriza y que los atajos no se pisen. Son pruebas
/// puras (sin interfaz): la tabla de rol por página es el contrato que el menú y los selectores van a obedecer.
/// </summary>
public class RegistroNavegacionTests
{
    private static IEnumerable<PaginaNavegacion> TodasLasPaginas => RegistroNavegacion.Secciones.SelectMany(s => s.Paginas);

    [Fact]
    public void Registro_IdsDeSeccionesYPaginas_SonUnicos()
    {
        RegistroNavegacion.Secciones.Select(s => s.Id).Should().OnlyHaveUniqueItems();
        TodasLasPaginas.Select(p => p.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Registro_Atajos_NoSeRepiten()
    {
        TodasLasPaginas.Where(p => p.Atajo is not null).Select(p => p.Atajo).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Registro_VistaCompartidaPorVariasPaginas_SiempreSeConfigura()
    {
        var compartidas = TodasLasPaginas.GroupBy(p => p.TipoVista).Where(g => g.Count() > 1).SelectMany(g => g);

        compartidas.Should().OnlyContain(
            p => p.Configurar != null,
            "dos páginas con la misma vista solo se distinguen por cómo se configura");
    }

    [Theory]
    [InlineData(RolUsuarioEnum.Gerente, "tablero,pos,clientes,caja,presupuestos,articulos,catalogos,pedidos,ingresos,proveedores,usuarios,copias,restauraciones,fiscal")]
    [InlineData(RolUsuarioEnum.Encargado, "pos,clientes,caja,presupuestos,articulos,catalogos,pedidos,ingresos,proveedores")]
    [InlineData(RolUsuarioEnum.Cajero, "pos,clientes,caja,presupuestos,articulos,catalogos,ingresos,proveedores")]
    [InlineData(RolUsuarioEnum.Administrador, "usuarios,copias,restauraciones")]
    public void PaginasVisibles_PorRol_ComoDefineLaPolitica(RolUsuarioEnum rol, string esperadas)
    {
        var visibles = RegistroNavegacion.SeccionesVisibles(rol)
            .SelectMany(s => RegistroNavegacion.PaginasVisibles(s, rol))
            .Select(p => p.Id);

        visibles.Should().Equal(esperadas.Split(','));
    }

    [Fact]
    public void PaginasVisibles_SinRol_NoHayNinguna()
    {
        RegistroNavegacion.SeccionesVisibles(null).Should().BeEmpty();
        RegistroNavegacion.PaginaInicial(null).Should().BeNull();
    }

    [Theory]
    [InlineData(RolUsuarioEnum.Gerente, "tablero,pos,ventas,inventario,compras,administracion")]
    [InlineData(RolUsuarioEnum.Encargado, "pos,ventas,inventario,compras")]
    [InlineData(RolUsuarioEnum.Cajero, "pos,ventas,inventario,compras")]
    [InlineData(RolUsuarioEnum.Administrador, "administracion")]
    public void SeccionesVisibles_PorRol_SonLasDelMenu(RolUsuarioEnum rol, string esperadas)
    {
        RegistroNavegacion.SeccionesVisibles(rol).Select(s => s.Id).Should().Equal(esperadas.Split(','));
    }

    [Theory]
    [InlineData(RolUsuarioEnum.Gerente, "tablero")]
    [InlineData(RolUsuarioEnum.Encargado, "pos")]
    [InlineData(RolUsuarioEnum.Cajero, "pos")]
    [InlineData(RolUsuarioEnum.Administrador, "usuarios")]
    public void PaginaInicial_PorRol_EsLaPrimeraDelMenu(RolUsuarioEnum rol, string esperada)
    {
        RegistroNavegacion.PaginaInicial(rol)!.Id.Should().Be(esperada);
    }

    [Theory]
    [InlineData("compras", RolUsuarioEnum.Cajero, 2)]
    [InlineData("compras", RolUsuarioEnum.Encargado, 3)]
    [InlineData("administracion", RolUsuarioEnum.Administrador, 3)]
    [InlineData("pos", RolUsuarioEnum.Cajero, 1)]
    public void PaginasVisibles_DeUnaSeccion_DecidenSiHaySelector(string seccionId, RolUsuarioEnum rol, int cantidad)
    {
        var seccion = RegistroNavegacion.BuscarSeccion(seccionId)!;

        RegistroNavegacion.PaginasVisibles(seccion, rol).Should().HaveCount(cantidad);
    }

    [Fact]
    public void BuscarPorAtajo_ConAtajoExistente_DevuelveSuPagina()
    {
        RegistroNavegacion.BuscarPorAtajo("F4")!.Id.Should().Be("clientes");
        RegistroNavegacion.BuscarPorAtajo("F11")!.Id.Should().Be("copias");
        RegistroNavegacion.BuscarPorAtajo("F12").Should().BeNull();
    }

    [Theory]
    [InlineData(RolUsuarioEnum.Gerente)]
    [InlineData(RolUsuarioEnum.Encargado)]
    [InlineData(RolUsuarioEnum.Cajero)]
    [InlineData(RolUsuarioEnum.Administrador)]
    public void PoliticaAcceso_CoincideConLasReglasActualesDelShell(RolUsuarioEnum rol)
    {
        // Mientras el MainViewModel conserve sus propios booleanos, la política nueva no puede divergir de ellos
        var sesion = Substitute.For<ICurrentUserSession>();
        sesion.Rol.Returns(rol);
        sesion.EsGerente.Returns(rol == RolUsuarioEnum.Gerente);
        sesion.EsEncargado.Returns(rol == RolUsuarioEnum.Encargado);
        sesion.EsCajero.Returns(rol == RolUsuarioEnum.Cajero);
        sesion.EsAdministrador.Returns(rol == RolUsuarioEnum.Administrador);
        var shell = new MainViewModel(sesion, Substitute.For<INavigationService>(), Substitute.For<IServiceScopeFactory>());

        PoliticaAcceso.Permite(AccesoPagina.Operadores, rol).Should().Be(shell.PuedeOperarNegocio);
        PoliticaAcceso.Permite(AccesoPagina.Abastecimiento, rol).Should().Be(shell.PuedeAbastecer);
        PoliticaAcceso.Permite(AccesoPagina.GestionUsuarios, rol).Should().Be(shell.PuedeGestionarUsuarios);
        PoliticaAcceso.Permite(AccesoPagina.Gerente, rol).Should().Be(shell.EsGerente);
    }

    [Fact]
    public void EstadoNavegacion_Registrar_RecuerdaLaUltimaPaginaDeLaSeccion()
    {
        var estado = new EstadoNavegacion();
        var administracion = RegistroNavegacion.BuscarSeccion("administracion")!;

        estado.Registrar("restauraciones");

        estado.PaginaActualId.Should().Be("restauraciones");
        estado.SeccionActualId.Should().Be("administracion");
        estado.PaginaDeEntrada(administracion, RolUsuarioEnum.Gerente)!.Id.Should().Be("restauraciones");
    }

    [Fact]
    public void EstadoNavegacion_SinHistorial_EntraPorLaPrimeraPaginaVisible()
    {
        var estado = new EstadoNavegacion();
        var compras = RegistroNavegacion.BuscarSeccion("compras")!;

        estado.PaginaDeEntrada(compras, RolUsuarioEnum.Encargado)!.Id.Should().Be("pedidos");
        estado.PaginaDeEntrada(compras, RolUsuarioEnum.Cajero)!.Id.Should().Be("ingresos");
    }

    [Fact]
    public void EstadoNavegacion_UltimaPaginaYaNoVisibleParaElRol_CaeALaPrimeraVisible()
    {
        var estado = new EstadoNavegacion();
        var administracion = RegistroNavegacion.BuscarSeccion("administracion")!;
        estado.Registrar("fiscal");

        estado.PaginaDeEntrada(administracion, RolUsuarioEnum.Administrador)!.Id.Should().Be("usuarios");
    }

    [Fact]
    public void EstadoNavegacion_SeccionSinPaginasVisibles_NoTieneEntrada()
    {
        var estado = new EstadoNavegacion();

        estado.PaginaDeEntrada(RegistroNavegacion.BuscarSeccion("tablero")!, RolUsuarioEnum.Cajero).Should().BeNull();
    }

    [Fact]
    public void EstadoNavegacion_PaginaInexistente_Falla()
    {
        var estado = new EstadoNavegacion();

        var registrar = () => estado.Registrar("no-existe");

        registrar.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EstadoNavegacion_Reiniciar_OlvidaTodoYAvisa()
    {
        var estado = new EstadoNavegacion();
        var avisos = 0;
        estado.Cambio += () => avisos++;
        estado.Registrar("caja");

        estado.Reiniciar();

        estado.PaginaActualId.Should().BeNull();
        estado.SeccionActualId.Should().BeNull();
        estado.PaginaDeEntrada(RegistroNavegacion.BuscarSeccion("ventas")!, RolUsuarioEnum.Cajero)!.Id.Should().Be("clientes");
        avisos.Should().Be(2);
    }
}
