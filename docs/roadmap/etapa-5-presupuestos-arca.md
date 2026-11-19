# Etapa 5: Épica 5 - Presupuestador Independiente y Facturación Fiscal ARCA

### Proyecto: Retail (Sistema ERP & Punto de Venta para Librería)
**Documento Maestro:** [`Roadmap de Implementacion.md`](file:///c:/Users/lucas/Proyectos/retail/docs/Roadmap%20de%20Implementacion.md) | **Mapa Semántico:** [`MAPA_DEL_PROYECTO.md`](file:///c:/Users/lucas/Proyectos/retail/docs/MAPA_DEL_PROYECTO.md)  
**Requisitos Vinculados:** [`RF-11`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L235), [`RF-12`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L236), [`RF-16`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L240), [`RF-17`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L241), [`RF-18`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L242), [`RNF-07`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L256)

---

> **Objetivo:** Confeccionar cotizaciones comerciales independientes con validez de 15 días y conversión en mostrador con control de stock, y automatizar la emisión fiscal electrónica ante AFIP/ARCA con contingencia resiliente y consola gerencial.

## Módulos e Interfaces Asignados

### Módulo 5.1: Presupuestador Independiente y Conversión a Venta
**Responsable:** Lucas Kruzolek (reasignado desde Pablo Fernandez el 2026-11-17)  
**Plan de diseño detallado:** [`etapa-5-modulo-5.1-presupuestos.md`](file:///c:/Users/lucas/Proyectos/retail/docs/roadmap/etapa-5-modulo-5.1-presupuestos.md) (decisiones PS-01 a PS-20, fases P0 a P6)

* **Interfaz Visual:** el presupuesto se **confecciona en el POS** (`PosView.xaml`): "Guardar como presupuesto" (F3) abre `GuardarPresupuestoDialog.xaml` (título interno opcional, vigencia, impresión y PDF) y "Recuperar presupuesto" (F7) abre `RecuperarPresupuestoDialog.xaml`. `AlertaPreciosPresupuestoDialog.xaml` advierte las diferencias de precio de un presupuesto vencido, los faltantes de stock y los artículos dados de baja. `PresupuestosView.xaml` (IU-13) es de **consulta**: listado con filtros, detalle (`DetallePresupuestoDialog.xaml`), reimpresión, PDF y anulación.
* **ViewModels:** `PresupuestosViewModel.cs`, `DetallePresupuestoViewModel.cs`, `GuardarPresupuestoViewModel.cs`, `RecuperarPresupuestoViewModel.cs`, `AlertaPreciosPresupuestoViewModel.cs` y cambios en `PosViewModel.cs`.
* **Lógica y Casos de Uso:** `IPresupuestoService`:
  - `EmitirAsync`: cotización a precios de catálogo, sin afectar stock ni caja (no requiere turno abierto), con título opcional y vigencia de 1 a 90 días (15 por defecto).
  - `RecuperarParaVentaAsync`: precios pactados si está vigente; si venció, informa las diferencias con el catálogo para la conciliación vía `AlertaPreciosPresupuestoDialog`. En ambos casos audita stock y artículos dados de baja.
  - `ListarAsync` (paginado), `ObtenerAsync`, `AnularAsync`, `ImprimirAsync` y `ExportarPdfAsync`.
  - La **conversión** ocurre dentro de `IVentaService.RegistrarVentaAsync` (venta con `IdPresupuestoOrigen`), en el mismo `SaveChanges` que la venta: no existe un método aparte para marcarlo como convertido.
* **Dominio:** Agregado `Presupuesto` (`IAggregateRoot`) con estados persistidos `Pendiente`, `Convertido` y `Anulado`; la vigencia es derivada (`EstaVigente(hoy)`) y la regla de precio de la conversión vive en el agregado (`ObtenerPrecioPactado`). `DetallePresupuesto` guarda la foto de la descripción y el precio pactado.
* **Persistencia:** `PresupuestoConfiguration.cs`, `DetallePresupuestoConfiguration.cs`, `PresupuestoQueryService.cs`, `rowversion` en `PRESUPUESTOS` e índice único filtrado en `VENTAS.id_presupuesto_origen`.
* **Exportación:** `ExportadorPresupuestoPdf.cs` (QuestPDF, A4) detrás de `IExportadorPresupuestoPdf`.
* **Testing:** Dominio (vigencia en el límite del día, invariantes, transiciones); servicio (emisión, recuperación, conversión con precio pactado o de catálogo); integración (conversión atómica y concurrente, filtros, PDF); ViewModels, humo STA y capturas de las vistas y diálogos.

---

### Módulo 5.3: Listas de Artículos Reutilizables
**Responsable:** Lucas Kruzolek  
**Plan de diseño detallado:** [`etapa-5-modulo-5.3-listas.md`](file:///c:/Users/lucas/Proyectos/retail/docs/roadmap/etapa-5-modulo-5.3-listas.md) (decisiones L-01 a L-21, fases L0 a L6) | **Requisitos:** RF-30, IU-14, IU-02

* **Problema:** la lista de útiles de un colegio la compran muchos padres y debe seguir los precios del proveedor; un presupuesto se consume al cobrarse y congela el precio.
* **Dominio:** agregado `ListaArticulos` (nombre y renglones de artículo y cantidad, **sin precio ni vencimiento**); `Venta` conserva `IdListaOrigen` como trazabilidad.
* **Lógica:** `IListaService` (crear, editar, actualizar renglones desde el POS, baja lógica, listar, cargar para venta, imprimir y exportar a PDF). La venta desde una lista es una venta común a precio de catálogo.
* **Interfaz Visual:** pestaña **Listas** en Ventas (`ListasView.xaml`, `ListaFormDialog.xaml`) y, en el POS, los desplegables **Cargar ▾** (F7) y **Guardar como ▾** (F3) con `SeleccionarListaDialog.xaml` y `GuardarListaDialog.xaml`.
* **Persistencia:** `LISTAS_ARTICULOS`, `RENGLONES_LISTA` y `VENTAS.id_lista_origen`; total del día y cantidad de ventas calculados en SQL.

---

### Módulo 5.2: Facturación Fiscal ARCA y Consola Gerencial
**Responsable:** Lucas Kruzolek

* **Interfaz Visual:** `ConsolaFiscalView.xaml` (panel exclusivo para el rol Gerente con grilla de comprobantes en contingencia, visualización de `motivo_error`, botón `[Reintentar Lote]` con barra de progreso) e indicador de salud del servicio fiscal en la barra de estado de `MainWindow`. Integración del comprobante al cierre del POS.
* **ViewModels:** `ConsolaFiscalViewModel.cs`.
* **Lógica y Casos de Uso:** `IFiscalService` (`EmitirComprobanteAsync`, `ObtenerPendientes`, `ReintentarLoteAsync`) y cliente HTTP tipado `ArcaClient` hacia `http://localhost:8080` (con timeout de 10s y soporte de `MockArcaClient`):
  - Discriminación tributaria: Factura A si `Cliente.CondicionIva == ResponsableInsc`; Factura B para Consumidor Final y demás casos.
  - Motor de contingencia resiliente: ante fallo de conexión, guarda la venta como `ERROR_FISCAL_REINTENTABLE` con `motivo_error` e imprime ticket no fiscal.
* **Dominio:** Entidad `ComprobanteFiscal`, `TipoComprobanteFiscalEnum`, `EstadoFiscalEnum`.
* **Persistencia:** `ComprobanteFiscalConfiguration.cs` en EF Core.
* **Testing:** Pruebas unitarias simulando respuestas CAE con `MockArcaClient`; pruebas de corte de conexión validando contingencia sin interrupción de la venta; tests de ViewModel.

---

## Prevención de Sobreescritura y Criterio de Aceptación
* **Prevención de Sobreescritura:** Lucas es el autor del POS, de los presupuestos y de la Facturación Fiscal, por lo que las conexiones del checkout con la conversión de presupuestos y con la emisión fiscal las realiza el mismo desarrollador sin riesgos de colisión.
* **Criterio de Aceptación Integrado:** Un presupuesto se guarda desde el POS aun con la caja cerrada y se entrega impreso o en PDF; dentro de término respeta estrictamente los precios cotizados; si se encuentra vencido, el sistema advierte al cajero mediante `AlertaPreciosPresupuestoDialog` mostrando las diferencias con el catálogo y actualiza los importes para cerrar la venta sin fricción; un presupuesto cobrado no puede volver a cobrarse; ante cortes de red o caídas del servicio fiscal, la venta concluye normalmente en contingencia, y el Gerente autoriza los comprobantes en lote con un clic desde su consola fiscal.
