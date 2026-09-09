# Etapa 0: Cimientos Técnicos, Gobernanza y Contratos (Sprint 0 - Pre-Implementación)

### Proyecto: Retail (Sistema ERP & Punto de Venta para Librería)
**Documento Maestro:** [`Roadmap de Implementacion.md`](file:///c:/Users/lucas/Proyectos/retail/docs/Roadmap%20de%20Implementacion.md) | **Mapa Semántico:** [`MAPA_DEL_PROYECTO.md`](file:///c:/Users/lucas/Proyectos/retail/docs/MAPA_DEL_PROYECTO.md)  
**Estado de la Etapa:** ✅ Concluida al 100%

---

> **Objetivo:** Disponer del repositorio operativo, proyectos .NET 8 compilables, pipeline de integración continua, estándares de código unificados, sistema de diseño visual y contratos de servicios acordados antes de programar la lógica del negocio.

## Tareas Desglosadas

| ID | Tarea Técnica | Responsable | Entregable / Criterio de Éxito | Estado |
| :--- | :--- | :--- | :--- | :---: |
| **0.1** | **Scaffolding de Solución y Proyectos**<br>Crear `Retail.sln`, proyectos de clase para `Retail.Domain`, `Retail.Application`, `Retail.Infrastructure`, ejecutable `Retail.App` (WPF .NET 8) y los 4 proyectos de pruebas xUnit bajo `tests/`. Configurar referencias entre capas, `<Nullable>enable</Nullable>` y `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` en `Directory.Build.props`. | **Pablo & Lucas** | Solución compila limpiamente desde CLI (`dotnet build`) sin advertencias. | ✅ |
| **0.2** | **Estandarización y Calidad de Código**<br>Crear `.editorconfig` con reglas estrictas de formato C# y XAML. Configurar `.gitignore` completo para entornos .NET/Visual Studio/Rider. | **Pablo** | Archivo `.editorconfig` en raíz validado con `dotnet format --verify-no-changes`. | ✅ |
| **0.3** | **Pipeline de CI/CD en GitHub Actions**<br>• **CI (`ci.yml`):** en runner `windows-latest` para PRs y pushes a `main` (`restore`, `build` Release sin warnings, `test` xUnit y `format`).<br>• **CD (`release.yml`):** en runner `windows-latest` ante Git Tags (`v*`) para compilar paquete auto-contenido de `Retail.App` (win-x64), comprimir en ZIP y publicar GitHub Release oficial. | **Pablo** | Workflows funcionales, verificados localmente y validados en sintaxis. | ✅ |
| **0.4** | **Definición de Contratos "Contract-First"**<br>Escribir en `Retail.Application` los contratos de interfaces: `IAuthService`, `IUsuarioService`, `IVentaService`, `IPresupuestoService`, `IClienteService`, `ICajaService`, `IInventarioService`, `ICompraService`, `IFiscalService` y sus DTOs principales de transporte. | **Pablo & Lucas** | Interfaces tipadas con firmas async documentadas y acordadas por ambos desarrolladores. | ✅ |
| **0.5** | **Aislamiento y Mocks de Hardware / Terceros**<br>Definir la interfaz `ITicketPrinterService` con su implementación de prueba `FileDebugTicketPrinterService` (salida a consola/archivo de texto). Crear `MockArcaClient` con bandera `"UseMockArca": true` en `appsettings.json` para simular respuestas fiscales sin requerir certificado AFIP. | **Pablo** | Pruebas unitarias que confirmen el comportamiento simulado sin dependencias externas. | ✅ |
| **0.6** | **Sistema de Diseño XAML y Guía de Estilos**<br>Integrar librería de controles modernos (diccionarios base en `Retail.App/Styles/`: `Colors.xaml`, `Typography.xaml`, `Controls.xaml`, `Icons.xaml`). Definir tokens de color (primario, advertencia de caja/stock, error fiscal, superficies de mostrador) y tipografía monoespaciada para valores monetarios. | **Lucas** | Galería de controles en XAML ([`StyleGalleryView.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Dev/StyleGalleryView.xaml)) que valide visualmente botones, inputs, modales y tablas. | ✅ |
| **0.7** | **Infraestructura de DB y Semillero Inicial**<br>Crear `RetailDbContext`, mapear la cadena de conexión para `LocalDB` (desarrollo) y configurar el `DbInitializer` que inserte roles (`Gerente`, `Encargado`, `Cajero`), usuario `admin`, categorías base y 20 artículos de librería simulados. | **Pablo** | Migración inicial generada (`InitialCreate`) y base poblada automáticamente en primer arranque. | ✅ |
| **0.8** | **Manejo Centralizado de Excepciones y Logging**<br>Configurar **Serilog** con destino a archivo local rotativo (`logs/retail-.log`). Conectar manejadores globales en `App.xaml.cs` (`DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`) con despliegue de diálogo visual amigable ([`UnhandledExceptionDialog.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Dialogs/UnhandledExceptionDialog.xaml)). | **Lucas** | Una excepción no controlada provocada deliberadamente no cuelga la app y se escribe en el log. | ✅ |

---

## Criterios de Calidad y Cierre de Etapa
1. Solución compila en Release sin warnings (`TreatWarningsAsErrors`).
2. Suite completa de tests en verde: `dotnet test Retail.sln`.
3. Estilo validado: `dotnet format --verify-no-changes`.
