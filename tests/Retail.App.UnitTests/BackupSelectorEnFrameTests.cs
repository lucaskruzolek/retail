using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FluentAssertions;
using NSubstitute;
using Retail.App.Services;
using Retail.App.UnitTests.Helpers;
using Retail.App.ViewModels.Backups;
using Retail.App.Views.Components;
using Retail.App.Views.Pages;
using Retail.Application.Interfaces.Services;
using Xunit;

namespace Retail.App.UnitTests;

/// <summary>
/// El selector Copias / Restauraciones de Respaldos debe mostrar su indicador al entrar a la pantalla, también cuando la
/// página se aloja en un <see cref="Frame"/> como en el shell, y al cambiar de pestaña.
/// </summary>
public class BackupSelectorEnFrameTests
{
    [Fact]
    public void BackupView_EnUnFrame_ElIndicadorSeMuestraAlEntrarYAlCambiarDePestana()
    {
        bool visibleAlEntrar = false;
        bool visibleAlCambiar = false;

        WpfTestHelper.Run(() =>
        {
            System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var sesion = Substitute.For<ICurrentUserSession>();
            sesion.EsGerente.Returns(true);
            var viewModel = new BackupViewModel(
                Substitute.For<IBackupService>(),
                Substitute.For<IRestauracionService>(),
                Substitute.For<IBackupDialogService>(),
                sesion,
                Substitute.For<IReinicioAplicacion>());

            var marco = new Frame { NavigationUIVisibility = System.Windows.Navigation.NavigationUIVisibility.Hidden };
            var ventana = new Window { Content = marco, Left = -10000, Top = -10000, Width = 1366, Height = 768, ShowActivated = false };
            ventana.Show();

            marco.Content = new BackupView(viewModel);
            Vaciar();

            var selector = Buscar<SelectorSegmentado>(marco)!;
            var indicador = (FrameworkElement)selector.Template.FindName("PART_Indicador", selector);
            visibleAlEntrar = indicador.Visibility == Visibility.Visible && indicador.Width > 0;

            viewModel.MostrandoRestauraciones = true;
            Vaciar();
            visibleAlCambiar = indicador.Visibility == Visibility.Visible && indicador.Width > 0;

            ventana.Close();
        });

        visibleAlEntrar.Should().BeTrue("la pestaña Copias debe verse seleccionada al entrar");
        visibleAlCambiar.Should().BeTrue("la pestaña Restauraciones debe verse seleccionada al cambiar");
    }

    private static void Vaciar()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static T? Buscar<T>(DependencyObject raiz)
        where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(raiz); i++)
        {
            var hijo = System.Windows.Media.VisualTreeHelper.GetChild(raiz, i);
            if (hijo is T encontrado)
            {
                return encontrado;
            }

            var interno = Buscar<T>(hijo);
            if (interno is not null)
            {
                return interno;
            }
        }

        return null;
    }
}
