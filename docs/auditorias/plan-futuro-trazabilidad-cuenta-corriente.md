# Plan futuro: Trazabilidad de la Cuenta Corriente

**Proyecto:** Retail (ERP y POS para librería)
**Fecha de registro:** 2026-11-19
**Estado:** 📋 Registrado, sin planificar. Surgió durante la auditoría de concurrencia multiterminal
(`auditoria-concurrencia-multiterminal.md`), pero queda fuera de su alcance porque no es un problema de concurrencia.
**Requisitos relacionados:** `RF-20` (clientes y cobranza en cuenta corriente), `RF-15` (arqueo de caja).

---

## 1. Pregunta que lo originó

> Una venta a cuenta corriente o una cobranza, ¿se pueden auditar? ¿Se sabe la fecha y quién la hizo?

**Respuesta corta:** los datos **se guardan**, pero **no se pueden ver**, y hay dos caminos que permiten **cambiar el
saldo sin dejar rastro**.

| Operación | Qué la registra | Fecha | Quién | Turno |
| :--- | :--- | :--- | :--- | :--- |
| Venta a cuenta corriente | `Venta` + `PagoVenta` con `MedioPago = CuentaCorriente` | `Venta.FechaHora` | `Venta.IdUsuario` | `Venta.IdTurno` |
| Cobranza de deuda | `CobranzaCliente` (hija de `Cliente`) | `FechaHora` | `IdUsuario` | `IdTurno` |

## 2. El concepto: el saldo es un dato derivado

Una cuenta corriente es un **libro mayor**: el saldo es el resultado de sumar los movimientos.

$$\text{SaldoCuentaCorriente} = \sum \text{ventas a crédito} - \sum \text{cobranzas}$$

`Cliente.SaldoCuentaCorriente` es una **caché** de ese cálculo (sirve para validar el límite de crédito sin sumar
todo en cada venta). Una caché solo es confiable si **toda** modificación del saldo pasa por un método que también
crea el movimiento. Esa es la invariante del agregado `Cliente`, y hoy tiene dos agujeros (T3 y T4).

## 3. Hallazgos

| # | Hallazgo | Evidencia (al 2026-11-19) | Consecuencia | Gravedad |
| :-- | :--- | :--- | :--- | :--- |
| T1 | No existe un estado de cuenta | `ClienteService.ListarHistorialCobranzasAsync` no tiene llamadores en la UI; las ventas a crédito no se listan en ningún lugar | No se puede responder "¿de dónde sale esta deuda?" | 🟠 |
| T2 | El historial de cobranzas no muestra quién cobró y se arma en memoria | La misma consulta deja `UsuarioNombre = null`, carga la entidad y ordena en C# (contra la Ley 8) | El `IdUsuario` está guardado pero no se muestra | 🟡 |
| T3 | Se puede debitar el saldo sin ningún comprobante | `ClienteService.DebitarCuentaCorrienteAsync`: aumenta la deuda y guarda, sin crear una venta ni un registro. Público en `IClienteService`, sin llamadores | El saldo puede cambiar sin fecha, usuario ni motivo | 🔴 |
| T4 | Se puede acreditar el saldo sin crear la cobranza | `Cliente.AcreditarCobranza` es `public`; solo debería usarse desde `RegistrarCobranza` | El dominio no obliga a dejar el rastro | 🟠 |
| T5 | El usuario de la cobranza lo decide la UI | `CobranzaModalViewModel`: `IdUsuario = turnoActivo.IdUsuario`, y el servicio confía en el DTO. Verificar si `VentaService` hace lo mismo | El dato de auditoría no lo garantiza el servicio | 🟡 |
| T6 | El cierre del turno no informa ventas a crédito ni cobranzas | Resumen de `CajaService` (solo efectivo y medios electrónicos) | Al cierre no se ve cuánto se vendió a crédito | 🟡 |

La fecha tomada del reloj de cada terminal ya está registrada en la auditoría de concurrencia (H10).

## 4. Opciones

| Opción | Qué implica | Ventaja | Desventaja |
| :--- | :--- | :--- | :--- |
| **A. Cerrar los agujeros y agregar el estado de cuenta como consulta** (recomendada) | Eliminar `DebitarCuentaCorrienteAsync` (YAGNI), hacer `private` a `AcreditarCobranza`, y agregar una consulta que una ventas a crédito y cobranzas, proyectada en SQL con el nombre del usuario | Sin cambios de esquema: los datos ya existen | El estado de cuenta se arma desde dos tablas |
| B. Tabla única de movimientos (`MovimientoCuentaCorriente`) | Un registro por débito o crédito, con referencia a la venta o la cobranza de origen | Estado de cuenta directo; lugar natural para notas de crédito y ajustes | Duplica datos que ya están en `PagoVenta` y `CobranzaCliente`; requiere migrar el historial |

En cualquiera de las dos: un **test de invariante** que, después de una serie de ventas a crédito y cobranzas,
compruebe que el saldo guardado coincide con la suma de los movimientos.

## 5. Decisiones pendientes

1. Opción A o B.
2. En qué etapa del Roadmap se incluye (candidata: Etapa 6, estabilización).
3. Si el estado de cuenta se imprime o se exporta (no figura en `RF-20`; habría que ampliar la ERS).
