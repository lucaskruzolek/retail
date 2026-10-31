using FluentAssertions;
using Retail.App.ViewModels.Comun;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class FiltroColumnaViewModelTests
{
    // Dos rubros con dos categorías cada uno, como el filtro de la columna Categoría
    private static FiltroColumnaViewModel CrearFiltroAgrupado()
    {
        var filtro = new FiltroColumnaViewModel("Categoría");
        filtro.CargarOpciones(
        [
            new OpcionFiltroColumna(1, "Cuadernos", "Escolar"),
            new OpcionFiltroColumna(2, "Lápices", "Escolar"),
            new OpcionFiltroColumna(3, "Carpetas", "Oficina"),
            new OpcionFiltroColumna(4, "Resmas", "Oficina")
        ]);
        return filtro;
    }

    private static OpcionFiltroColumna Opcion(FiltroColumnaViewModel filtro, int id)
    {
        return filtro.Grupos.SelectMany(g => g.Opciones).Single(o => o.Id == id);
    }

    [Fact]
    public void CargarOpciones_ConGrupos_AgrupaEnElOrdenEnQueLlegan()
    {
        var filtro = CrearFiltroAgrupado();

        filtro.Grupos.Select(g => g.Nombre).Should().Equal("Escolar", "Oficina");
        filtro.Grupos[0].Opciones.Select(o => o.Nombre).Should().Equal("Cuadernos", "Lápices");
        filtro.TieneOpciones.Should().BeTrue();
        filtro.HayFiltroActivo.Should().BeFalse();
    }

    [Fact]
    public void Aplicar_ConCasillasMarcadas_GuardaLosIdsAvisaYCierra()
    {
        var filtro = CrearFiltroAgrupado();
        int avisos = 0;
        filtro.Aplicado += (_, _) => avisos++;
        filtro.EstaAbierto = true;
        Opcion(filtro, 2).Seleccionada = true;
        Opcion(filtro, 3).Seleccionada = true;

        filtro.AplicarCommand.Execute(null);

        filtro.IdsAplicados.Should().Equal(2, 3);
        filtro.HayFiltroActivo.Should().BeTrue();
        filtro.EstaAbierto.Should().BeFalse();
        avisos.Should().Be(1);
    }

    [Fact]
    public void Aplicar_SinCambiosRespectoDeLoAplicado_NoAvisa()
    {
        var filtro = CrearFiltroAgrupado();
        int avisos = 0;
        filtro.Aplicado += (_, _) => avisos++;

        filtro.EstaAbierto = true;
        filtro.AplicarCommand.Execute(null);

        avisos.Should().Be(0, "no marcar nada sobre un filtro vacío no cambia la consulta");
    }

    [Fact]
    public void Aplicar_ConTodasMarcadas_EquivaleASinFiltro()
    {
        var filtro = CrearFiltroAgrupado();
        filtro.EstaAbierto = true;
        filtro.SeleccionarTodoCommand.Execute(null);

        filtro.AplicarCommand.Execute(null);

        filtro.IdsAplicados.Should().BeEmpty();
        filtro.HayFiltroActivo.Should().BeFalse();
    }

    [Fact]
    public void Cancelar_DespuesDeMarcar_DescartaElBorradorAlVolverAAbrir()
    {
        var filtro = CrearFiltroAgrupado();
        filtro.EstaAbierto = true;
        Opcion(filtro, 1).Seleccionada = true;
        filtro.AplicarCommand.Execute(null);

        filtro.EstaAbierto = true;
        Opcion(filtro, 1).Seleccionada = false;
        Opcion(filtro, 4).Seleccionada = true;
        filtro.CancelarCommand.Execute(null);
        filtro.EstaAbierto = true;

        filtro.IdsAplicados.Should().Equal(1);
        Opcion(filtro, 1).Seleccionada.Should().BeTrue("el borrador vuelve a partir de lo aplicado");
        Opcion(filtro, 4).Seleccionada.Should().BeFalse();
    }

    [Fact]
    public void TextoBusqueda_SinTildesNiMayusculas_MuestraSoloLasCoincidenciasYSusGrupos()
    {
        var filtro = CrearFiltroAgrupado();
        filtro.EstaAbierto = true;

        filtro.TextoBusqueda = "LAPI";

        filtro.Grupos.SelectMany(g => g.Opciones).Where(o => o.Visible).Select(o => o.Nombre).Should().Equal("Lápices");
        filtro.Grupos.Select(g => g.Visible).Should().Equal(true, false);
        filtro.HayCoincidencias.Should().BeTrue();
    }

    [Fact]
    public void SeleccionarTodo_ConBusqueda_MarcaSoloLasVisibles()
    {
        var filtro = CrearFiltroAgrupado();
        filtro.EstaAbierto = true;
        filtro.TextoBusqueda = "c";

        filtro.SeleccionarTodoCommand.Execute(null);

        filtro.Grupos.SelectMany(g => g.Opciones).Where(o => o.Seleccionada).Select(o => o.Nombre)
            .Should().BeEquivalentTo("Cuadernos", "Lápices", "Carpetas");
    }

    [Fact]
    public void TodasSeleccionadas_MarcarElGrupo_MarcaSusOpcionesYSuEstadoSigueALasOpciones()
    {
        var filtro = CrearFiltroAgrupado();
        filtro.EstaAbierto = true;
        var escolar = filtro.Grupos[0];

        escolar.TodasSeleccionadas = true;

        Opcion(filtro, 1).Seleccionada.Should().BeTrue();
        Opcion(filtro, 2).Seleccionada.Should().BeTrue();
        Opcion(filtro, 3).Seleccionada.Should().BeFalse("el grupo solo marca sus propias opciones");

        Opcion(filtro, 2).Seleccionada = false;
        escolar.TodasSeleccionadas.Should().BeNull("con una marcada y otra no, el grupo queda indeterminado");

        escolar.TodasSeleccionadas = false;
        Opcion(filtro, 1).Seleccionada.Should().BeFalse();
    }

    [Fact]
    public void Limpiar_ConCasillasMarcadas_DesmarcaElBorradorSinTocarLoAplicado()
    {
        var filtro = CrearFiltroAgrupado();
        filtro.EstaAbierto = true;
        Opcion(filtro, 3).Seleccionada = true;
        filtro.AplicarCommand.Execute(null);
        filtro.EstaAbierto = true;

        filtro.LimpiarCommand.Execute(null);

        Opcion(filtro, 3).Seleccionada.Should().BeFalse();
        filtro.IdsAplicados.Should().ContainSingle().Which.Should().Be(3, "hace falta Aplicar para quitar el filtro");
    }

    [Fact]
    public void QuitarFiltro_ConFiltroAplicado_LoVaciaSinAvisar()
    {
        var filtro = CrearFiltroAgrupado();
        filtro.EstaAbierto = true;
        Opcion(filtro, 3).Seleccionada = true;
        filtro.AplicarCommand.Execute(null);
        int avisos = 0;
        filtro.Aplicado += (_, _) => avisos++;

        filtro.QuitarFiltro();

        filtro.HayFiltroActivo.Should().BeFalse();
        avisos.Should().Be(0, "la pantalla que limpia todos los filtros consulta una sola vez");
    }

    [Fact]
    public void CargarOpciones_DeNuevo_ConservaLoAplicadoQueSigueExistiendo()
    {
        var filtro = CrearFiltroAgrupado();
        filtro.EstaAbierto = true;
        Opcion(filtro, 1).Seleccionada = true;
        Opcion(filtro, 4).Seleccionada = true;
        filtro.AplicarCommand.Execute(null);
        int avisos = 0;
        filtro.Aplicado += (_, _) => avisos++;

        // La categoría 4 se dio de baja en el diálogo de clasificación
        filtro.CargarOpciones(
        [
            new OpcionFiltroColumna(1, "Cuadernos", "Escolar"),
            new OpcionFiltroColumna(2, "Lápices", "Escolar")
        ]);

        filtro.IdsAplicados.Should().Equal(1);
        Opcion(filtro, 1).Seleccionada.Should().BeTrue();
        avisos.Should().Be(1, "lo aplicado cambió: la pantalla tiene que volver a consultar sin la categoría eliminada");
    }

    [Fact]
    public void MostrarBusqueda_ConPocasOpciones_EsFalso()
    {
        var filtro = new FiltroColumnaViewModel("Stock");
        filtro.CargarOpciones(Opciones(3));

        filtro.MostrarBusqueda.Should().BeFalse();
    }

    [Fact]
    public void MostrarBusqueda_ConSeisOpciones_EsFalso()
    {
        var filtro = new FiltroColumnaViewModel("Stock");
        filtro.CargarOpciones(Opciones(6));

        filtro.MostrarBusqueda.Should().BeFalse();
    }

    [Fact]
    public void MostrarBusqueda_ConSieteOpciones_EsVerdadero()
    {
        var filtro = new FiltroColumnaViewModel("Marca");
        filtro.CargarOpciones(Opciones(7));

        filtro.MostrarBusqueda.Should().BeTrue();
    }

    [Fact]
    public void MostrarBusqueda_ConGrupos_SumaLasOpcionesDeTodosLosGrupos()
    {
        var filtro = new FiltroColumnaViewModel("Categoría");
        filtro.CargarOpciones(
        [
            .. Enumerable.Range(1, 4).Select(i => new OpcionFiltroColumna(i, "A" + i, "Escolar")),
            .. Enumerable.Range(5, 3).Select(i => new OpcionFiltroColumna(i, "B" + i, "Oficina"))
        ]);

        filtro.MostrarBusqueda.Should().BeTrue("4 + 3 opciones superan el máximo aunque cada grupo sea chico");
    }

    [Fact]
    public void CargarOpciones_CambiandoLaCantidad_NotificaMostrarBusqueda()
    {
        var filtro = new FiltroColumnaViewModel("Marca");
        filtro.CargarOpciones(Opciones(3));
        var notificadas = new List<string?>();
        filtro.PropertyChanged += (_, e) => notificadas.Add(e.PropertyName);

        filtro.CargarOpciones(Opciones(8));

        filtro.MostrarBusqueda.Should().BeTrue();
        notificadas.Should().Contain(nameof(FiltroColumnaViewModel.MostrarBusqueda));
    }

    private static List<OpcionFiltroColumna> Opciones(int cantidad)
    {
        return Enumerable.Range(1, cantidad).Select(i => new OpcionFiltroColumna(i, "Opción " + i)).ToList();
    }

    [Fact]
    public void CargarOpciones_DeNuevoSinPerderNadaAplicado_NoAvisa()
    {
        var filtro = CrearFiltroAgrupado();
        filtro.EstaAbierto = true;
        Opcion(filtro, 1).Seleccionada = true;
        filtro.AplicarCommand.Execute(null);
        int avisos = 0;
        filtro.Aplicado += (_, _) => avisos++;

        filtro.CargarOpciones(
        [
            new OpcionFiltroColumna(1, "Cuadernos", "Escolar"),
            new OpcionFiltroColumna(5, "Mochilas", "Escolar")
        ]);

        avisos.Should().Be(0);
    }
}
