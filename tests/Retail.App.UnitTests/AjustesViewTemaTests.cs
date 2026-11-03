using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FluentAssertions;
using NSubstitute;
using Retail.App.Services;
using Retail.App.UnitTests.Helpers;
using Retail.App.ViewModels.Ajustes;
using Retail.App.Views.Pages;
using Xunit;

namespace Retail.App.UnitTests;

/// <summary>
/// Entrar a Ajustes con el tema oscuro vigente no debe cambiar de tema: la vista solo muestra la preferencia, y únicamente un
/// clic del operador en "Claro" u "Oscuro" la modifica.
/// </summary>
public class AjustesViewTemaTests
{
    private static void Vaciar()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static (IServicioTema Tema, IPreferenciasUsuario Preferencias, AjustesView Vista) CrearVista(TemaVisual vigente)
    {
        var tema = Substitute.For<IServicioTema>();
        var preferencias = Substitute.For<IPreferenciasUsuario>();
        preferencias.Tema = vigente;

        return (tema, preferencias, new AjustesView(new AjustesViewModel(tema, preferencias)));
    }

    [Fact]
    public void AjustesView_AlEntrarConTemaOscuro_NoReaplicaNingunTema()
    {
        IServicioTema? tema = null;
        IPreferenciasUsuario? preferencias = null;

        WpfTestHelper.Run(() =>
        {
            System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var creada = CrearVista(TemaVisual.Oscuro);
            tema = creada.Tema;
            preferencias = creada.Preferencias;

            var marco = new Frame { NavigationUIVisibility = System.Windows.Navigation.NavigationUIVisibility.Hidden };
            var ventana = new Window { Content = marco, Left = -10000, Top = -10000, Width = 900, Height = 600, ShowActivated = false };
            ventana.Show();
            marco.Content = creada.Vista;
            Vaciar();
            ventana.Close();
        });

        tema!.DidNotReceive().Aplicar(Arg.Any<TemaVisual>());
        preferencias!.DidNotReceive().Guardar();
    }

    [Fact]
    public void AjustesView_AlVolverAEntrarConTemaOscuro_LaVistaAnteriorNoCambiaElTema()
    {
        // El shell crea una vista nueva cada vez que se navega a Ajustes; la anterior queda a la espera del recolector
        IServicioTema? temaPrimera = null;
        IPreferenciasUsuario? preferenciasPrimera = null;
        IServicioTema? temaSegunda = null;

        WpfTestHelper.Run(() =>
        {
            System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var primera = CrearVista(TemaVisual.Oscuro);
            var segunda = CrearVista(TemaVisual.Oscuro);
            temaPrimera = primera.Tema;
            preferenciasPrimera = primera.Preferencias;
            temaSegunda = segunda.Tema;

            var marco = new Frame { NavigationUIVisibility = System.Windows.Navigation.NavigationUIVisibility.Hidden };
            var ventana = new Window { Content = marco, Left = -10000, Top = -10000, Width = 900, Height = 600, ShowActivated = false };
            ventana.Show();

            marco.Content = primera.Vista;
            Vaciar();
            marco.Content = new Grid();
            Vaciar();
            marco.Content = segunda.Vista;
            Vaciar();

            GC.KeepAlive(primera.Vista);
            ventana.Close();
        });

        temaPrimera!.DidNotReceive().Aplicar(Arg.Any<TemaVisual>());
        preferenciasPrimera!.DidNotReceive().Guardar();
        temaSegunda!.DidNotReceive().Aplicar(Arg.Any<TemaVisual>());
    }
}
