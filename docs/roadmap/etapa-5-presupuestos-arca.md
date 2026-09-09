# Etapa 5: Épica 5 - Presupuestador Independiente y Facturación Fiscal ARCA

### Proyecto: Retail (Sistema ERP & Punto de Venta para Librería)
**Documento Maestro:** [`Roadmap de Implementacion.md`](file:///c:/Users/lucas/Proyectos/retail/docs/Roadmap%20de%20Implementacion.md) | **Mapa Semántico:** [`MAPA_DEL_PROYECTO.md`](file:///c:/Users/lucas/Proyectos/retail/docs/MAPA_DEL_PROYECTO.md)  
**Requisitos Vinculados:** [`RF-11`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L235), [`RF-12`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L236), [`RF-16`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L240), [`RF-17`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L241), [`RF-18`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L242), [`RNF-07`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L256)

---

> **Objetivo:** Confeccionar cotizaciones comerciales independientes con validez de 15 días y conversión en mostrador con control de stock, y automatizar la emisión fiscal electrónica ante AFIP/ARCA con contingencia resiliente y consola gerencial.

## Módulos e Interfaces Asignados

### Módulo 5.1: Presupuestador Independiente y Conversión a Venta
**Responsable:** Pablo Fernandez

* **Interfaz Visual:** `PresupuestosView.xaml` (confección, listado e impresión de cotizaciones independientes) y `AlertaPreciosPresupuestoDialog.xaml` (modal de alerta que advierte al cajero si los precios del catálogo aumentaron o si falta stock en góndola al intentar convertir un presupuesto en mostrador).
* **ViewModels:** `PresupuestosViewModel.cs` y `AlertaPreciosPresupuestoViewModel.cs`.
* **Lógica y Casos de Uso:** `IPresupuestoService`:
  - `CrearPresupuestoAsync`: cotización temporal sin afectar stock ni caja, con validez configurable (15 días por defecto, editable).
  - `RecuperarPresupuestoParaVentaAsync`: valida vigencia temporal y disponibilidad de stock. Si está vigente, respeta los precios pactados congelados; si expiró, no bloquea destructivamente sino que detecta discrepancias de catálogo y habilita la conciliación adaptativa vía `AlertaPreciosPresupuestoDialog`.
  - `MarcarPresupuestoComoConvertidoAsync` al confirmarse la venta.
* **Dominio:** Agregado `Presupuesto` (`IAggregateRoot`), `DetallePresupuesto`, `EstadoPresupuesto`, propiedad calculada `EstaVencido` e invariante de congelamiento de `precio_unitario_pactado` durante la vigencia.
* **Persistencia:** `PresupuestoConfiguration.cs` y `DetallePresupuestoConfiguration.cs`.
* **Testing:** Pruebas unitarias de cálculo de vigencia y expiración; pruebas de conciliación de precios en `AlertaPreciosPresupuestoDialog`; pruebas de bloqueo por stock insuficiente en conversión y tests de `PresupuestosViewModel`.

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
* **Prevención de Sobreescritura:** Lucas es el autor del POS y de la Facturación Fiscal, por lo que la conexión entre el checkout y la emisión fiscal la realiza el mismo desarrollador sin riesgos de colisión. Pablo entrega el módulo de presupuestos completo con su propio modal de alerta de discrepancias `AlertaPreciosPresupuestoDialog.xaml`, listo para ser consumido mediante la interfaz `IPresupuestoService`.
* **Criterio de Aceptación Integrado:** Un presupuesto dentro de término respeta estrictamente los precios cotizados; si se encuentra vencido, el sistema advierte al cajero mediante `AlertaPreciosPresupuestoDialog` mostrando los aumentos de catálogo y permitiendo actualizar importes para cerrar la venta sin fricción; ante cortes de red o caídas del servicio fiscal, la venta concluye normalmente en contingencia, y el Gerente autoriza los comprobantes en lote con un clic desde su consola fiscal.
