using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Retail.App.Services;

namespace Retail.App.ViewModels.Articulos;

/// <summary>
/// ViewModel del formulario modal único de los diccionarios del motor de catalogación (Track 6.6, 6.3 y 6.8):
/// término + categoría, marca + alias, marca + prefijo y segunda palabra + categoría. Solo captura los datos: la
/// escritura la hace la sección que lo abre, que es quien conoce el servicio y muestra los errores del servidor.
/// </summary>
public partial class DiccionarioFormViewModel : ObservableObject
{
    private readonly bool _opcionObligatoria;

    [ObservableProperty]
    private string _texto;

    [ObservableProperty]
    private OpcionDiccionario? _opcionSeleccionada;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string? _mensajeError;

    /// <param name="titulo">Título de la ventana.</param>
    /// <param name="etiquetaTexto">Etiqueta del campo de texto (por ejemplo, "Término *").</param>
    /// <param name="textoInicial">Texto precargado (vacío en un alta).</param>
    /// <param name="textoEditable">Falso cuando se cambia solo el destino de algo que ya existe.</param>
    /// <param name="etiquetaOpcion">Etiqueta de la lista desplegable.</param>
    /// <param name="opciones">Opciones de la lista.</param>
    /// <param name="idOpcionInicial">Opción preseleccionada; nulo elige la opción "sin destino" si existe.</param>
    /// <param name="opcionObligatoria">Verdadero cuando hay que elegir una opción con Id (marcas).</param>
    public DiccionarioFormViewModel(
        string titulo,
        string etiquetaTexto,
        string textoInicial,
        bool textoEditable,
        string etiquetaOpcion,
        IReadOnlyList<OpcionDiccionario> opciones,
        int? idOpcionInicial,
        bool opcionObligatoria)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        TituloVentana = titulo;
        EtiquetaTexto = etiquetaTexto;
        _texto = textoInicial;
        TextoEditable = textoEditable;
        EtiquetaOpcion = etiquetaOpcion;
        _opcionObligatoria = opcionObligatoria;

        foreach (var opcion in opciones)
        {
            Opciones.Add(opcion);
        }

        _opcionSeleccionada = Opciones.FirstOrDefault(o => o.Id == idOpcionInicial)
            ?? (opcionObligatoria ? null : Opciones.FirstOrDefault(o => o.Id is null));
    }

    public string TituloVentana { get; }

    public string EtiquetaTexto { get; }

    public bool TextoEditable { get; }

    public string EtiquetaOpcion { get; }

    public ObservableCollection<OpcionDiccionario> Opciones { get; } = new();

    /// <summary>Con una sola opción (la marca ya elegida, M-68) no hay nada que elegir: la lista solo informa.</summary>
    public bool OpcionEditable => Opciones.Count > 1;

    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    /// <summary>Verdadero si el usuario aceptó el formulario.</summary>
    public bool DialogResult { get; private set; }

    /// <summary>El texto y la opción elegida; solo tiene sentido si <see cref="DialogResult"/> es verdadero.</summary>
    public ResultadoFormularioDiccionario Resultado => new(Texto.Trim(), OpcionSeleccionada?.Id);

    [RelayCommand]
    private void Aceptar(Window? window)
    {
        if (string.IsNullOrWhiteSpace(Texto))
        {
            MensajeError = $"{EtiquetaTexto.TrimEnd('*', ' ')} es obligatorio.";
            return;
        }

        if (_opcionObligatoria && OpcionSeleccionada?.Id is null)
        {
            MensajeError = $"{EtiquetaOpcion.TrimEnd('*', ' ')} es obligatorio.";
            return;
        }

        MensajeError = null;
        DialogResult = true;
        if (window is not null)
        {
            window.DialogResult = true;
            window.Close();
        }
    }

    [RelayCommand]
    private static void Cancelar(Window? window)
    {
        window?.Close();
    }
}
