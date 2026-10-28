# Track 4.3 — Paquete de Trabajo de Pablo: Compras (Replanteo del Módulo 4.2)

### Proyecto: Retail (Sistema ERP & Punto de Venta para Librería)
**Documento rector:** [`etapa-4-track-4.3-abastecimiento.md`](file:///c:/Users/lucas/Proyectos/retail/docs/roadmap/etapa-4-track-4.3-abastecimiento.md) | **Responsable:** Pablo Fernandez  
**Rama existente:** `feat/modulo-4.2-compras` (último commit `a1d7be9`, 2026-10-26)  
**Destinatarios:** el agente de código de Pablo. Las decisiones `A-n`, los contratos (§5) y las reglas de integración (§7) son del documento rector.

> **Antes de empezar cualquier fase, el agente:**
> 1. Lee el documento rector completo, en especial §3 (decisiones), §5 (contratos) y §6 (propiedad de archivos).
> 2. Lee `AGENTS.md` y `CLAUDE.md`: plan antes de editar, capa por capa, nada de commits, ramas, rebase ni PR sin pedido explícito de Pablo.
> 3. Verifica que los PR de los que depende la fase estén mergeados en `main`. Si no lo están, **no empieza**.
>
> **Detenerse y reportar si:** hace falta cambiar algo de `Pedido`, `IPedidoService` o sus DTOs (son de Lucas, §6 del rector), o si un contrato de §5 no alcanza para implementar la fase.

---

## Contexto: qué cambia y qué no en el Módulo 4.2

El trabajo de `a1d7be9` **se conserva**: el agregado `Compra` encapsulado, la fábrica `Registrar`, `AgregarDetalle` con redondeo, `CompraService` con la propagación a presentaciones (RF-19, RF-21), el validador y los tests. El pedido del docente **se agrega encima**; no reemplaza lo hecho.

| Tema | Antes | Después |
| :--- | :--- | :--- |
| Origen de la compra | Siempre se carga a mano | A mano **o** importada desde un pedido `Emitido` del mismo proveedor (A-9) |
| Recepción | No existía | **La compra es la recepción**: no hay diálogo de recepción separado (A-9) |
| Pedido | No existía | Al registrar, el pedido de origen pasa a `Recibido` en el mismo `SaveChanges` (A-10) |
| Costo del artículo | RF-19 | **Sin cambios** (A-4), aunque el proveedor no sea la fuente principal del artículo |

---

## Fase P1 — Poner la rama al día y aplicar las correcciones de revisión

**Depende de:** PR-0. **Migración:** sí (índice único de comprobante).  
**Objetivo:** dejar el 4.2 mergeable en `main`, **sin nada de pedidos todavía**.

### P1.1. Integrar `main`

La rama está **28 commits atrás** de `origin/main`. Hay que integrar con **merge** de `origin/main` (no rebase: la rama está publicada y reescribirla exige force push, que está prohibido sin pedido explícito). Después:
* Resolver conflictos en `MAPA_DEL_PROYECTO.md`, `AppSmokeTests.cs` y `ServidorDePruebas.cs` conservando **los dos lados**.
* Compilar y correr los tests **antes** de seguir con P1.2.

### P1.2. Correcciones de revisión (de la lectura de `a1d7be9`)

| Id | Hallazgo | Corrección esperada | Test |
| :--- | :--- | :--- | :--- |
| **C-1** | Un mismo comprobante (proveedor + tipo + número) se puede registrar dos veces, y eso duplica el stock | Índice único filtrado `(id_proveedor, tipo_comprobante, numero_comprobante) WHERE deleted_at IS NULL` en `CompraConfiguration` y migración. En el servicio, verificar antes y lanzar una `DomainException` con un mensaje claro (el índice es la defensa en profundidad) | `RegistrarCompraAsync_ComprobanteYaRegistrado_LanzaExcepcion` (unitario) y la violación del índice (integración) |
| **C-2** | `AgregarDetalle` acepta dos renglones del mismo artículo. Si tienen costos distintos, el costo final depende del orden | Rechazar renglones repetidos en `CrearCompraValidator` (mensaje para el usuario) y en `Compra.AgregarDetalle` (invariante del agregado) | `AgregarDetalle_ArticuloRepetido_LanzaExcepcion`, `Validar_ArticulosRepetidos_FallaConMensaje` |
| **C-3** | Dentro del bucle de renglones, `articulos.FirstOrDefault(...)` / `First(...)` recorren la lista por cada renglón (O(n·m), prohibido por `AGENTS.md`) | Armar un `Dictionary<int, Articulo>` una sola vez, como ya se hace con `presentacionesPorOrigen` | Los existentes alcanzan |
| **C-4** | Después del `SaveChanges` exitoso, el contexto sigue siguiendo las entidades. Una segunda compra desde la misma pantalla puede leer un `row_version` viejo y chocar con un conflicto falso | Aplicar el patrón de `RegistrarVentaAsync` (D-15): `_unitOfWork.DescartarCambios()` después de guardar. Si el guardado falla, la unidad de trabajo descarta los cambios | Test que verifique la llamada a `DescartarCambios` |
| **C-5** | En `ComprasViewModel.RegistrarCompraAsync`, las `DomainException` (reglas de negocio) y los `DbUpdateConcurrencyException` (otra terminal modificó el mismo artículo, D-12) caen en el `catch (Exception)` genérico: se registran en el log como error inesperado y el mensaje expone el texto técnico. Además, el registro usa `Task.Run` directo en lugar de `CargaSerializada` (H-19, ya en `main`) | Seguir el patrón de `PosViewModel`: las reglas de negocio y los conflictos se muestran como alertas y conservan la grilla para reintentar; solo los errores inesperados van al log. El acceso a datos pasa por `CargaSerializada` | Tests de ViewModel: regla de negocio, conflicto y error inesperado |

### P1.3. Cierre de P1

Bucle de verificación completo de `AGENTS.md`, mapa actualizado y estado de P1 en §2 del rector. Pablo decide el commit y el PR (revisión cruzada de Lucas).

---

## Fase P2 — Compra desde un pedido

**Depende de:** P1 **y** L1 mergeados en `main`. **Migración:** sí (`COMPRAS.id_pedido_origen`).  
**Objetivo:** que Compras importe un pedido emitido, permita ajustarlo a la factura y cierre el pedido en la misma transacción.

### P2.1. Dominio

* `Compra`: propiedad `int? IdPedidoOrigen { get; private set; }` y parámetro opcional `int? idPedidoOrigen = null` en `Registrar` (debe ser > 0 si tiene valor). Navegación opcional a `Pedido` (el patrón es el de `Venta.IdPresupuestoOrigen`).
* Tests: `Registrar_ConPedidoOrigen_LoConserva`, `Registrar_PedidoOrigenCero_LanzaExcepcion`.

### P2.2. Aplicación

* `CrearCompraDto.IdPedidoOrigen` (`int?`), exactamente como en §5.3 del rector. El validador lo acepta nulo o > 0.
* `CompraService.RegistrarCompraAsync` implementa la secuencia de §5.3 del rector:
  1. Si hay `IdPedidoOrigen`, cargar el pedido con `IRepository<Pedido>`. Si no existe, lanzar `DomainException`.
  2. Si `pedido.IdProveedor != dto.IdProveedor`, lanzar `DomainException`.
  3. Invocar `pedido.MarcarRecibido()` **antes** de modificar artículos. Si el pedido no está `Emitido`, el dominio lanza `TransicionPedidoInvalidaException`.
  4. Seguir con el flujo actual (stock, costos y presentaciones). **No** comparar los renglones de la compra con los del pedido (A-9).
  5. Un único `SaveChangesAsync`, y después `DescartarCambios()` (C-4).
* `DbUpdateConcurrencyException` sobre el pedido (A-13) y violación del índice único de `id_pedido_origen` (A-11): ambos llegan al ViewModel como el mensaje "El pedido ya fue recibido o modificado en otra terminal".
* **Tests** en `CompraServiceTests`: el pedido no existe, es de otro proveedor, no está emitido (no se modifica ningún artículo), camino feliz (el pedido queda `Recibido` y la compra con `IdPedidoOrigen`) y compra con renglones distintos de los del pedido (se acepta).

### P2.3. Infraestructura

* `CompraConfiguration`: columna `id_pedido_origen`, FK nullable a `PEDIDOS` con `Restrict` e índice único filtrado `(id_pedido_origen) WHERE id_pedido_origen IS NOT NULL AND deleted_at IS NULL`.
* Migración `AgregarPedidoOrigenACompras`, respetando la regla de §7 del rector (si `main` avanzó, se regenera).
* **Test de integración:** compra desde un pedido, que verifica en la base el stock, el costo, `id_pedido_origen` y el estado `Recibido`, todo o nada. Además, un test con dos contextos que registran compras del mismo pedido: la segunda falla y el stock no se duplica.

### P2.4. UI (`ComprasView`)

**Lectura obligatoria:** `docs/SISTEMA_DE_DISENO.md` y `StyleGalleryView.xaml` (Ley 9).

* Botón **"Importar pedido"**, habilitado cuando hay un proveedor seleccionado y la grilla está vacía (si no está vacía, pedir confirmación para reemplazarla).
* Diálogo `SeleccionarPedidoModalDialog` (dueño Pablo) que lista `IPedidoService.ListarEmitidosPorProveedorAsync(idProveedor)`: fecha, cantidad de renglones y total estimado.
* Al elegir un pedido: `IPedidoService.ObtenerParaCompraAsync(idPedido)`. Si devuelve `null`, avisar que el pedido ya no está disponible. Si no, **precargar la grilla** con cada renglón: artículo, cantidad y `CostoPactado` como costo unitario inicial. La cabecera muestra "Pedido #N" con una acción para **desvincular** el pedido (la compra vuelve a ser manual).
* El usuario ajusta la grilla a la factura con los controles que ya existen: quita, agrega, cambia cantidades y costos.
* Al registrar, enviar `IdPedidoOrigen`. Nada de navegación hacia `PedidosView` (A-12).
* Todo acceso a datos por `CargaSerializada` + `Task.Run` (Ley 4). Correr `scripts/audit-xaml.ps1`.
* **Tests:** `ComprasViewModelTests` (importar, reemplazar con confirmación, pedido no disponible, desvincular, el DTO enviado lleva `IdPedidoOrigen`), `SeleccionarPedidoModalViewModelTests` y el humo STA del diálogo en `AppSmokeTests.cs`.

### P2.5. Cierre de P2

* Bucle de verificación completo.
* Actualizar: el mapa (diálogo, ViewModel y cambios de `Compra`), el DER (`COMPRAS.id_pedido_origen` si difiere del rector), el estado de P2 en §2 del rector y el Módulo 4.2 en [`etapa-4-pos-compras.md`](file:///c:/Users/lucas/Proyectos/retail/docs/roadmap/etapa-4-pos-compras.md).
* Avisar a Lucas: con P2 mergeado se desbloquea L2b ("Volver a pedir faltantes").

---

## Archivos que este paquete **no** modifica

`Pedido.cs`, `DetallePedido.cs`, `EstadoPedidoEnum.cs`, `IPedidoService.cs`, `PedidoService.cs`, `DTOs/Pedidos/`, configuraciones de pedidos, `PedidosView`, `CatalogoProveedor.cs`, el importador y su vista, y la navegación del Sidebar. Si algo de esto parece necesario, **detenerse y reportar**.

## Formato del Reporte del Agente al Terminar Cada Fase

1. Qué se hizo, capa por capa, con los archivos tocados.
2. Resultado del bucle de verificación (comandos y salida resumida).
3. Desvíos respecto de este paquete o del rector, y por qué (debería ser "ninguno").
4. Preguntas de defensa que el docente podría hacer sobre la fase.
5. Propuesta de commit (mensaje), **sin ejecutarlo**.
