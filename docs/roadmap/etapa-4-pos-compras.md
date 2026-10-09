# Etapa 4: Épica 4 - Punto de Venta (POS) y Gestión de Compras

### Proyecto: Retail (Sistema ERP & Punto de Venta para Librería)
**Documento Maestro:** [`Roadmap de Implementacion.md`](file:///c:/Users/lucas/Proyectos/retail/docs/Roadmap%20de%20Implementacion.md) | **Mapa Semántico:** [`MAPA_DEL_PROYECTO.md`](file:///c:/Users/lucas/Proyectos/retail/docs/MAPA_DEL_PROYECTO.md)  
**Requisitos Vinculados:** [`RF-09`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L233), [`RF-10`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L234), [`RF-19`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L243), [`RNF-01`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L250), [`RNF-02`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L251)

---

> **Objetivo:** Construir la terminal operativa de mostrador de alta velocidad para ventas y cobros multimedio con descuento atómico de stock, y el módulo de ingreso de compras a distribuidores con recálculo automático de precios de venta.

## Módulos e Interfaces Asignados

### Módulo 4.1: Punto de Venta (POS) y Checkout Multimedio Completo
**Responsable:** Lucas Kruzolek  
**Estado:** 🟢 Completado (Fase UI & MVVM Mostrador: ✅ 100% | Fase Persistencia Transaccional ACID: ✅ PR 4.1a | Integración del POS con la persistencia: ✅ PR 4.1b)

* **Interfaz Visual:**
  - [`PosView.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Pages/PosView.xaml): Terminal de mostrador ágil (Enfoque B Full-Width con grilla ticket 70% y panel resumen 30%), searchbar unificada (letras: búsqueda texto, números: escaneo código de barras), popup predictivo de artículos con precio y stock, visor de totales en `Cascadia Code` a 32px y atajos globales F1 a F12.
  - [`CobroModalDialog.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Dialogs/CobroModalDialog.xaml): Modal de pago multimedio (`[F12] Cobrar`) con selector de medios (Efectivo, Tarjeta Débito/Crédito, QR, Cuenta Corriente), botones rápidos de billetes (+$1.000, +$2.000, +$5.000, +$10.000, +$20.000), cálculo reactivo de vuelto, verificación de límite de crédito para cuenta corriente e impresión de ticket.
  - [`SeleccionarClienteModalDialog.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Dialogs/SeleccionarClienteModalDialog.xaml): Modal de selección rápida de cliente (`[F4]`) con búsqueda instantánea en servidor y confirmación al ticket.
* **ViewModels:** [`PosViewModel.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/ViewModels/Ventas/PosViewModel.cs), [`ItemVentaPosViewModel.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/ViewModels/Ventas/ItemVentaPosViewModel.cs), [`CobroModalViewModel.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/ViewModels/Ventas/CobroModalViewModel.cs) y [`SeleccionarClienteModalViewModel.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/ViewModels/Ventas/SeleccionarClienteModalViewModel.cs).
* **Lógica y Casos de Uso:** [`IVentaService`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Interfaces/Services/IVentaService.cs) y [`VentaService`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Services/VentaService.cs) (`BuscarPorCodigoBarrasAsync` y `BuscarPorTextoAsync` con proyección sin tracking, `VerificarTicketAsync`, `RegistrarVentaAsync` transaccional e impresión de ticket con `ITicketPrinterService`).
* **Testing:**
  - Pruebas unitarias de mostrador en [`PosViewModelTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.App.UnitTests/ViewModels/PosViewModelTests.cs) (cobertura completa de atajos F1-F12, búsqueda reactiva, commit Enter y totales).
  - Pruebas unitarias de cobro en [`CobroModalViewModelTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.App.UnitTests/ViewModels/CobroModalViewModelTests.cs) (cálculo dinámico de vuelto, billetes rápidos, límite de crédito).
  - Pruebas de navegación en [`MainViewModelTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.App.UnitTests/ViewModels/MainViewModelTests.cs).
  - Pruebas de humo en hilo STA con validación de árbol visual BAML en [`AppSmokeTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.App.UnitTests/AppSmokeTests.cs) (`PosView`, `CobroModalDialog`, `SeleccionarClienteModalDialog`).
* **Persistencia Transaccional (PR 4.1a, ✅):**
  - **Dominio:** agregado [`Venta`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Domain/Entities/Venta.cs) encapsulado (`Registrar`, `AgregarItem`, `AplicarDescuento`, `ImputarPago`, `ValidarCierre`); `DetalleVenta` y `PagoVenta` solo se crean desde la raíz. `Articulo.VerificarStockDisponible` permite validar sin descontar.
  - **Aplicación:** `RegistrarVentaAsync` real con [`CrearVentaValidator`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Validators/Ventas/CrearVentaValidator.cs): precio tomado del catálogo (D-11), descuento de stock (`RF-10`), débito de cuenta corriente con límite de crédito (`RF-09`) e imputación del turno de caja (la cuenta corriente no ingresa a la gaveta, `RF-15`), todo en un único `SaveChanges`. Valida todo antes de modificar cualquier agregado y descarta los cambios si el guardado falla.
  - **Persistencia:** `VENTAS.id_cliente` nullable (Consumidor Final, D-10) y `row_version` en `ARTICULOS` para concurrencia optimista entre terminales (D-12). Migración `VentaConsumidorFinalYConcurrenciaArticulos`. Detalle en la Ley 9 de [`SISTEMA_DE_PERSISTENCIA.md`](file:///c:/Users/lucas/Proyectos/retail/docs/SISTEMA_DE_PERSISTENCIA.md).
  - **Testing:** [`VentaTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.Domain.UnitTests/Entities/VentaTests.cs), [`VentaServiceTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.Application.UnitTests/Services/VentaServiceTests.cs), [`CrearVentaValidatorTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.Application.UnitTests/Validators/CrearVentaValidatorTests.cs) y [`RegistrarVentaIntegrationTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.Infrastructure.IntegrationTests/Services/RegistrarVentaIntegrationTests.cs) (LocalDB: persistencia, atomicidad y conflicto entre dos terminales con reintento).
* **Integración del POS con la persistencia (PR 4.1b, ✅):**
  - **Verificación previa al cobro:** `IVentaService.VerificarTicketAsync` informa precios cambiados, faltantes de stock y artículos dados de baja en una consulta sin tracking. El POS actualiza los precios del ticket y avisa el nuevo total (D-14), frena si algún faltante no tiene solución y, si faltan unidades sueltas de una presentación, **ofrece** abrir el mínimo de orígenes con una confirmación (`RF-10`, `RF-21`, D-13). `ServicioFraccionamiento.CalcularOrigenesNecesarios` hace el cálculo. Es una ayuda: `RegistrarVentaAsync` vuelve a validar todo.
  - **`PosViewModel`:** sin valores por defecto `?? 1` para turno y usuario; las reglas de negocio y los conflictos de concurrencia se muestran como alertas y conservan el ticket; solo los errores inesperados se registran en el log. Todos los accesos a datos del cobro pasan por `CargaSerializada` con `Task.Run` (Ley 4, H-19).
  - **Persistencia:** búsquedas del mostrador proyectadas con `AsNoTracking` y `Take` en SQL (Ley 8). El contexto se libera después de cada venta, también la exitosa (D-15), y el fraccionamiento descarta sus cambios si falla.
  - **Testing:** `VerificarTicketAsync` en [`VentaServiceTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.Application.UnitTests/Services/VentaServiceTests.cs), consultas nuevas en [`ArticuloQueryServiceTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.Infrastructure.IntegrationTests/Services/ArticuloQueryServiceTests.cs) (LocalDB) y cada rama del flujo de cobro en [`PosViewModelTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.App.UnitTests/ViewModels/PosViewModelTests.cs).
* **Pendiente para la Etapa 5:** conversión de presupuestos a venta (`RF-12`) y comprobante fiscal (`RF-16`, `RF-17`).

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
