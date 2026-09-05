# Roadmap de Implementación y Plan de Acciones Inmediatas

### Proyecto: Retail (Sistema ERP & Punto de Venta para Librería)
**Versión:** 1.0  
**Fecha:** Septiembre de 2026  
**Equipo:** Pablo Fernandez (Líder Técnico / Backend) & Lucas Kruzolek (UI Designer / Frontend)  
**Documentos de Referencia:** [ERS v3.2](file:///c:/Users/lucas/Proyectos/retail/ERS%20-%20Libreria%20POS.md), [Arquitectura y Estructura](file:///c:/Users/lucas/Proyectos/retail/Arquitectura%20y%20Estructura%20del%20Proyecto.md), [DER](file:///c:/Users/lucas/Proyectos/retail/DER.mmd)

---

## 🧭 Visión General del Roadmap

El presente documento establece la hoja de ruta técnica ordenada por etapas para guiar la construcción del sistema **Retail**, optimizado para un equipo de **dos desarrolladores**.

El modelo de trabajo adopta un enfoque **Contract-First (Basado en Contratos)** y **Vertical Slices (Rebanadas Verticales)**:
1. Las interfaces y modelos de intercambio de datos se definen y congelan primero en la capa de Aplicación.
2. Ambos desarrolladores avanzan en paralelo sin bloqueos mutuos mediante el uso de dobles de prueba (*mocks* y *stubs*).
3. Cada funcionalidad se integra de forma continua a través de pruebas automatizadas en un pipeline de CI.

```mermaid
gantt
    title Cronograma de Etapas y Épicas de Trabajo
    dateFormat  YYYY-MM-DD
    axisFormat  %d/%m

    section Etapa 0
    Cimientos Técnicos, CI y Contratos      :active, e0, 2026-09-08, 5d

    section Etapa 1
    Épica 1: Auth, RBAC y Shell WPF          :e1, after e0, 6d

    section Etapa 2
    Épica 2: Catálogo y MiniExcel           :e2, after e1, 8d

    section Etapa 3
    Épica 3: Caja, Clientes y Cobranza Cta Cte:e3, after e2, 9d

    section Etapa 4
    Épica 4: Punto de Venta (POS) y Stock    :e4, after e3, 10d

    section Etapa 5
    Épica 5: Presupuestos y Compras con Markup:e5, after e4, 8d

    section Etapa 6
    Épica 6: Facturación ARCA y Empaquetado  :e6, after e5, 8d
```

---

## 📍 Etapa 0: Cimientos Técnicos, Gobernanza y Contratos (Sprint 0 - Pre-Implementación)

> **Objetivo:** Disponer del repositorio operativo, proyectos .NET 8 compilables, pipeline de integración continua, estándares de código unificados, sistema de diseño visual y contratos de servicios acordados antes de programar la lógica del negocio.

### Tareas Desglosadas

| ID | Tarea Técnica | Responsable | Entregable / Criterio de Éxito |
| :--- | :--- | :--- | :--- |
| **0.1** | **Scaffolding de Solución y Proyectos**<br>Crear `Retail.sln`, proyectos de clase para `Retail.Domain`, `Retail.Application`, `Retail.Infrastructure`, ejecutable `Retail.App` (WPF .NET 8) y los 4 proyectos de pruebas xUnit bajo `tests/`. Configurar referencias entre capas, `<Nullable>enable</Nullable>` y `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` en `Directory.Build.props`. | **Pablo & Lucas** | Solución compila limpiamente desde CLI (`dotnet build`) sin advertencias. |
| **0.2** | **Estandarización y Calidad de Código**<br>Crear `.editorconfig` con reglas estrictas de formato C# y XAML. Configurar `.gitignore` completo para entornos .NET/Visual Studio/Rider. | **Pablo** | Archivo `.editorconfig` en raíz validado con `dotnet format --verify-no-changes`. |
| **0.3** | **Pipeline de CI en GitHub Actions**<br>Configurar workflow `.github/workflows/ci.yml` en runner `windows-latest` disparado en PRs a `main` que ejecute: `dotnet restore`, `dotnet build -c Release`, `dotnet test` y verificación de formato. Proteger la rama `main` requiriendo CI verde y 1 aprobación. | **Pablo** | Pull Request de prueba verificado y validado por el action de GitHub. |
| **0.4** | **Definición de Contratos "Contract-First"**<br>Escribir en `Retail.Application` los contratos de interfaces: `IAuthService`, `IVentaService`, `IPresupuestoService`, `IClienteService`, `ICajaService`, `IInventarioService`, `ICompraService`, `IFiscalService` y sus DTOs principales de transporte. | **Pablo & Lucas** | Interfaces tipadas con firmas async documentadas y acordadas por ambos desarrolladores. |
| **0.5** | **Aislamiento y Mocks de Hardware / Terceros**<br>Definir la interfaz `ITicketPrinterService` con su implementación de prueba `FileDebugTicketPrinterService` (salida a consola/archivo de texto). Crear `MockArcaClient` con bandera `"UseMockArca": true` en `appsettings.json` para simular respuestas fiscales sin requerir certificado AFIP. | **Pablo** | Pruebas unitarias que confirmen el comportamiento simulado sin dependencias externas. |
| **0.6** | **Sistema de Diseño XAML y Guía de Estilos**<br>Integrar librería de controles modernos (ej. `WPF-UI` o diccionarios base en `Retail.App/Styles/`: `Colors.xaml`, `Typography.xaml`, `Controls.xaml`, `Icons.xaml`). Definir tokens de color (primario, advertencia de caja/stock, error fiscal, superficies de mostrador) y tipografía monoespaciada para valores monetarios. | **Lucas** | Ventana de prueba o galería de controles en XAML que valide visualmente botones, inputs, modales y tablas. |
| **0.7** | **Infraestructura de DB y Semillero Inicial**<br>Crear `RetailDbContext`, mapear la cadena de conexión para `LocalDB` (desarrollo) y configurar el `DbInitializer` que inserte roles (`Gerente`, `Encargado`, `Cajero`), usuario `admin`, categorías base y 20 artículos de librería simulados. | **Pablo** | Migración inicial generada (`InitialCreate`) y base poblada automáticamente en primer arranque. |
| **0.8** | **Manejo Centralizado de Excepciones y Logging**<br>Configurar **Serilog** con destino a archivo local rotativo (`logs/retail-.log`). Conectar manejadores globales en `App.xaml.cs` (`DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`) con despliegue de diálogo visual amigable. | **Lucas** | Una excepción no controlada provocada deliberadamente no cuelga la app y se escribe en el log. |

---

## 📍 Etapa 1: Épica 1 - Autenticación, RBAC y Shell de Navegación

> **Objetivo:** Permitir el acceso seguro de usuarios según su rol, persistencia de credenciales protegidas e interfaz base de navegación para el mostrador.
> **Requisitos Vinculados:** `RF-01`, `RF-02`, `RF-03`, `RNF-04`, `RNF-06`.

### Tareas y Distribución
* **Backend (Pablo):**
  * Implementar `PasswordHasher` con BCrypt (`BCrypt.Net-Next`).
  * Implementar `AuthService` y validaciones de credenciales contra `USUARIOS` y `ROLES`.
  * Casos de uso de administración de usuarios (alta, baja lógica y blanqueo de clave para rol Gerente).
  * Pruebas unitarias de hashing y autenticación en `Retail.Application.UnitTests/AuthServiceTests.cs`.
* **Frontend (Lucas):**
  * Diseñar `LoginWindow.xaml` con controles `PasswordBox` y enlace a `LoginViewModel`.
  * Diseñar `MainWindow.xaml` con barra de estado superior (cajero activo, turno, hora y botón de cambio rápido de usuario).
  * Implementar `CurrentUserSession` en memoria y `NavigationService` con control de visibilidad condicional según roles (`Cajero`, `Encargado`, `Gerente`).
* **Criterio de Aceptación:** El cajero inicia sesión, se le habilita únicamente el POS y arqueo de caja. El gerente visualiza todas las opciones. El cambio de usuario no requiere reiniciar el ejecutable.

---

## 📍 Etapa 2: Épica 2 - Catálogo, Artículos e Importador Masivo

> **Objetivo:** Centralizar el padrón de productos, soportar artículos artesanales sin código y permitir la carga de listas masivas de distribuidores.
> **Requisitos Vinculados:** `RF-04`, `RF-05`, `RF-06`, `RF-07`, `RF-08`, `RNF-02`.

### Tareas y Distribución
* **Backend (Pablo):**
  * Mapear entidad `Articulo` en EF Core configurando el **índice filtrado único**:
    `builder.HasIndex(a => a.CodigoBarras).IsUnique().HasFilter("[codigo_barras] IS NOT NULL");`
  * Implementar `InventarioService` (CRUD con borrado lógico `deleted_at`).
  * Implementar `ExcelCatalogParser` con **MiniExcel** dentro de `Task.Run` con lectura streaming para no saturar memoria (`RNF-03`).
  * Pruebas de integración para garantizar que dos productos artesanales con `codigo_barras = NULL` coexistan sin colisión de índice.
* **Frontend (Lucas):**
  * Diseñar `ArticulosView.xaml` con grilla optimizada, filtro de búsqueda instantánea y badges visuales para productos con `stock_actual <= stock_minimo`.
  * Formulario modal de alta/edición que calcule automáticamente el `PrecioVenta` al tipear el `CostoReposicion` y el `Markup %`.
  * Diseñar `ImportadorView.xaml` con asistente de mapeo de columnas y barra de progreso asíncrona.
* **Criterio de Aceptación:** Se pueden dar de alta artesanías con código en blanco; se importa una planilla Excel de 5.000 filas en $< 3$ segundos sin congelar la interfaz.

---

## 📍 Etapa 3: Épica 3 - Turnos de Caja, Clientes y Cobranza de Cuentas Corrientes

> **Objetivo:** Controlar el flujo de dinero físico en mostrador mediante arqueo ciego y registrar la cancelación de deudas de clientes con impacto directo en caja.
> **Requisitos Vinculados:** `RF-13`, `RF-14`, `RF-15`, `RF-20`, `RNF-08`.

### Tareas y Distribución
* **Backend (Pablo):**
  * Entidades `TurnoCaja`, `MovimientoCaja`, `Cliente` y `CobranzaCliente`.
  * Lógica de `CajaService`: apertura con fondo inicial, registro de ingresos/retiros y cálculo del saldo teórico de efectivo.
  * Circuito de cobranza en `ClienteService.RegistrarCobranzaAsync`:
    * Transacción ACID que reduce `saldo_cuenta_corriente`.
    * Si es `EFECTIVO`, suma a `total_ingresos_efectivo` de la caja activa.
    * Si es electrónico, suma a `total_ventas_electronicas`.
* **Frontend (Lucas):**
  * `CajaView.xaml` con visualización de estado de turno.
  * Diálogo modal `ArqueoCiegoDialog.xaml`: el cajero ingresa el conteo físico de billetes a ciegas y el sistema imprime el acta con faltante/sobrante y el total de cupones electrónicos para cotejo POS.
  * `ClientesView.xaml` con saldo deudor visible y botón `[Registrar Cobranza]`.
  * `CobranzaModalDialog.xaml` para registrar cobro parcial o total con emisión de recibo duplicado.
* **Criterio de Aceptación:** Una cobranza en efectivo disminuye la deuda del cliente y eleva el saldo esperado de la gaveta de caja en la misma transacción.

---

## 📍 Etapa 4: Épica 4 - Punto de Venta (POS) y Descuento Atómico

> **Objetivo:** Construir el núcleo operativo de mostrador de alta velocidad, venta ágil con teclado y cobros multimedio.
> **Requisitos Vinculados:** `RF-09`, `RF-10`, `RNF-01`, `RNF-02`.

### Tareas y Distribución
* **Backend (Pablo):**
  * Implementar `VentaService.RegistrarVentaAsync`:
    * Validación de stock previo con bloqueo optimista/pesimista.
    * Inserción de cabecera `VENTAS`, líneas `DETALLE_VENTAS` e imputaciones `PAGOS_VENTA`.
    * Descuento atómico de stock dentro de la misma transacción.
    * Enlace con `ITicketPrinterService` para emitir ticket de mostrador.
* **Frontend (Lucas):**
  * Diseñar `PosView.xaml` optimizado para mostrador: soporte estricto de atajos de teclado (`F1` a `F12`), lectura de scanner de código de barras en buffer continuo y foco automático.
  * `CobroModalDialog.xaml` multimedio (efectivo con cálculo dinámico de vuelto, tarjetas, transferencias y cuenta corriente con validación de límite de crédito).
* **Criterio de Aceptación:** Una venta con 3 ítems se procesa e imprime en $< 50$ ms; si el cliente no tiene crédito suficiente en cuenta corriente, el sistema bloquea la operación.

---

## 📍 Etapa 5: Épica 5 - Presupuestador y Compras con Recálculo Automático

> **Objetivo:** Gestionar cotizaciones independientes sin descontar stock y automatizar la protección del margen comercial ante facturas de proveedores.
> **Requisitos Vinculados:** `RF-11`, `RF-12`, `RF-19`.

### Tareas y Distribución
* **Backend (Pablo):**
  * Entidades `Presupuesto` y `DetallePresupuesto` con vigencia estricta de 15 días y precios unitarios pactados congelados.
  * Lógica de conversión de presupuesto a venta en `PresupuestoService`: audita stock actual en góndola y verifica discrepancias contra precios vigentes.
  * Registro de facturas de proveedores en `CompraService`: incrementa stock y ejecuta `Articulo.ActualizarCostoYRecalcularPrecio()` actualizando el precio de venta en base al markup.
* **Frontend (Lucas):**
  * `PresupuestosView.xaml` para confección e impresión de cotizaciones.
  * Botón en `PosView` para recuperar presupuestos por número, mostrando el diálogo de alerta `AlertaPreciosPresupuestoDialog` si los precios del catálogo cambiaron.
  * `ComprasView.xaml` para ingreso rápido de facturas de distribuidores con previsualización del nuevo precio de venta sugerido.
* **Criterio de Aceptación:** Al ingresar una compra con costo superior, el precio de venta en catálogo se actualiza automáticamente. Un presupuesto vencido (> 15 días) es rechazado al intentar cobrarlo.

---

## 📍 Etapa 6: Épica 6 - Facturación Fiscal ARCA, Resiliencia y Empaquetado

> **Objetivo:** Automatizar la emisión tributaria ante AFIP/ARCA, garantizar ventas ininterrumpidas ante caídas de red y empaquetar la aplicación para producción.
> **Requisitos Vinculados:** `RF-16`, `RF-17`, `RF-18`, `RNF-07`.

### Tareas y Distribución
* **Backend (Pablo):**
  * Implementar `ArcaClient` sobre `HttpClient` hacia `http://localhost:8080/` con timeout de 10s.
  * Discriminación automática: Factura A para clientes Responsables Inscriptos; Factura B para el resto.
  * Mecanismo de contingencia resiliente: ante fallo de red o caída del microservicio, guarda la venta como `ERROR_FISCAL_REINTENTABLE` registrando el `motivo_error`.
  * Método de reintento secuencial en lote para la consola gerencial.
* **Frontend (Lucas):**
  * `ConsolaFiscalView.xaml` para el rol Gerente: grilla con ventas pendientes de autorización y botón `[Reintentar Lote]`.
  * Indicador de salud de conexión fiscal en la barra de estado de la ventana principal.
  * Generación del instalador final auto-contenido y validación en un equipo Windows limpio.
* **Criterio de Aceptación:** Al desconectar el cable de red, el POS concluye la venta normalmente en contingencia sin colgarse; al restablecer conexión, el gerente autoriza los comprobantes en lote con un clic.

---

## 📋 Matriz de Responsabilidades y Gobernanza del Equipo

```text
               PABLO FERNANDEZ                          LUCAS KRUZOLEK
         (Líder Técnico / Backend)                (UI Designer / Frontend)
       ┌──────────────────────────────┐         ┌──────────────────────────────┐
       │ • Retail.Domain (Reglas)     │         │ • Retail.App (Vistas XAML)   │
       │ • Retail.Infrastructure (EF) │         │ • ViewModels (MvvmToolkit)   │
       │ • Retail.Application (Serv.) │  ◄───►  │ • Design System & Estilos    │
       │ • CI / GitHub Actions        │         │ • Ergonomía & Atajos F1-F12  │
       │ • Integración SQL Server     │         │ • Experiencia de Mostrador   │
       └──────────────────────────────┘         └──────────────────────────────┘
                       ▲                                       ▲
                       └───────────────┬───────────────────────┘
                                       │
                         ACUERDO EN CONTRATOS PREVIOS
                         • Interfaces de Servicios C#
                         • DTOs de Entrada / Salida
                         • Revisión Cruzada de PRs (100%)
```

### Reglas de Operación del Equipo (Políticas de Git)
1. **GitHub Flow:** La rama `main` siempre compila y pasa todas las pruebas. Se trabaja en ramas cortas por tarea (`feat/nombre-tarea` o `fix/nombre-bug`).
2. **Revisión Cruzada Obligatoria (Peer Review):** Todo PR requiere la aprobación del otro desarrollador antes de fusionarse. Esto garantiza que ambos conozcan todo el código fuente.
3. **Ninguna advertencia permitida:** Código con warnings de compilación es rechazado por el CI.
4. **Commits Semánticos:** Seguir la convención `feat:`, `fix:`, `refactor:`, `test:`, `docs:`.
