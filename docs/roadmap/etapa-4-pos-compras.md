# Etapa 4: Épica 4 - Punto de Venta (POS) y Gestión de Compras

### Proyecto: Retail (Sistema ERP & Punto de Venta para Librería)
**Documento Maestro:** [`Roadmap de Implementacion.md`](file:///c:/Users/lucas/Proyectos/retail/docs/Roadmap%20de%20Implementacion.md) | **Mapa Semántico:** [`MAPA_DEL_PROYECTO.md`](file:///c:/Users/lucas/Proyectos/retail/docs/MAPA_DEL_PROYECTO.md)  
**Requisitos Vinculados:** [`RF-09`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L233), [`RF-10`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L234), [`RF-19`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L243), [`RNF-01`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L250), [`RNF-02`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L251)

---

> **Objetivo:** Construir la terminal operativa de mostrador de alta velocidad para ventas y cobros multimedio con descuento atómico de stock, y el módulo de ingreso de compras a distribuidores con recálculo automático de precios de venta.

## Módulos e Interfaces Asignados

### Módulo 4.1: Punto de Venta (POS) y Checkout Multimedio Completo
**Responsable:** Lucas Kruzolek

* **Interfaz Visual:** `PosView.xaml` (terminal de mostrador de alta velocidad, captura en buffer continuo de scanner de barras, atajos F1 a F12, foco permanente, grilla de líneas con cantidades y selección rápida de cliente) y `CobroModalDialog.xaml` (modal de pago multimedio: efectivo con cálculo dinámico de vuelto, tarjetas, QR y cuenta corriente con verificación de límite de crédito; confirmación e impresión).
* **ViewModels:** `PosViewModel.cs` y `CobroModalViewModel.cs`.
* **Lógica y Casos de Uso:** `IVentaService` (`BuscarArticuloParaVentaAsync` con lecturas ultrarrápidas $< 15\text{ ms}$ sin tracking, y `RegistrarVentaAsync` con transacción ACID que guarda venta, ítems, pagos y descuenta stock atómicamente).
* **Dominio:** Agregado `Venta` (`IAggregateRoot`), `DetalleVenta`, `PagoVenta`, invariantes de total y método de descuento `articulo.DescontarStock(cantidad)`.
* **Persistencia:** `VentaConfiguration.cs`, `DetalleVentaConfiguration.cs` y `PagoVentaConfiguration.cs` en EF Core. Despacho a `ITicketPrinterService`.
* **Testing:** Pruebas unitarias de cálculo de vuelto; pruebas de concurrencia y rollback por falta de stock; pruebas de interfaz de `PosViewModel`.

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
