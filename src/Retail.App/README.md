# Retail.App (Aplicación de Escritorio WPF .NET 8 / MVVM)

`Retail.App` es el **proyecto ejecutable principal** del sistema Retail. Es una aplicación de escritorio nativa para Windows desarrollada bajo **WPF (.NET 8)** que implementa el patrón **MVVM** mediante el paquete oficial `CommunityToolkit.Mvvm`. Aloja la interfaz gráfica en XAML, los ViewModels, los servicios de UI y el contenedor de Inyección de Dependencias en `App.xaml.cs`.

---

## 📁 Estructura del Directorio

```text
src/Retail.App/
├── ViewModels/                          # ViewModels basados en CommunityToolkit.Mvvm
│   ├── MainViewModel.cs                 # Navegación general, barra superior y usuario activo
│   ├── LoginViewModel.cs                # Autenticación local y cambio de usuario
│   ├── PosViewModel.cs                  # Venta rápida, lector, cliente y conversión de presupuesto
│   ├── CobroModalViewModel.cs           # Multimedio con soporte de Cuenta Corriente y cálculo de vuelto
│   ├── PresupuestosViewModel.cs         # Generador de cotizaciones y emisión de comprobantes impresos
│   ├── ClientesViewModel.cs             # ABM de clientes, consulta de saldo y comando AbrirCobranzaCommand
│   ├── CobranzaModalViewModel.cs        # Modal para cancelar saldos de clientes (efectivo, tarjeta, QR)
│   ├── CajaViewModel.cs                 # Apertura, movimientos de caja y Arqueo Ciego de efectivo
│   ├── ArticulosViewModel.cs            # Catálogo propio, markup y soporte de productos artesanales
│   ├── ImportadorCatalogosViewModel.cs  # Mapeo de columnas y procesamiento en segundo plano
│   ├── ComprasViewModel.cs              # Registro de facturas y recálculo automático de precios
│   └── ConsolaFiscalViewModel.cs        # Panel gerencial de reintentos fiscales ARCA
├── Views/
│   ├── Windows/
│   │   ├── MainWindow.xaml              # Ventana principal del sistema
│   │   └── LoginWindow.xaml             # Ventana modal de acceso
│   ├── Pages/                           # Vistas de contenido intercambiables
│   │   ├── PosView.xaml
│   │   ├── PresupuestosView.xaml
│   │   ├── ClientesView.xaml
│   │   ├── CajaView.xaml
│   │   ├── ArticulosView.xaml
│   │   ├── ImportadorView.xaml
│   │   ├── ComprasView.xaml
│   │   └── ConsolaFiscalView.xaml
│   └── Dialogs/                         # Modales de interacción
│       ├── CobroModalDialog.xaml
│       ├── CobranzaModalDialog.xaml     # Modal para cobrar deuda de clientes
│       ├── ArqueoCiegoDialog.xaml
│       └── AlertaPreciosPresupuestoDialog.xaml
├── Services/                            # Servicios de soporte de UI
│   ├── Session/CurrentUserSession.cs    # Mantiene la identidad y rol del usuario en memoria
│   ├── Navigation/NavigationService.cs  # Navegación entre páginas y control de roles
│   ├── Dialog/DialogService.cs          # Apertura de modales
│   └── Hardware/TicketPrinterService.cs # Impresión térmica ESC/POS para tickets y recibos de cobranza
├── Styles/
│   ├── Colors.xaml                      # Paleta visual contemporánea
│   ├── Typography.xaml                  # Tipografía de la interfaz
│   ├── Controls.xaml                    # Estilos para botones, inputs, tablas y tarjetas
│   └── Icons.xaml                       # Recursos vectoriales XAML
├── App.xaml
├── App.xaml.cs                          # Configuración del Host IoC
└── appsettings.json
```

---

## ⚙️ Registro de Dependencias (`App.xaml.cs`)

```csharp
// Persistencia y Servicios
serviceCollection.AddDbContext<RetailDbContext>(options =>
    options.UseSqlServer(config.GetConnectionString("DefaultConnection")));
serviceCollection.AddScoped<IRetailDbContext>(sp => sp.GetRequiredService<RetailDbContext>());
serviceCollection.AddScoped<IUnitOfWork, UnitOfWork>();
serviceCollection.AddScoped<IArcaClient, ArcaClient>();
serviceCollection.AddScoped<IExcelCatalogParser, ExcelCatalogParser>();
serviceCollection.AddScoped<IPasswordHasher, PasswordHasher>();

// Servicios de Caso de Uso
serviceCollection.AddScoped<IAuthService, AuthService>();
serviceCollection.AddScoped<IVentaService, VentaService>();
serviceCollection.AddScoped<IPresupuestoService, PresupuestoService>();
serviceCollection.AddScoped<IClienteService, ClienteService>();
serviceCollection.AddScoped<ICajaService, CajaService>();
serviceCollection.AddScoped<IInventarioService, InventarioService>();
serviceCollection.AddScoped<ICompraService, CompraService>();
serviceCollection.AddScoped<IFiscalService, FiscalService>();

// ViewModels
serviceCollection.AddTransient<MainViewModel>();
serviceCollection.AddTransient<LoginViewModel>();
serviceCollection.AddTransient<PosViewModel>();
serviceCollection.AddTransient<CobroModalViewModel>();
serviceCollection.AddTransient<PresupuestosViewModel>();
serviceCollection.AddTransient<ClientesViewModel>();
serviceCollection.AddTransient<CobranzaModalViewModel>();
serviceCollection.AddTransient<CajaViewModel>();
serviceCollection.AddTransient<ArticulosViewModel>();
serviceCollection.AddTransient<ImportadorCatalogosViewModel>();
serviceCollection.AddTransient<ComprasViewModel>();
serviceCollection.AddTransient<ConsolaFiscalViewModel>();
serviceCollection.AddTransient<UsuariosViewModel>();
```
