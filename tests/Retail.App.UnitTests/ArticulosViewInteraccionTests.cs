using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using FluentAssertions;
using NSubstitute;
using Retail.App.Services;
using Retail.App.UnitTests.Helpers;
using Retail.App.ViewModels.Articulos;
using Retail.App.Views.Pages;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Interfaces.Services;
using Xunit;

namespace Retail.App.UnitTests;

/// <summary>
/// Interacción real con la grilla de Artículos: el doble clic en una fila abre la edición de ESA fila y el botón "+n"
/// de fuentes adicionales abre la edición de su artículo (Track 4.3, A-19). Las pruebas de ViewModel no ven el
/// <c>EventSetter</c> ni los bindings de la plantilla, por eso se monta la vista y se levantan los eventos.
/// </summary>
public class ArticulosViewInteraccionTests
{
    private sealed class Escenario
    {
        public required IArticuloDialogService Dialogos { get; init; }

        public required ArticulosView Vista { get; init; }

        public required ArticuloDto Primero { get; init; }

        public required ArticuloDto Segundo { get; init; }
    }

    private static Escenario Crear()
    {
        var inventario = Substitute.For<IInventarioService>();
        inventario.ListarCategoriasAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CategoriaDto>>([new() { IdCategoria = 1, NombreCategoria = "Escolar" }]));
        inventario.ListarMarcasAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<MarcaDto>>([new() { IdMarca = 1, NombreMarca = "Bic" }]));
        inventario.ObtenerFuentesDeArticuloAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FuenteArticuloDto>>([]));
        var dialogos = Substitute.For<IArticuloDialogService>();
        var viewModel = new ArticulosViewModel(inventario, dialogos);

        var primero = new ArticuloDto { IdArticulo = 1, Descripcion = "Cuaderno A4", CostoReposicion = 100m, PorcentajeGanancia = 50m, PrecioVenta = 150m, StockActual = 5, StockMinimo = 1, EsServicio = false };
        var segundo = new ArticuloDto
        {
            IdArticulo = 2,
            Descripcion = "Lapicera Azul",
            CostoReposicion = 10m,
            PorcentajeGanancia = 50m,
            PrecioVenta = 15m,
            StockActual = 5,
            StockMinimo = 1,
            EsServicio = false,
            IdCatalogoProveedor = 40,
            ProveedorNombre = "Distribuidora Sur",
            CantidadFuentes = 3
        };
        viewModel.Articulos.Add(new ArticuloFila(primero));
        viewModel.Articulos.Add(new ArticuloFila(segundo));

        var vista = new ArticulosView(viewModel);
        vista.Measure(new Size(1280, 800));
        vista.Arrange(new Rect(0, 0, 1280, 800));
        vista.UpdateLayout();
        return new Escenario { Dialogos = dialogos, Vista = vista, Primero = primero, Segundo = segundo };
    }

    /// <summary>
    /// MouseDoubleClick es un evento directo: WPF lo levanta sobre la fila (el control) con el elemento pulsado como
    /// origen. Se reproduce igual: se levanta en la fila y <paramref name="origen"/> queda como OriginalSource.
    /// </summary>
    private static void DobleClic(UIElement fila, UIElement origen)
    {
        fila.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
        {
            RoutedEvent = Control.MouseDoubleClickEvent,
            Source = origen
        });
    }

    private static int LlamadasAlDialogo(Escenario e, ArticuloDto articulo)
    {
        return e.Dialogos.ReceivedCalls()
            .Count(c => c.GetMethodInfo().Name == nameof(IArticuloDialogService.MostrarDialogoModificar)
                && ReferenceEquals(c.GetArguments()[0], articulo));
    }

    [Fact]
    public void DobleClic_SobreUnaCeldaDeTextoDeLaSegundaFila_AbreLaEdicionDeEsaFila()
    {
        int sobreSegundo = 0;
        int sobrePrimero = 0;

        WpfTestHelper.Run(() =>
        {
            var e = Crear();
            var segundaFila = Descendientes<DataGridRow>(e.Vista).Single(f => ReferenceEquals((f.Item as ArticuloFila)?.Articulo, e.Segundo));
            var texto = Descendientes<TextBlock>(segundaFila).First(t => t.Text == "Lapicera Azul");

            DobleClic(segundaFila, texto);

            sobreSegundo = LlamadasAlDialogo(e, e.Segundo);
            sobrePrimero = LlamadasAlDialogo(e, e.Primero);
        });

        sobreSegundo.Should().Be(1, "el doble clic abre el artículo de la fila pulsada");
        sobrePrimero.Should().Be(0, "no el artículo seleccionado antes ni otra fila");
    }

    [Fact]
    public void DobleClic_SobreUnBotonDeLaFila_NoAbreLaEdicion()
    {
        int llamadas = 0;

        WpfTestHelper.Run(() =>
        {
            var e = Crear();
            var segundaFila = Descendientes<DataGridRow>(e.Vista).Single(f => ReferenceEquals((f.Item as ArticuloFila)?.Articulo, e.Segundo));
            var boton = Descendientes<ButtonBase>(segundaFila).First();

            DobleClic(segundaFila, boton);

            llamadas = LlamadasAlDialogo(e, e.Segundo) + LlamadasAlDialogo(e, e.Primero);
        });

        llamadas.Should().Be(0, "un botón de la fila hace lo suyo y el doble clic no abre la edición");
    }

    [Fact]
    public void BotonFuentesAdicionales_ArticuloConAdicionales_ExisteYAbreLaEdicionDeSuArticulo()
    {
        string? texto = null;
        bool primeroTieneBoton = true;
        int llamadas = 0;

        WpfTestHelper.Run(() =>
        {
            var e = Crear();
            var filas = Descendientes<DataGridRow>(e.Vista).ToList();
            var botonesSegundo = Descendientes<Button>(filas.Single(f => ReferenceEquals((f.Item as ArticuloFila)?.Articulo, e.Segundo)))
                .Where(b => b.Content is string && b.Visibility == Visibility.Visible)
                .ToList();
            var botonesPrimero = Descendientes<Button>(filas.Single(f => ReferenceEquals((f.Item as ArticuloFila)?.Articulo, e.Primero)))
                .Where(b => b.Content is string && b.Visibility == Visibility.Visible)
                .ToList();
            primeroTieneBoton = botonesPrimero.Count > 0;
            var boton = botonesSegundo.Single();
            texto = (string)boton.Content;

            // Act: la ejecución de un clic real (ButtonBase.OnClick) sobre el comando enlazado
            boton.Command.Execute(boton.CommandParameter);

            llamadas = LlamadasAlDialogo(e, e.Segundo);
        });

        texto.Should().Be("+2");
        primeroTieneBoton.Should().BeFalse("sin fuentes adicionales no hay botón");
        llamadas.Should().Be(1);
    }

    private static IEnumerable<T> Descendientes<T>(DependencyObject raiz) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
        {
            var hijo = VisualTreeHelper.GetChild(raiz, i);
            if (hijo is T encontrado)
            {
                yield return encontrado;
            }

            foreach (var nieto in Descendientes<T>(hijo))
            {
                yield return nieto;
            }
        }
    }
}
