using CommunityToolkit.Mvvm.ComponentModel;

namespace Retail.App.ViewModels.Sistema;

/// <summary>
/// Una fila del diálogo de diagnóstico de arranque: la línea de comando a copiar y el texto de su botón,
/// que cambia a "Copiado" un instante como confirmación.
/// </summary>
public sealed partial class ComandoCopiableViewModel : ObservableObject
{
    public const string TextoCopiar = "Copiar";

    public ComandoCopiableViewModel(string linea)
    {
        Linea = linea;
    }

    public string Linea { get; }

    [ObservableProperty]
    private string _textoBoton = TextoCopiar;
}
