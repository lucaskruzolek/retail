# Etapa 3: Épica 3 - Turnos de Caja, Clientes y Cobranza de Cuentas Corrientes

### Proyecto: Retail (Sistema ERP & Punto de Venta para Librería)
**Documento Maestro:** [`Roadmap de Implementacion.md`](file:///c:/Users/lucas/Proyectos/retail/docs/Roadmap%20de%20Implementacion.md) | **Mapa Semántico:** [`MAPA_DEL_PROYECTO.md`](file:///c:/Users/lucas/Proyectos/retail/docs/MAPA_DEL_PROYECTO.md)  
**Requisitos Vinculados:** [`RF-13`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L237), [`RF-14`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L238), [`RF-15`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L239), [`RF-20`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L244), [`RNF-08`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L257)

---

> **Objetivo:** Controlar los flujos de dinero físico en mostrador mediante balance teórico y arqueo ciego, y gestionar el padrón de clientes con cobranzas multimedio de deudas que impacten atómicamente en la caja activa.

## Módulos e Interfaces Asignados

### Módulo 3.1: Caja y Tesorería con Arqueo Ciego
**Responsable:** Pablo Fernandez

* **Interfaz Visual:** `CajaView.xaml` (panel de control de turno, saldo inicial, grilla de movimientos varios) y `ArqueoCiegoDialog.xaml` (modal de cierre donde el cajero declara únicamente el dinero físico contado; el sistema emite el acta comparativa de sobrante/faltante y exhibe el total electrónico para conciliar con el POS adquirente).
* **ViewModels:** `CajaViewModel.cs` y `ArqueoCiegoViewModel.cs`.
* **Lógica y Casos de Uso:** `ICajaService` (`AbrirTurnoAsync`, `RegistrarMovimientoAsync`, `ObtenerTurnoActivoAsync`, `CerrarTurnoConArqueoCiegoAsync`).
* **Dominio:** Agregado `TurnoCaja` (`IAggregateRoot`), `MovimientoCaja`, balance teórico:
  $$\text{SaldoTeorico} = \text{Inicial} + \text{VentasEfectivo} + \text{CobranzasEfectivo} + \text{Ingresos} - \text{Egresos}$$
  Cálculo de arqueo ciego: $\text{Diferencia} = \text{SaldoDeclarado} - \text{SaldoTeorico}$.
* **Persistencia:** `TurnoCajaConfiguration.cs` y `MovimientoCajaConfiguration.cs`.
* **Testing:** Pruebas unitarias de fórmulas de arqueo; pruebas de servicio de turno y pruebas de interfaz de `ArqueoCiegoDialog`.

---

### Módulo 3.2: Clientes y Cobranzas de Cuentas Corrientes
**Responsable:** Lucas Kruzolek  
**Estado:** 🟢 Completado al 100% (Fase 1: ✅ 100% | Fase 2: ✅ 100%)

#### Fase 1: Padrón de Clientes y Cuentas Corrientes (✅ Completada)
* **Dominio DDD:** Agregado [`Cliente.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Domain/Entities/Cliente.cs) (`IAggregateRoot`) enriquecido con métodos de negocio: `ActualizarDatos`, `HabilitarCuentaCorriente`, `DeshabilitarCuentaCorriente`, `ModificarLimiteCredito`, `DebitarCuentaCorriente`, `AcreditarCobranza`, `CreditoDisponible` y override de `MarkAsDeleted()` con salvaguarda contable ante deuda pendiente.
* **Persistencia:** Configuración en [`ClienteConfiguration.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Infrastructure/Persistence/Configurations/ClienteConfiguration.cs) con índice único filtrado `[deleted_at] IS NULL` para `numero_documento` (Migración EF Core: `AddFilteredIndexToClientesNumeroDocumento`).
* **Aplicación:** Casos de uso en [`ClienteService.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Services/ClienteService.cs) con push-down a SQL Server para búsqueda multicriterio (`EF.Functions.Like`), alta, modificación y débito en cuenta corriente; validaciones declarativas en [`CrearClienteValidator.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Validators/Clientes/CrearClienteValidator.cs) y [`ActualizarClienteValidator.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Validators/Clientes/ActualizarClienteValidator.cs).
* **Presentación (WPF / MVVM):** Vista [`ClientesView.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Pages/ClientesView.xaml) con búsqueda reactiva, atajos de mostrador F2/F3/F5, indicadores de cuenta corriente y badges de saldo; diálogo modal [`ClienteFormDialog.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Dialogs/ClienteFormDialog.xaml), ViewModels [`ClientesViewModel.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/ViewModels/Clientes/ClientesViewModel.cs) y [`ClienteFormViewModel.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/ViewModels/Clientes/ClienteFormViewModel.cs), servicio desacoplado [`ClienteDialogService.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Services/ClienteDialogService.cs) y arnés sandbox en [`StyleGalleryView.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Dev/StyleGalleryView.xaml).
* **Testing:** 19 pruebas de dominio en `ClienteTests.cs`, validadores y servicios en `ClienteValidatorTests.cs` y `ClienteServiceTests.cs`, integración de índice filtrado en `RetailDbContextTests.cs`, y pruebas STA de ViewModels en `ClientesViewModelTests.cs` y `ClienteFormViewModelTests.cs`.

#### Fase 2: Cobranzas Multimedio e Integración Transaccional con Caja (✅ Completada)
* **Dominio DDD:** Método de raíz de agregado `Cliente.RegistrarCobranza(idTurno, idUsuario, monto, medioPago, referencia)` que amortiza el saldo y genera internamente la entidad hija [`CobranzaCliente`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Domain/Entities/CobranzaCliente.cs) cumpliendo las invariantes de negocio (`CobranzaExcedeDeudaException`).
* **Infraestructura:** Creación del doble de prueba [`FakeCajaService.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Infrastructure/ExternalServices/Mocks/FakeCajaService.cs) registrado mediante `TryAddScoped` para desacoplar el desarrollo de Lucas de las tareas de Pablo (Módulo 3.1); extensión de `Repository<T>` con `GetByIdWithIncludesAsync` para carga Eager del agregado sin romper Clean Architecture.
* **Aplicación:** Casos de uso `RegistrarCobranzaAsync` y `ListarHistorialCobranzasAsync` en [`ClienteService.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Services/ClienteService.cs) orquestando la transacción ACID con `IUnitOfWork`, imputación en caja activa vía `ICajaService.RegistrarIngresoCobranzaAsync` y emisión de recibo duplicado con `ITicketPrinterService` protegido ante fallos de hardware; validador [`RegistrarCobranzaValidator.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Validators/Clientes/RegistrarCobranzaValidator.cs).
* **Presentación (WPF / MVVM):** Diálogo modal de mostrador [`CobranzaModalDialog.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Dialogs/CobranzaModalDialog.xaml) con selector de medios de pago, cálculo reactivo de vuelto, botón "Pagar Totalidad" y ViewModel [`CobranzaModalViewModel.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/ViewModels/Clientes/CobranzaModalViewModel.cs); integración en [`ClientesView.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Pages/ClientesView.xaml) con atajo **F4**, botón en barra de herramientas y botón de cobranza contextual por fila en la grilla.
* **Testing:** Pruebas unitarias de invariantes en `ClienteTests.cs`, validaciones en `RegistrarCobranzaValidatorTests.cs`, orquestación con mocks en `ClienteServiceTests.cs`, prueba de integración en LocalDB `Cliente_AlRegistrarCobranza_PersisteEnTablaCobranzasClientesYActualizaSaldoDeudor` en `RetailDbContextTests.cs`, y pruebas completas de MVVM en `CobranzaModalViewModelTests.cs` y `ClientesViewModelTests.cs`. Suite de 283 tests superados al 100% en Release.

---

## Prevención de Sobreescritura y Criterio de Aceptación
* **Prevención de Sobreescritura:** Para imputar el cobro en caja, Lucas inyecta la interfaz `ICajaService` y ejecuta el método acordado en los contratos (`RegistrarIngresoCobranzaAsync` o `RegistrarMovimientoAsync`). Lucas **no modifica `CajaView`, `CajaViewModel` ni la implementación de `CajaService`**.
* **Criterio de Aceptación Integrado:** Al registrar una cobranza de cuenta corriente en efectivo, la deuda del cliente se reduce y el efectivo de la caja activa se incrementa en una misma transacción ACID; al cerrar el turno, el arqueo ciego detecta faltantes/sobrantes y exhibe el total electrónico para cotejo.
