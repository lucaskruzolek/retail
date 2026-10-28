using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Retail.App.Services;
using Retail.App.UnitTests.Helpers;
using Retail.App.ViewModels.Articulos;
using Retail.App.ViewModels.Proveedores;
using Retail.App.Views.Dialogs;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;
using Xunit;

namespace Retail.App.UnitTests;

/// <summary>
/// Un control editable dentro de una celda de <see cref="DataGrid"/> queda en el BindingGroup de la fila: sin
/// <c>UpdateSourceTrigger</c> explícito, el valor no llega al ViewModel hasta que la fila confirma la edición, cosa que
/// nunca pasa en una grilla de solo lectura. Las pruebas de ViewModel no lo detectan porque asignan las propiedades
/// sin pasar por el binding: estas eligen la opción en el combo de la celda, como lo hace el usuario.
/// </summary>
public class EdicionEnCeldasDeGrillaTests
{
    [Fact]
    public void ComboCategoria_IncorporarArticulosModal_ElegirOtraOpcion_LlegaAlItem()
    {
        // Arrange
        object? elegida = null;
        object? enElItem = null;

        WpfTestHelper.Run(() =>
        {
            var viewModel = new IncorporarArticulosModalViewModel(
                Substitute.For<IClasificacionService>(),
                Substitute.For<ITerminoClasificacionService>(),
                Substitute.For<IEquivalenciaClasificacionService>(),
                Substitute.For<IRubroService>(),
                Substitute.For<ICurrentUserSession>(),
                Substitute.For<ILogger<IncorporarArticulosModalViewModel>>());
            viewModel.Inicializar(
                new[]
                {
                    new CategoriaDto { IdCategoria = 1, NombreCategoria = "Cuadernos" },
                    new CategoriaDto { IdCategoria = 2, NombreCategoria = "Escritura" }
                },
                new[] { new MarcaDto { IdMarca = 1, NombreMarca = "Bic" } },
                new[]
                {
                    new CatalogoProveedorDto
                    {
                        Id = 1,
                        IdProveedor = 3,
                        CodigoProveedor = "BIC-01",
                        DescripcionProveedor = "Bolígrafo Azul",
                        CostoReposicion = 1000m
                    }
                });
            var dialog = new IncorporarArticulosModalDialog { DataContext = viewModel };
            var raiz = Montar(dialog, 1040, 680);
            var combo = BuscarCombo(raiz, "GrillaConSugerencias", viewModel.OpcionesCategoria);
            elegida = viewModel.OpcionesCategoria.Single(o => o.Id == 2);

            // Act
            combo.SelectedItem = elegida;
            enElItem = viewModel.Items[0].CategoriaSeleccionada;
        });

        // Assert
        enElItem.Should().Be(elegida, "la categoría elegida en la celda debe llegar al ViewModel de la fila");
    }

    [Fact]
    public void ComboCategoria_ReglasPropuestas_ElegirOtraOpcion_LlegaALaPropuesta()
    {
        // Arrange
        object? elegida = null;
        object? enLaPropuesta = null;

        WpfTestHelper.Run(() =>
        {
            var terminoService = Substitute.For<ITerminoClasificacionService>();
            terminoService.ListarAsync(Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<TerminoDto>>([]));
            terminoService.ListarParesAsync(Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<ParDto>>([]));
            terminoService.ListarParesPropuestosAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<ParPropuestoDto>>([]));
            var categoriaService = Substitute.For<ICategoriaService>();
            categoriaService.ListarAsync(Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<CategoriaDto>>([]));
            var viewModel = new AdministrarClasificacionViewModel(
                Substitute.For<IRubroService>(),
                categoriaService,
                Substitute.For<IMarcaService>(),
                terminoService,
                Substitute.For<IClasificacionDialogService>(),
                Substitute.For<ICurrentUserSession>());
            var dialog = new AdministrarClasificacionDialog { DataContext = viewModel };

            // Primero la sección, el término ambiguo y la pestaña (sus cargas vacían las listas) y después la fila a
            // editar (M-71: los pares propuestos están dentro del término).
            var ambiguo = new TerminoDto { IdTermino = 1, Termino = "CUADERNO" };
            viewModel.MostrandoTerminos = true;
            viewModel.Terminos.Terminos.Add(ambiguo);
            viewModel.Terminos.TerminoSeleccionado = ambiguo;
            viewModel.Terminos.MostrandoPropuestos = true;

            var propuesta = new CategoriaDto { IdCategoria = 10, NombreCategoria = "Cuadernos", IdRubro = 1 };
            var otra = new CategoriaDto { IdCategoria = 11, NombreCategoria = "Carpetas", IdRubro = 1 };
            viewModel.Terminos.Propuestos.Categorias.Add(propuesta);
            viewModel.Terminos.Propuestos.Categorias.Add(otra);
            viewModel.Terminos.Propuestos.Propuestas.Add(new ParPropuestoItemViewModel(
                new ParPropuestoDto
                {
                    IdTermino = 1,
                    Termino = "CUADERNO",
                    SegundaPalabra = "TAPA",
                    IdCategoria = 10,
                    NombreCategoria = "Cuadernos",
                    Coincidencias = 8,
                    Total = 8,
                    Ejemplos = ["CUADERNO TAPA DURA 48H"]
                },
                propuesta));
            var raiz = Montar(dialog, 960, 640);
            var combo = BuscarCombo(raiz, grilla: null, viewModel.Terminos.Propuestos.Categorias);
            elegida = otra;

            // Act
            combo.SelectedItem = otra;
            enLaPropuesta = viewModel.Terminos.Propuestos.Propuestas[0].CategoriaElegida;
        });

        // Assert
        enLaPropuesta.Should().Be(elegida, "la categoría elegida en la celda debe llegar a la propuesta");
    }

    /// <summary>
    /// Monta el contenido de la ventana en un elemento suelto, con los mismos recursos y DataContext, y lo mide: una
    /// ventana sin mostrar no arma el árbol visual de su contenido, y mostrarla procesa mensajes que en el proceso de
    /// pruebas fallan (WPF-UI carga un recurso diferido por URI pack).
    /// </summary>
    private static Border Montar(Window ventana, double ancho, double alto)
    {
        var contenido = (UIElement)ventana.Content;
        ventana.Content = null;
        var raiz = new Border
        {
            Child = contenido,
            DataContext = ventana.DataContext,
            Resources = ventana.Resources
        };
        raiz.Measure(new Size(ancho, alto));
        raiz.Arrange(new Rect(0, 0, ancho, alto));
        raiz.UpdateLayout();
        return raiz;
    }

    /// <summary>
    /// Primer combo dentro de una celda de grilla cuyo origen de opciones es <paramref name="opciones"/>.
    /// </summary>
    private static ComboBox BuscarCombo(DependencyObject raiz, string? grilla, object opciones)
    {
        var grillas = Descendientes<DataGrid>(raiz).Where(g => grilla is null || g.Name == grilla);
        return grillas
            .SelectMany(g => Descendientes<ComboBox>(g))
            .First(c => ReferenceEquals(c.ItemsSource, opciones));
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
