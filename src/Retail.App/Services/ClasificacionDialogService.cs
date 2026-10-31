using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Retail.App.ViewModels.Articulos;
using Retail.App.Views.Dialogs;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;

namespace Retail.App.Services;

public class ClasificacionDialogService : IClasificacionDialogService
{
    private readonly IServiceProvider _serviceProvider;

    public ClasificacionDialogService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public bool AbrirFormularioRubro(RubroDto? existente)
    {
        var vm = _serviceProvider.GetRequiredService<ClasificacionFormViewModel>();
        if (existente is null)
        {
            vm.ConfigurarAltaRubro();
        }
        else
        {
            vm.ConfigurarEdicionRubro(existente);
        }

        return Mostrar(vm);
    }

    public bool AbrirFormularioCategoria(CategoriaDto? existente, IReadOnlyList<RubroDto> rubros, int? idRubroPreseleccionado)
    {
        ArgumentNullException.ThrowIfNull(rubros);

        var vm = _serviceProvider.GetRequiredService<ClasificacionFormViewModel>();
        if (existente is null)
        {
            vm.ConfigurarAltaCategoria(rubros, idRubroPreseleccionado);
        }
        else
        {
            vm.ConfigurarEdicionCategoria(existente, rubros);
        }

        return Mostrar(vm);
    }

    public bool AbrirFormularioMarca(MarcaDto? existente)
    {
        var vm = _serviceProvider.GetRequiredService<ClasificacionFormViewModel>();
        if (existente is null)
        {
            vm.ConfigurarAltaMarca();
        }
        else
        {
            vm.ConfigurarEdicionMarca(existente);
        }

        return Mostrar(vm);
    }

    public ResultadoFormularioDiccionario? AbrirFormularioTermino(TerminoDto? existente, IReadOnlyList<CategoriaDto> categorias)
    {
        ArgumentNullException.ThrowIfNull(categorias);

        var opciones = new List<OpcionDiccionario> { new(null, "Ambiguo (sin categoría)") };
        opciones.AddRange(categorias.Select(c => new OpcionDiccionario(c.IdCategoria, c.NombreCategoria)));

        return Mostrar(new DiccionarioFormViewModel(
            existente is null ? "Nuevo término" : "Cambiar categoría del término",
            "Término *",
            existente?.Termino ?? string.Empty,
            textoEditable: existente is null,
            "Categoría",
            opciones,
            existente?.IdCategoria,
            opcionObligatoria: false));
    }

    public ResultadoFormularioDiccionario? AbrirFormularioAlias(MarcaDto marca)
    {
        return Mostrar(FormularioDeMarca("Nuevo alias de marca", "Alias *", marca));
    }

    public ResultadoFormularioDiccionario? AbrirFormularioPrefijo(MarcaDto marca)
    {
        return Mostrar(FormularioDeMarca("Nuevo prefijo de marca", "Prefijo EAN *", marca));
    }

    public ResultadoFormularioDiccionario? AbrirFormularioPar(TerminoDto termino, ParDto? existente, IReadOnlyList<CategoriaDto> categorias)
    {
        ArgumentNullException.ThrowIfNull(termino);
        ArgumentNullException.ThrowIfNull(categorias);

        var opciones = new List<OpcionDiccionario> { new(null, "Ignorado (no sugerir categoría)") };
        opciones.AddRange(categorias.Select(c => new OpcionDiccionario(c.IdCategoria, c.NombreCategoria)));

        return Mostrar(new DiccionarioFormViewModel(
            existente is null
                ? $"Nuevo par de «{termino.Termino}»"
                : $"Cambiar el par de «{termino.Termino}»",
            "Segunda palabra *",
            existente?.SegundaPalabra ?? string.Empty,
            textoEditable: existente is null,
            "Categoría",
            opciones,
            existente?.IdCategoria,
            opcionObligatoria: false));
    }

    public bool AbrirAdministracion()
    {
        var vm = _serviceProvider.GetRequiredService<AdministrarClasificacionViewModel>();
        try
        {
            var dialog = _serviceProvider.GetRequiredService<AdministrarClasificacionDialog>();
            dialog.DataContext = vm;
            AsignarPropietario(dialog);
            dialog.ShowDialog();
            return vm.HuboCambios;
        }
        finally
        {
            vm.Dispose();
        }
    }

    public bool Confirmar(string titulo, string mensaje)
    {
        var resultado = MessageBox.Show(
            mensaje,
            titulo,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return resultado == MessageBoxResult.Yes;
    }

    public void MostrarError(string titulo, string mensaje)
    {
        MessageBox.Show(mensaje, titulo, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static bool Mostrar(ClasificacionFormViewModel vm)
    {
        var dialog = new ClasificacionFormDialog
        {
            DataContext = vm
        };

        AsignarPropietario(dialog);

        var resultado = dialog.ShowDialog();
        return resultado == true && vm.DialogResult;
    }

    /// <summary>
    /// La marca ya viene elegida en la sección Marcas (M-68): es la única opción, preseleccionada y no editable, y el
    /// formulario la muestra para confirmar a qué marca se agrega.
    /// </summary>
    private static DiccionarioFormViewModel FormularioDeMarca(string titulo, string etiquetaTexto, MarcaDto marca)
    {
        ArgumentNullException.ThrowIfNull(marca);

        return new DiccionarioFormViewModel(
            titulo,
            etiquetaTexto,
            string.Empty,
            textoEditable: true,
            "Marca",
            [new OpcionDiccionario(marca.IdMarca, marca.NombreMarca)],
            idOpcionInicial: marca.IdMarca,
            opcionObligatoria: true);
    }

    private static ResultadoFormularioDiccionario? Mostrar(DiccionarioFormViewModel vm)
    {
        var dialog = new DiccionarioFormDialog
        {
            DataContext = vm
        };

        AsignarPropietario(dialog);

        var resultado = dialog.ShowDialog();
        return resultado == true && vm.DialogResult ? vm.Resultado : null;
    }

    private static void AsignarPropietario(Window dialog)
    {
        // Un diálogo abierto desde otro diálogo debe colgar de la ventana activa y no del Shell.
        var propietario = System.Windows.Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsActive) ?? System.Windows.Application.Current.MainWindow;
        if (propietario is { IsVisible: true })
        {
            dialog.Owner = propietario;
        }
    }
}
