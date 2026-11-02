using System.Windows;
using System.Windows.Controls;
using Retail.App.Services;

namespace Retail.App.Views.Pages;

/// <summary>
/// Vista informativa y de cortesía para módulos proyectados en el Roadmap de la cátedra
/// pero cuya implementación pertenece a etapas posteriores.
/// </summary>
public partial class ModuloEnConstruccionView : UserControl
{
    public static readonly DependencyProperty SeccionIdProperty =
        DependencyProperty.Register(
            nameof(SeccionId),
            typeof(string),
            typeof(ModuloEnConstruccionView),
            new PropertyMetadata(string.Empty));

    private readonly INavigationService? _navigationService;

    public ModuloEnConstruccionView()
    {
        InitializeComponent();
    }

    public ModuloEnConstruccionView(INavigationService navigationService) : this()
    {
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
    }

    /// <summary>Sección cuyo selector se muestra arriba a la izquierda; vacío lo colapsa.</summary>
    public string SeccionId
    {
        get => (string)GetValue(SeccionIdProperty);
        set => SetValue(SeccionIdProperty, value);
    }

    public void Configurar(
        string titulo,
        string etapa,
        string responsable,
        string requisitos,
        string descripcion)
    {
        TxtTituloModulo.Text = titulo;
        TxtEtapaBadge.Text = etapa;
        TxtDescripcion.Text = descripcion;
        TxtDetalles.Text = $"Responsable asignado: {responsable} | Requisitos ERS: {requisitos}";
    }

    private void BtnVolverCatalogo_Click(object sender, RoutedEventArgs e)
    {
        _navigationService?.NavigateTo<ArticulosView>();
    }
}
