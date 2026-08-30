# Retail.Client.Wpf (Capa de Presentación Desktop / WPF .NET 8 con MVVM)

`Retail.Client.Wpf` es la **aplicación de escritorio nativa de Windows** instalada en cada puesto de trabajo de la tienda (terminales de mostrador y oficinas de administración). Diseñada bajo el patrón **Model-View-ViewModel (MVVM)** utilizando `CommunityToolkit.Mvvm`, ofrece una experiencia de usuario ágil, orientada a la operación por teclado y lector de códigos de barra, desacoplada por completo de la base de datos y conectada a la Web API mediante clientes HTTP resilientes con **Polly**.

---

## 🎯 Objetivos de la Capa

1. **Velocidad y Ergonomía de Operación en Mostrador (`IU-02`, `RNF-01`):**
   Garantizar respuestas en < 150 ms para el escaneo y cobro de productos, permitiendo operar al 100% mediante teclado y atajos rápidos (F1-F12) e integración transparente con lectores de códigos de barra.
2. **Desacoplamiento Estricto mediante Patrón MVVM:**
   Separar completamente la interfaz gráfica (XAML) de la lógica de presentación (ViewModels) y de los modelos de transporte (`Retail.Shared.DTOs`). Cero referencias a EF Core o bases de datos relacionales.
3. **Resiliencia ante Microcortes de Red LAN (`RNF-08`):**
   Implementar políticas de reintento automático (*exponential backoff retry policies*) con `Polly` para absorber microcortes transitorios en switches o cables de red de la tienda sin interrumpir la venta.
4. **Seguridad y Control de Sesión en Memoria (`RF-01`, `RF-02`, `RNF-04`):**
   Gestionar el token JWT en memoria mediante `SessionManager`, emitir pulsos de `Heartbeat` periódicos y responder visualmente ante desalojos (*Kick-Out*) o cuadros de diálogo modales.
5. **Integración con Hardware de Punto de Venta:**
   Control de impresoras térmicas de tickets (comandos ESC/POS) para la emisión instantánea de comprobantes de control interno y tickets fiscales.

---

## 📁 Estructura del Directorio

```text
src/Frontend/Retail.Client.Wpf/
├── ViewModels/                          # Lógica de presentación con CommunityToolkit.Mvvm
│   ├── MainViewModel.cs                 # Shell principal y navegación por roles
│   ├── LoginViewModel.cs                # Autenticación y diálogo de Kick-Out
│   ├── PosViewModel.cs                  # Venta rápida, lector de códigos y atajos F1-F12
│   ├── CobroModalViewModel.cs           # Cobro multimedio y cálculo de vuelto
│   ├── CajaViewModel.cs                 # Apertura de turno y Arqueo Ciego
│   ├── ArticulosViewModel.cs            # ABM Artículos y cálculo visual de Markup
│   ├── ImportadorCatalogosViewModel.cs  # Mapeo de columnas y barra de progreso
│   ├── ComprasViewModel.cs              # Registro de facturas/remitos de proveedores
│   ├── ConsolaFiscalViewModel.cs        # Panel Gerencial de reintentos ARCA
│   └── UsuariosViewModel.cs             # Administración de perfiles y sesiones activas
├── Views/                               # Interfaces de usuario en XAML
│   ├── Windows/
│   │   ├── MainWindow.xaml              # Ventana contenedora principal
│   │   └── LoginWindow.xaml             # Ventana de autenticación
│   ├── Pages/                           # Vistas de contenido / UserControls
│   │   ├── PosView.xaml                 # Mostrador de ventas y búsqueda ágil
│   │   ├── CajaView.xaml                # Panel de control de caja
│   │   ├── ArticulosView.xaml           # Grilla de inventario y edición
│   │   ├── ImportadorView.xaml          # Asistente de importación masiva
│   │   ├── ComprasView.xaml             # Carga de comprobantes de compra
│   │   ├── ConsolaFiscalView.xaml       # Monitor y reintentos fiscales
│   │   └── UsuariosView.xaml            # Panel de seguridad gerencial
│   └── Dialogs/                         # Cuadros de diálogo modales no bloqueantes
│       ├── CobroModalDialog.xaml        # Modal de selección de medios de pago
│       ├── KickOutDialog.xaml           # Modal de confirmación de desalojo de sesión
│       ├── ArqueoCiegoDialog.xaml       # Formulario de declaración de valores
│       └── DevolucionDialog.xaml        # Formulario de devolución con autorización
├── Services/                            # Servicios de infraestructura cliente
│   ├── ApiClient/
│   │   ├── IRetailApiClient.cs          # Contrato de comunicación HTTP
│   │   ├── RetailApiClient.cs           # HttpClient con autenticación JWT
│   │   └── PollyPolicies.cs             # Políticas de reintento ante microcortes LAN
│   ├── Navigation/
│   │   └── NavigationService.cs         # Navegación desacoplada entre vistas
│   ├── Dialog/
│   │   └── DialogService.cs             # Apertura desacoplada de diálogos modales
│   ├── Session/
│   │   ├── SessionManager.cs            # Almacenamiento seguro del JWT en memoria
│   │   └── HeartbeatTimerService.cs     # Emisión periódica de pulsos de actividad
│   └── Hardware/
│       └── TicketPrinterService.cs      # Impresión térmica directa (ESC/POS)
├── Styles/                              # Recursos de diseño visual
│   ├── Colors.xaml                      # Paleta cromática contemporánea
│   ├── Typography.xaml                  # Jerarquía tipográfica
│   ├── Controls.xaml                    # Estilos de botones, DataGrids, TextBox y Badges
│   └── Icons.xaml                       # Recursos vectoriales XAML
├── App.xaml                             # Diccionarios de recursos globales
└── App.xaml.cs                          # Configuración de Host / Inyección de Dependencias
```

---

## 🧩 Implementación con `CommunityToolkit.Mvvm`

El cliente utiliza los *Source Generators* de `CommunityToolkit.Mvvm` para eliminar código repetitivo (*boilerplate*) y asegurar el máximo rendimiento:

```csharp
public partial class PosViewModel : ObservableObject
{
    private readonly IRetailApiClient _apiClient;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private string _codigoBarrasInput = string.Empty;

    [ObservableProperty]
    private ObservableCollection<DetalleVentaItemViewModel> _items = new();

    [ObservableProperty]
    private decimal _totalVenta;

    public PosViewModel(IRetailApiClient apiClient, IDialogService dialogService)
    {
        _apiClient = apiClient;
        _dialogService = dialogService;
    }

    [RelayCommand]
    private async Task AgregarArticuloAsync()
    {
        if (string.IsNullOrWhiteSpace(CodigoBarrasInput)) return;

        var articulo = await _apiClient.GetArticuloPorCodigoAsync(CodigoBarrasInput);
        if (articulo != null)
        {
            Items.Add(new DetalleVentaItemViewModel(articulo));
            RecalcularTotal();
            CodigoBarrasInput = string.Empty;
        }
    }

    [RelayCommand]
    private async Task AbrirCobroModalAsync()
    {
        if (!Items.Any()) return;
        var resultado = await _dialogService.ShowCobroModalAsync(TotalVenta);
        if (resultado.CobroExitoso)
        {
            // Registrar venta y limpiar grilla para la siguiente operación
            Items.Clear();
            TotalVenta = 0;
        }
    }
}
```

---

## 📡 Resiliencia y Comunicación con la Web API (`PollyPolicies.cs`)

Para proteger la operación del mostrador contra fluctuaciones en switches o cables de red LAN (`RNF-08`), las solicitudes HTTP están protegidas con políticas de `Polly`:

```csharp
public static class PollyPolicies
{
    public static IAsyncPolicy<HttpResponseMessage> GetLanRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError() // Errores 5xx y fallos de conexión TCP/IP
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    // Notificar o loguear microcorte transitorio en LAN
                });
    }
}
```

---

## 🖨️ Impresión Térmica ESC/POS (`TicketPrinterService.cs`)

El servicio de impresión se conecta directamente a impresoras térmicas de mostrador (USB, serie o red) mediante comandos binarios estándar ESC/POS para emitir:
- Tickets de venta interna no fiscal en contingencia (`RF-18`).
- Presupuestos con la leyenda *"Documento No Válido como Factura"* (`RF-11`).
- Actas de apertura y cierre de turno con Arqueo Ciego (`RF-16`).

---

## 🚫 Buenas Prácticas y Restricciones de Desarrollo

- **Cero Código de Negocio en Vistas (Code-Behind Limpio):** Los archivos `.xaml.cs` deben contener únicamente la inicialización obligatoria (`InitializeComponent()`) y enlace de eventos de control visual cuando sea indispensable.
- **Asincronía Obligatoria (`async/await`):** Toda interacción con la Web API o procesamiento pesado debe ejecutarse de forma asíncrona para no bloquear el hilo de la interfaz de usuario (*WPF Dispatcher*), manteniendo la ventana siempre fluida e interactiva (`RNF-02`).
- **Navegación Controlada por Roles:** El menú lateral y las pantallas accesibles se configuran dinámicamente al momento del login según los claims del token JWT (`Cajero`, `Encargado`, `Gerente`).
