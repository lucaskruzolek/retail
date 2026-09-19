# Etapa 4: Épica 4 - Punto de Venta (POS) y Gestión de Compras

### Proyecto: Retail (Sistema ERP & Punto de Venta para Librería)
**Documento Maestro:** [`Roadmap de Implementacion.md`](file:///c:/Users/lucas/Proyectos/retail/docs/Roadmap%20de%20Implementacion.md) | **Mapa Semántico:** [`MAPA_DEL_PROYECTO.md`](file:///c:/Users/lucas/Proyectos/retail/docs/MAPA_DEL_PROYECTO.md)  
**Requisitos Vinculados:** [`RF-09`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L233), [`RF-10`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L234), [`RF-19`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L243), [`RNF-01`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L250), [`RNF-02`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L251)

---

> **Objetivo:** Construir la terminal operativa de mostrador de alta velocidad para ventas y cobros multimedio con descuento atómico de stock, y el módulo de ingreso de compras a distribuidores con recálculo automático de precios de venta.

## Módulos e Interfaces Asignados

### Módulo 4.1: Punto de Venta (POS) y Checkout Multimedio Completo
**Responsable:** Lucas Kruzolek  
**Estado:** 🟡 En Curso (Fase UI & MVVM Mostrador: ✅ 100% | Fase Persistencia Transaccional ACID: ⏳ Siguiente sesión)

* **Interfaz Visual:**
  - [`PosView.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Pages/PosView.xaml): Terminal de mostrador ágil (Enfoque B Full-Width con grilla ticket 70% y panel resumen 30%), searchbar unificada (letras: búsqueda texto, números: escaneo código de barras), popup predictivo de artículos con precio y stock, visor de totales en `Cascadia Code` a 32px y atajos globales F1 a F12.
  - [`CobroModalDialog.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Dialogs/CobroModalDialog.xaml): Modal de pago multimedio (`[F12] Cobrar`) con selector de medios (Efectivo, Tarjeta Débito/Crédito, QR, Cuenta Corriente), botones rápidos de billetes (+$1.000, +$2.000, +$5.000, +$10.000, +$20.000), cálculo reactivo de vuelto, verificación de límite de crédito para cuenta corriente e impresión de ticket.
  - [`SeleccionarClienteModalDialog.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Dialogs/SeleccionarClienteModalDialog.xaml): Modal de selección rápida de cliente (`[F4]`) con búsqueda instantánea en servidor y confirmación al ticket.
* **ViewModels:** [`PosViewModel.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/ViewModels/Ventas/PosViewModel.cs), [`ItemVentaPosViewModel.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/ViewModels/Ventas/ItemVentaPosViewModel.cs), [`CobroModalViewModel.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/ViewModels/Ventas/CobroModalViewModel.cs) y [`SeleccionarClienteModalViewModel.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/ViewModels/Ventas/SeleccionarClienteModalViewModel.cs).
* **Lógica y Casos de Uso:** [`IVentaService`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Interfaces/Services/IVentaService.cs) y [`VentaService`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Services/VentaService.cs) (`BuscarPorCodigoBarrasAsync` y `BuscarPorTextoAsync` con delegación al motor SQL mediante `.AsNoTracking()`, push-down de búsqueda y simulación de ticket a `ITicketPrinterService`).
* **Testing:**
  - Pruebas unitarias de mostrador en [`PosViewModelTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.App.UnitTests/ViewModels/PosViewModelTests.cs) (cobertura completa de atajos F1-F12, búsqueda reactiva, commit Enter y totales).
  - Pruebas unitarias de cobro en [`CobroModalViewModelTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.App.UnitTests/ViewModels/CobroModalViewModelTests.cs) (cálculo dinámico de vuelto, billetes rápidos, límite de crédito).
  - Pruebas de navegación en [`MainViewModelTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.App.UnitTests/ViewModels/MainViewModelTests.cs).
  - Pruebas de humo en hilo STA con validación de árbol visual BAML en [`AppSmokeTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.App.UnitTests/AppSmokeTests.cs) (`PosView`, `CobroModalDialog`, `SeleccionarClienteModalDialog`).
* **Siguiente Fase (Persistencia Transaccional):** Mapeo EF Core de agregados `Venta`, `DetalleVenta`, `PagoVenta`, `ComprobanteFiscal` y método transaccional ACID `RegistrarVentaAsync` con descuento atómico de stock.

---

### Módulo 4.2: Compras a Proveedores y Recálculo Automático de Precios
**Responsable:** Pablo Fernandez

* **Interfaz Visual:** `ComprasView.xaml` (ingreso de facturas/remitos de distribuidores, selección de proveedor, grilla interactiva con previsualización en tiempo real del nuevo precio de venta sugerido al ingresar el nuevo costo de reposición unitario).
* **ViewModels:** `ComprasViewModel.cs` y `DetalleCompraItemViewModel.cs`.
* **Lógica y Casos de Uso:** `ICompraService.RegistrarCompraAsync`:
  - Transacción ACID de ingreso de mercadería.
  - Incremento del stock físico de los artículos adquiridos.
  - Ejecución inmediata del recálculo de precios de venta en catálogo.
* **Dominio:** Agregado `Compra` (`IAggregateRoot`), `DetalleCompra`. Regla de negocio central `RF-19`: `Articulo.ActualizarCostoYRecalcularPrecio(nuevoCosto)` con fórmula de markup.
* **Persistencia:** `CompraConfiguration.cs` y `DetalleCompraConfiguration.cs` en EF Core.
* **Testing:** Pruebas unitarias de recálculo de markup; pruebas de integración de compras validando nuevo stock y nuevo precio en una sola transacción; tests de `ComprasView`.

---

## Prevención de Sobreescritura y Criterio de Aceptación
* **Prevención de Sobreescritura:** Lucas es el dueño exclusivo del POS y el modal de cobro. Pablo es el dueño exclusivo de la pantalla de Compras. En la entidad compartida `Articulo`, Lucas solo invoca `DescontarStock` y Pablo invoca `IncrementarStock` y `ActualizarCostoYRecalcularPrecio`, manteniendo los métodos estrictamente encapsulados.
* **Criterio de Aceptación Integrado:** El POS permite vender ágilmente con teclado y lector de barras en $< 50\text{ ms}$, descontando stock e imprimiendo el ticket; al ingresar una compra de distribuidor, el stock se incrementa y el catálogo recalcula inmediatamente el precio de venta sugerido protegiendo el margen comercial.
