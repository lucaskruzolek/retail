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

* **Interfaz Visual:** `ClientesView.xaml` (padrón con búsqueda por CUIT/DNI, condición IVA, límite de crédito y saldo deudor) y `CobranzaModalDialog.xaml` (modal multimedio para cancelar deuda con efectivo, tarjeta o QR, con emisión de recibo duplicado).
* **ViewModels:** `ClientesViewModel.cs` y `CobranzaModalViewModel.cs`.
* **Lógica y Casos de Uso:** `IClienteService` (ABM de clientes, consulta de saldo) y `RegistrarCobranzaAsync` bajo transacción ACID:
  - Reduce el saldo deudor del cliente (`Cliente.SaldoCuentaCorriente`).
  - Invoca a `ICajaService` para ingresar los fondos a la caja del turno activo.
  - Emite recibo duplicado no fiscal mediante `ITicketPrinterService`.
* **Dominio:** Agregado `Cliente` (`IAggregateRoot`), `CobranzaCliente`, límite de crédito.
* **Persistencia:** `ClienteConfiguration.cs` y `CobranzaClienteConfiguration.cs`.
* **Testing:** Pruebas de integración transaccional de cobranza en LocalDB; pruebas unitarias de validación de deudas y límites en `ClienteServiceTests.cs`.

---

## Prevención de Sobreescritura y Criterio de Aceptación
* **Prevención de Sobreescritura:** Para imputar el cobro en caja, Lucas inyecta la interfaz `ICajaService` y ejecuta el método acordado en los contratos (`RegistrarIngresoCobranzaAsync`). Lucas **no modifica `CajaView`, `CajaViewModel` ni la implementación de `CajaService`**.
* **Criterio de Aceptación Integrado:** Al registrar una cobranza de cuenta corriente en efectivo, la deuda del cliente se reduce y el efectivo de la caja activa se incrementa en una misma transacción ACID; al cerrar el turno, el arqueo ciego detecta faltantes/sobrantes y exhibe el total electrónico para cotejo.
