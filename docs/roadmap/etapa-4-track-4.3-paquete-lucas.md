# Track 4.3 — Paquete de Trabajo de Lucas: Pedidos, Fuentes Múltiples y Comparador

### Proyecto: Retail (Sistema ERP & Punto de Venta para Librería)
**Documento rector:** [`etapa-4-track-4.3-abastecimiento.md`](file:///c:/Users/lucas/Proyectos/retail/docs/roadmap/etapa-4-track-4.3-abastecimiento.md) | **Responsable:** Lucas Kruzolek  
**Destinatarios:** agentes de código que trabajan para Lucas. Las decisiones `A-n`, los contratos (§5) y las reglas de integración (§7) son del documento rector.

> **Antes de empezar cualquier fase, el agente:**
> 1. Lee el documento rector completo (es corto a propósito).
> 2. Lee `AGENTS.md` y `CLAUDE.md`: modo pedagógico, plan antes de editar, capa por capa, nada de Git sin pedido.
> 3. Verifica que los PR de los que depende la fase estén mergeados en `main` (tabla de §2 del rector). Si no lo están, **no empieza**.
> 4. Lee solo los manuales de la capa que va a tocar (Nivel 3 de `AGENTS.md`).
>
> **Detenerse y reportar si:** hace falta cambiar un contrato de §5 del rector, modificar un archivo cuyo dueño es Pablo (§6), o una decisión cerrada parece incorrecta.

---

## Fase L1 — Agregado `Pedido` (Dominio → Aplicación → Infraestructura)

**Depende de:** PR-0. **Rama sugerida:** `feat/pedidos-dominio`. **Migración:** sí (`AgregarPedidos`).  
**Objetivo:** implementar el pedido completo **sin UI**, incluidos los dos métodos de frontera que consume Compras.

### L1.1. Dominio (`Retail.Domain`)

| Archivo | Contenido |
| :--- | :--- |
| `Enums/EstadoPedidoEnum.cs` | Exactamente como en §5.1 del rector |
| `Exceptions/TransicionPedidoInvalidaException.cs` | Hereda de `DomainException`. El mensaje indica el estado actual y la operación intentada. Precedente: `TransicionBackupInvalidaException` |
| `Entities/Pedido.cs` | Raíz de agregado (`BaseEntity, IAggregateRoot`), setters privados, constructor privado para EF y colección encapsulada `_detalles` |
| `Entities/DetallePedido.cs` | Entidad interna: solo se crea desde `Pedido` (constructor `internal`) |

API de `Pedido`:

| Miembro | Regla |
| :--- | :--- |
| `static Pedido CrearBorrador(int idProveedor, int idUsuario)` | Ids > 0. Estado `Borrador`. La fecha de creación es `BaseEntity.CreatedAt` (D2) |
| `void AgregarRenglon(int idArticulo, int idCatalogoProveedor, string codigoProveedor, string descripcion, int cantidad, decimal costoPactado)` | Solo en `Borrador`. Cantidad ≥ 1, costo > 0, textos no vacíos. Si el ítem de catálogo ya está en el pedido, **suma la cantidad** y actualiza la foto (código, descripción y costo) con los valores recibidos. Si ese renglón tiene otro artículo (el ítem se revinculó), `DomainException` |
| `void CambiarCantidad(int idCatalogoProveedor, int cantidad)` | Solo en `Borrador`. Cantidad ≥ 1. Si el renglón no existe, `DomainException` |
| `void QuitarRenglon(int idCatalogoProveedor)` | Solo en `Borrador`. El renglón sale de la colección y la persistencia lo da de baja lógica (D4). Puede dejar el borrador vacío (no se cancela solo) |
| `void Emitir()` | `Borrador → Emitido`. Exige al menos un renglón. Registra `FechaEmision` |
| `void MarcarRecibido()` | `Emitido → Recibido`. Registra `FechaCierre`. **Contrato de frontera (§5.1)** |
| `void Cancelar(string motivo)` | `Borrador \| Emitido → Cancelado`. Motivo obligatorio (normalizado con `ReglasTexto`). Registra `FechaCierre` |
| `decimal TotalEstimado` | Calculado: Σ `cantidad × costoPactado`, redondeado con `ReglasMonetarias`. No se persiste |

**Decisiones de L1.1** (aprobadas por el estudiante el 2026-10-28; no tocan la frontera de §5):

| # | Decisión | Razón |
| :--- | :--- | :--- |
| D1 | `row_version` es una **propiedad sombra** configurada en `PedidoConfiguration`; `Pedido` no expone `RowVersion` | Mismo criterio que `Articulo` (D-12): el dominio no conoce un detalle de persistencia |
| D2 | No hay `FechaCreacion`: se usa `BaseEntity.CreatedAt` (columna `created_at`) | Evita dos columnas con el mismo dato |
| D3 | Se quita `observaciones` de `PEDIDOS` | YAGNI: ningún método ni pantalla la escribe |
| D4 | `QuitarRenglon` saca el renglón de la colección; al guardar, el interceptor de `RetailDbContext.SaveChangesAsync` convierte el huérfano en **baja lógica**. La UK `(id_pedido, id_catalogo_proveedor)` se filtra con `deleted_at IS NULL` para poder volver a agregar el ítem | *Revisada en L1.3.* La versión inicial (remoción física) chocaba con el interceptor global de la Ley 3 y obligaba a una excepción en un archivo compartido. Con D-15 cada comando relee el agregado y el filtro global oculta los renglones dados de baja, así que el agregado en memoria nunca los mezcla. La Ley 3 se cumple sin excepciones y queda registro de lo quitado |

**Tests** en `tests/Retail.Domain.UnitTests/Entities/PedidoTests.cs` (nomenclatura BDD). Como mínimo:
`CrearBorrador_IdsValidos_QuedaEnBorrador`, `AgregarRenglon_ItemYaPresente_SumaCantidadYActualizaFoto`, `AgregarRenglon_PedidoEmitido_LanzaTransicionInvalida`, `AgregarRenglon_CantidadCero_LanzaExcepcion`, `Emitir_SinRenglones_LanzaExcepcion`, `Emitir_ConRenglones_PasaAEmitidoYRegistraFecha`, `MarcarRecibido_PedidoEmitido_PasaARecibido`, `MarcarRecibido_PedidoEnBorrador_LanzaTransicionInvalida`, `MarcarRecibido_PedidoCancelado_LanzaTransicionInvalida`, `Cancelar_SinMotivo_LanzaExcepcion`, `Cancelar_PedidoRecibido_LanzaTransicionInvalida`, `TotalEstimado_VariosRenglones_SumaRedondeada`.

### L1.2. Aplicación (`Retail.Application`)

| Archivo | Contenido |
| :--- | :--- |
| `Interfaces/Services/IPedidoService.cs` | Los dos métodos de frontera de §5.2 **con la firma exacta**, más los de uso interno (tabla siguiente) |
| `Interfaces/Persistence/IPedidoQueryService.cs` | Consultas de solo lectura que usa el servicio |
| `DTOs/Pedidos/` | Los tres DTOs de frontera de §5.2 (sin cambios de nombre ni de tipo) y los internos: `AgregarAlPedidoDto`, `ConsultaPedidosDto`, `PedidoResumenDto`, `PedidosPaginadosDto`, `PedidoDetalleDto`, `RenglonPedidoDto` |
| `Validators/Pedidos/` | `AgregarAlPedidoValidator`, `CancelarPedidoValidator`, `CambiarCantidadRenglonValidator` |
| `Services/PedidoService.cs` | Orquestación. Registro en `DependencyInjection.cs` (solo agregar, §6 del rector) |

Métodos internos de `IPedidoService`:

| Método | Comportamiento |
| :--- | :--- |
| `AgregarAlPedidoAsync(AgregarAlPedidoDto)` | Recibe `IdCatalogoProveedor`, `Cantidad` e `IdUsuario`. Carga el ítem de catálogo y **resuelve su artículo** con el vínculo vigente (hoy, el artículo cuyo `IdCatalogoProveedor` es el ítem; en L3 cambia **solo esta resolución**). Si el ítem no está vinculado, lanza `DomainException` (la UI ofrece incorporarlo antes, A-8). Busca el borrador del proveedor del ítem o lo crea (A-7) y agrega el renglón con la foto actual del catálogo. Devuelve el `PedidoResumenDto` del borrador |
| `ListarPedidosAsync(ConsultaPedidosDto)` | Filtro por estado y proveedor, paginado en SQL, `AsNoTracking`, proyección a DTO (Ley 8) |
| `ObtenerDetalleAsync(int idPedido)` | Cabecera y renglones con el estado actual de cada artículo (dado de baja o no) |
| `CambiarCantidadAsync`, `QuitarRenglonAsync`, `EmitirAsync`, `CancelarAsync` | Cargan el agregado, invocan el método de dominio y guardan |

**Concurrencia al crear el borrador:** si dos terminales agregan a la vez el primer renglón de un proveedor, la segunda viola el índice único del borrador (A-7). El servicio **reintenta una sola vez**: descarta los cambios, vuelve a buscar el borrador y agrega el renglón. Debe tener un test.

**Decisiones de L1.2** (aprobadas por el estudiante el 2026-10-28; no tocan la frontera de §5):

| # | Decisión | Razón |
| :--- | :--- | :--- |
| E1 | Los filtros de los métodos de frontera (`null` si no está `Emitido`, omitir artículos dados de baja, orden) se prueban en **integración** (L1.3); en Aplicación son delegaciones al query service | La lógica vive en la consulta SQL: un mock solo probaría al mock |
| E2 | Todo comando llama a `IRepository<Pedido>.UpdateAsync` antes de guardar | `row_version` está en `PEDIDOS`: un cambio que solo toca `DETALLE_PEDIDOS` no emite `UPDATE` de la raíz y la concurrencia nunca se compararía. `Update` marca la raíz como `Modified`, así se versiona el agregado entero. `ARTICULOS` no lo necesita: todo su estado está en su propia fila |
| E3 | El reintento único de `AgregarAlPedidoAsync` cubre `RestriccionDeUnicidadException` y `ConflictoDeConcurrenciaException`; si falla dos veces, `DomainException` legible | Dos terminales que agregan al mismo borrador chocan por `row_version` (E2); reintentar relee y suma |
| E4 | El artículo se resuelve con `IPedidoQueryService.ObtenerIdArticuloVinculadoAsync` (`int?`, `AsNoTracking`) | Es el único punto que cambia en L3, y no retiene un agregado ajeno en el contexto |
| E5 | DTOs adicionales `CambiarCantidadRenglonDto` y `CancelarPedidoDto` | Los validadores necesitan un tipo que validar |

**Tests** en `tests/Retail.Application.UnitTests/Services/PedidoServiceTests.cs` (NSubstitute) y en los tests de los validadores. Cubrir: el borrador existe y no existe, el ítem no vinculado, el reintento por el índice único, el filtrado de `ObtenerParaCompraAsync` (`null` si no está `Emitido`; omite los artículos dados de baja) y el orden de `ListarEmitidosPorProveedorAsync`.

### L1.3. Infraestructura (`Retail.Infrastructure`)

| Archivo | Contenido |
| :--- | :--- |
| `Persistence/Configurations/PedidoConfiguration.cs` | Tabla `PEDIDOS` según §4.2 del rector: `estado` como texto (F1), `row_version` como propiedad sombra con `IsRowVersion()`, índice único filtrado `(id_proveedor) WHERE estado = N'Borrador' AND deleted_at IS NULL`, FKs `Restrict`, filtro de baja lógica, navegación con `HasField("_detalles")` |
| `Persistence/Configurations/DetallePedidoConfiguration.cs` | Tabla `DETALLE_PEDIDOS`: UK `(id_pedido, id_catalogo_proveedor)`, CHECK `cantidad >= 1` y `costo_pactado > 0`, precisión decimal igual a `DETALLE_COMPRAS` |
| `Persistence/Context/RetailDbContext.cs` | Solo `DbSet<Pedido>` (zona compartida, §6) |
| `Persistence/Services/PedidoQueryService.cs` | Proyecciones `AsNoTracking` y `Select` a DTO, sin `ToList()` prematuro |
| `Persistence/Migrations/…_AgregarPedidos.cs` | Generada con `dotnet ef`. Respetar la regla de migraciones de §7 del rector |

**Decisiones de L1.3** (aprobadas por el estudiante el 2026-10-28; no tocan la frontera de §5):

| # | Decisión | Razón |
| :--- | :--- | :--- |
| F1 | `estado` se guarda como texto (`HasConversion<string>()`) | Los cuatro estados existentes del sistema (`Presupuesto`, `TurnoCaja`, `Venta.EstadoFiscal`, `RegistroBackup`) se guardan así, y el filtro del índice se lee solo |
| F2 | Los tests de concurrencia usan `Repository<T>` y `UnitOfWork` reales con dos contextos | Prueban el camino real de E2 y la traducción a `ConflictoDeConcurrenciaException` |
| F3 | La migración no se aplica a la base de desarrollo durante L1.3; solo a las bases temporales de los tests | Si P1 mergea antes, `AgregarPedidos` se regenera (§7) y una base con la versión vieja aplicada quedaría con historial huérfano |
| F4 | `ObtenerDetalleAsync` incluye y marca los renglones de artículos dados de baja (`IgnoreQueryFilters` + filtros explícitos); `ObtenerParaCompraAsync` los omite | El filtro global ocultaría justo lo que la pantalla de pedidos debe señalar |
| F5 | `TotalEstimado`: suma en SQL, redondeo con `ReglasMonetarias` en memoria sobre la página | `Math.Round` con `AwayFromZero` no se traduce a SQL |

**Tests de integración** en `tests/Retail.Infrastructure.IntegrationTests/` (SQL Server de pruebas): el índice de borrador único, la UK de renglón, `row_version` (dos contextos marcan recibido el mismo pedido y el segundo falla) y las proyecciones de los dos métodos de frontera.

### L1.4. Cierre de L1

* Bucle de verificación completo (sin XAML en esta fase).
* Actualizar `MAPA_DEL_PROYECTO.md` (entidades, enum, excepción, servicio, DTOs, configuración y query service), el DER si difiere de §4.2 y el estado en §2 del rector.
* **Antes de mergear**, pedirle a Pablo que verifique los contratos de §5.2 contra el código (punto de sincronización).

---

## Fase L2a — Pedidos en la UI (dos PR)

**Depende de:** L1. **Migración:** no. **Decisiones del rector:** A-17 y A-18.
**Lectura obligatoria:** `docs/SISTEMA_DE_DISENO.md` y `StyleGalleryView.xaml` (Ley 9).

**Decisiones de diseño de L2a** (aprobadas por el estudiante el 2026-10-28; no tocan la frontera de §5):

| # | Decisión | Razón |
| :--- | :--- | :--- |
| G1 | La entrada "Pedidos" del Sidebar se ve solo con `EsGerente \|\| EsEncargado` (propiedad `PuedeAbastecer` de `MainViewModel`) | A-14 literal. Compras y Catálogos hoy usan `PuedeOperarNegocio`, que incluye al Cajero |
| G2 | El motivo de cancelación se escribe en un `TextBox` del panel de detalle y se confirma con un diálogo de confirmación | No justifica otra ventana |
| G3 | La cantidad editada de un renglón se guarda con una acción explícita ("Aplicar" o Enter) que aparece cuando la fila tiene cambios | Un cambio de foco accidental no escribe en la base |
| G4 | "Pedir" va por fila (columna Acción) y "Pedir seleccionados" en la barra; pedir una fila es un lote de uno | Un solo camino de código y un solo modal |
| G5 | La apertura de `IncorporarArticulosModalDialog` pasa a `IProveedorDialogService.MostrarIncorporacion`, y su orquestación a un método privado que comparten "Incorporar seleccionados" y "Pedir seleccionados" | Sin esto el flujo "no vinculado → incorporar → pedir" no se puede testear, y la incorporación quedaría duplicada |

**Decisiones de Aplicación de L2a-1** (aprobadas por el estudiante el 2026-10-28; no tocan la frontera de §5):

| # | Decisión | Razón |
| :--- | :--- | :--- |
| H1 | Se eliminan `AgregarAlPedidoAsync`, `AgregarAlPedidoDto` y `AgregarAlPedidoValidator`; sus escenarios de test pasan al lote | Con G4, pedir una fila es un lote de uno: el método unitario quedaría sin llamadores |
| H2 | Los ítems inexistentes o sin incorporar se **omiten e informan**; no abortan el lote. Si todos se omiten, no se guarda nada | El modal ya se los mostró al usuario antes de confirmar |
| H3 | La vista previa informa la cantidad que ya figura en el borrador (`CantidadEnBorrador`) | `AgregarRenglon` suma la cantidad: sin el dato, el usuario pide de más sin saberlo |
| H4 | `ObtenerIdArticuloVinculadoAsync` se reemplaza por `ObtenerArticulosVinculadosAsync` (lote) | Una consulta por lote en vez de una por ítem, y sigue siendo el único punto que cambia en L3 (E4) |

**Manejo de errores en la UI** (orden de los `catch`): `TransicionPedidoInvalidaException` → alerta y recarga (el pedido cambió de estado en otra terminal); `ConflictoDeConcurrenciaException` (no hereda de `DomainException`) → alerta y recarga; `ValidationException` → mensaje en línea; `DomainException` → alerta con su mensaje; `Exception` → log y mensaje genérico.

### L2a-1 — Lote, `PedidosView`, modal de pedido y Sidebar

**Rama sugerida:** `feat/pedidos-ui`.

| Capa | Pieza | Contenido |
| :--- | :--- | :--- |
| Aplicación | `DTOs/Pedidos/AgregarVariosAlPedidoDto`, `RenglonAAgregarDto`, `ResultadoAgregarAlPedidoDto`, `Validators/Pedidos/AgregarVariosAlPedidoValidator` | Lote: `IdUsuario` y renglones (`IdCatalogoProveedor`, `Cantidad`). Resultado: un `PedidoResumenDto` por borrador tocado y los ítems omitidos con su motivo (inexistente, no vinculado) |
| Aplicación | `IPedidoService.AgregarVariosAlPedidoAsync` | Agrupa por proveedor, relee cada borrador **una vez** (o lo crea), agrega los renglones y guarda todo en **un solo `SaveChanges`** (A-17). Reintento único de E3. Reemplaza a `AgregarAlPedidoAsync`, su DTO y su validador, que se eliminan (H1) |
| Aplicación | `IPedidoService.ObtenerVistaPreviaAsync` (delega en `IPedidoQueryService`, E1) con `VistaPreviaPedidoDto`, `DestinoPedidoDto`, `RenglonVistaPreviaDto`, `RenglonOmitidoDto` y `MotivoOmisionPedidoEnum` | Por cada ítem: su proveedor, el borrador de destino (existente con su cantidad de renglones, o "se creará"), el artículo vinculado con su stock actual y mínimo, la cantidad que ya figura en el borrador (H3), o el motivo por el que no se puede pedir. Consulta `AsNoTracking` con proyección (Ley 8) |
| UI | `Views/Pages/PedidosView.xaml` + `ViewModels/Pedidos/PedidosViewModel.cs` (con `PedidoFila`, `RenglonPedidoFila`, `OpcionEstadoPedido`) | Filtro por estado (Todos, Borradores, Emitidos, Recibidos, Cancelados) y por proveedor. Grilla con número, proveedor, fecha, antigüedad (`TimeProvider`), cantidad de renglones, total estimado y badge de estado. Panel de detalle con los renglones (badge de artículo dado de baja). En `Borrador`: editar cantidad (G3), quitar renglón, **Emitir**, **Cancelar**. En `Emitido`: **Cancelar** con motivo (G2). Sin navegación a Compras (A-12) |
| UI | Modal de pedido `PedirArticulosDialog` + `PedirArticulosViewModel` | Renglones agrupados por proveedor con el **destino informado** (A-17), cantidad por renglón (1 por defecto), stock actual y mínimo, subtotal y total estimado. Aviso de los ítems que no se pueden pedir. Es una `FluentWindow`: test de humo STA. En L2a-1 se prueba desde sus tests; las entradas llegan en L2a-2 |
| UI | `Services/IPedidoDialogService` + `PedidoDialogService` | Confirmación, alertas, errores y apertura del modal de pedido |
| UI | Navegación | Entrada "Pedidos" en el Sidebar después de Compras (G1), `NavegarPedidos` en `MainViewModel` y registro en `App.xaml.cs` (solo agregar). Textos de `EstadoPedidoEnum` en `EnumATextoConverter` |

Todo acceso a datos por `CargaSerializada` + `Task.Run` (Ley 4). En `PedidosView` la grilla usa la carga reemplazable y el detalle `EjecutarOperacionAsync`, aplicado solo si esa fila sigue seleccionada: hay una sola `CargaSerializada` por pantalla y dos cargas reemplazables se cancelarían entre sí.

**Tests:** del servicio (lote con uno y con varios proveedores, borrador existente y nuevo, ítems omitidos, todos omitidos, reintento), del validador, de integración (el lote se guarda en una transacción; la vista previa), `PedidosViewModelTests` (filtros, acciones habilitadas por estado, confirmaciones, manejo de cada excepción, antigüedad), `PedirArticulosViewModelTests` (agrupación, cantidades, omitidos, total), `MainViewModelTests` (navegación y visibilidad por rol) y el humo STA de `PedidosView` y `PedirArticulosDialog`.

### L2a-2 — Pedir desde Artículos y desde el importador

**Adelantado en L2a-1 (aprobado por el estudiante el 2026-10-28):** el importador muestra la fecha de la última planilla de cada proveedor (una sola por proveedor, no por ítem) y la resalta en ámbar si supera `ImportadorCatalogosViewModel.DiasListaDesactualizada` (30 días; A-2). Se calcula como el `MAX(fecha_actualizacion)` de los ítems vigentes del proveedor, porque cada importación actualiza todas las filas que trae; viaja en `CatalogoPaginadoDto.FechaUltimaPlanilla` y no requiere migración. Marcar ítems que no vinieron en la última planilla queda fuera de alcance: hacerlo sin heurísticas exige registrar cada importación (migración).

**Depende de:** L2a-1.

| Pieza | Contenido |
| :--- | :--- |
| `ImportadorCatalogosView` / `ImportadorCatalogosViewModel` | "Pedir" por fila y "Pedir seleccionados" (R-2: solo comandos y botones). Si hay ítems no incorporados, el modal de pedido ofrece **incorporarlos primero** con el modal existente (G5) o pedir solo los vinculados; si la incorporación se cancela, vuelve con los vinculados. **No** se agrega la columna de stock (A-17) |
| `ArticulosView` / `ArticulosViewModel` | Columna de selección (la selección vive en el ViewModel, no en el DTO), "Seleccionar todos", "Pedir" por fila y "Pedir seleccionados". Los artículos sin proveedor vinculado y los servicios quedan con la acción deshabilitada y un tooltip, y se informan como omitidos en el modal |

**Tests:** `ImportadorCatalogosViewModelTests` (vinculado → pide; no vinculado → incorpora → pide; rechaza incorporar → pide solo vinculados; incorporación cancelada; modal cancelado; error del servicio; sin usuario en sesión) y `ArticulosViewModelTests` (selección, pedir fila, pedir selección, sin proveedor y servicio excluidos).

**Decisiones de implementación de L2a-2** (aprobadas por el estudiante el 2026-10-29; no tocan la frontera de §5):

| # | Decisión | Razón |
| :--- | :--- | :--- |
| I1 | La oferta de incorporar es un botón **dentro del modal de pedido** ("Incorporar los N ítems y volver"). El modal responde `RespuestaPedirArticulos` con tres acciones (cancelar, confirmar, incorporar primero) en lugar de una lista nula | Es lo que dice A-17, y el usuario decide viendo el lote completo. Un `null` no distingue "canceló" de "quiere incorporar" |
| I2 | El segundo paso del recorrido reabre el modal **sin** la oferta, haya confirmado o cancelado la incorporación | El recorrido tiene un final |
| I3 | Lo que se incorpora son los ítems que la **vista previa** informó `SinIncorporar`, no los que la grilla marca como no vinculados | El servidor decide (H2); la grilla puede estar desactualizada |
| I4 | `FlujoPedirArticulos` (UI, sin interfaz) coordina vista previa → modal → guardado para las dos pantallas, en la `CargaSerializada` de cada una | Un caso de uso con dos adaptadores de UI no duplica la interacción (A-17). No decide reglas de negocio: es un coordinador de presentación, no un segundo caso de uso |
| I5 | Selección en Artículos con `ArticuloFila` (envoltura del DTO), **por página**. Los excluidos (servicio, derivado, sin proveedor) se pueden marcar y se informan como avisos previos; si todo queda excluido, no se abre el modal | La selección es estado de WPF y no va en el DTO. Seleccionar entre páginas no lo pide la ERS (YAGNI) |
| I6 | Las acciones de pedir se ocultan al Cajero (`PuedePedir`). La verificación en el servicio queda registrada como deuda del Track 6.1 (A-14) | A-14 y G1 |
| I7 | En el importador y en Artículos los errores se informan con la convención de cada pantalla (en línea o `MostrarError`), no con alertas de recarga | Las alertas de `PedidosView` existen para recargar una grilla de pedidos; agregar a un borrador no produce `TransicionPedidoInvalidaException` |
| I8 | Ajustes de `PedidosView` pedidos por el estudiante: grilla y detalle 50/50; se quitan la columna Antigüedad (y `PedidoFila.Antiguedad`), el botón Actualizar con su atajo F5 y el mensaje de éxito de las acciones; la columna Renglones pasa a llamarse Productos. En el modal de pedido, el total estimado pasa de la barra de botones al contenido | La fecha alcanza para ver un pedido demorado (R-4). La grilla ya se recarga al entrar, al filtrar y tras cada comando; para ver cambios de otra terminal hay que volver a entrar a la pantalla. El total es contenido, no una acción |

**Corrección incluida:** la incorporación del importador (`IncorporarAsync`, compartida por G5) leía categorías y marcas en el Dispatcher y no pasaba por la `CargaSerializada`. Ahora sus dos accesos a datos esperan su turno y corren en `Task.Run` (Ley 4, H-19), con un test que lo demuestra.

**Siguiente mejora fuera de L2a-2:** filtro por rubro en Artículos (combo "Todos los rubros" que acota el combo de categorías, filtro en SQL con `ConsultaArticulosDto.IdRubro`). Es RF-04 y va en un PR propio.

## Fase L2b — "Volver a pedir faltantes"

**Depende de:** L2a-1 y **P2** (necesita `COMPRAS.id_pedido_origen`).

* `IPedidoService.VolverAPedirFaltantesAsync(int idPedido, int idUsuario)`: solo para pedidos `Recibido`. Compara los renglones del pedido con los de la compra que lo cerró (consulta de lectura sobre `DETALLE_COMPRAS` por `id_pedido_origen`; leer otra tabla en una consulta está permitido, modificar el agregado `Compra` no). Por cada artículo con `cantidad comprada < cantidad pedida`, agrega la diferencia al borrador del proveedor con el costo **actual** del catálogo. Devuelve cuántos renglones agregó.
* En `PedidosView`, la acción se habilita solo en pedidos `Recibido` y pide confirmación.
* Tests: con faltantes, sin faltantes, con un artículo comprado de más (se ignora) y con un ítem de catálogo dado de baja (se informa y se omite).

---

## Fase L3 — Fuentes múltiples y fuente principal

L3 se partió en **L3a** (back-end y migración) y **L3b** (UI) el 2026-10-29: la migración con datos se revisa sola. **A-16 está cerrada con la opción A** (rector): `ARTICULOS.id_catalogo_proveedor` es la fuente principal y `CATALOGOS_PROVEEDORES.id_articulo` registra todas las fuentes.

### Decisiones de diseño de L3 (aprobadas el 2026-10-29)

| Id | Propuesta | Fundamento |
| :--- | :--- | :--- |
| J1 | Las reglas del vínculo viven en el dominio y reciben las entidades, no los ids. `CatalogoProveedor.AsignarArticulo(Articulo)` rechaza la presentación derivada (RF-21) y el ítem que ya es fuente de **otro** artículo (A-1). `CatalogoProveedor.LiberarArticulo()` lo desvincula. `Articulo.MarcarFuentePrincipal(CatalogoProveedor)` exige que el ítem ya sea fuente de este artículo (invariante de A-16) y toma su costo. `Articulo.QuitarFuentePrincipal()` lo deja sin principal | Con la opción A, A-1 deja de ser una consulta ("¿hay otro artículo con este ítem?") y pasa a ser un dato del propio ítem. Recibir la entidad permite verificar en el dominio el invariante que la base no declara |
| J2 | *(Reemplazada en L4 por A-19: el formulario pasa a gestionar todas las fuentes y aplica todo al Guardar; el mecanismo de J2-a para cambiar la principal se reutiliza.)* El formulario de artículo maneja **un solo vínculo: la fuente principal**; no administra varias fuentes. Al crear o editar, `InventarioService` asigna la fuente y la marca como principal (J1) en el mismo `SaveChanges`. Si se cambia la principal, la anterior **sigue como fuente adicional** (variante J2-a; se descartó J2-b, que la liberaba). Elegir un ítem que ya es fuente adicional del artículo lo promueve; uno que es fuente de otro artículo se rechaza (J1). "Desvincular" en el formulario libera la principal | Sin este cambio, el formulario escribiría `id_catalogo_proveedor` sin `id_articulo` y rompería el invariante: es el tercer camino de escritura del vínculo. Administrar varias fuentes desde el formulario duplicaría el modal de L4 (A-18) y haría que un Guardar modifique ítems de varios proveedores. Las fuentes adicionales nacen en el importador (J4). J2-a no pierde vínculos en silencio; su costo es que, hasta L4, una adicional solo se quita con el desvincular del importador (J5). Idea para L4, no comprometida: acceso al modal "Fuentes del artículo" desde el formulario en edición |
| J3 | La **baja lógica** de un artículo libera todas sus fuentes en el mismo `SaveChanges` | Sin liberarlas, los ítems figurarían vinculados a un artículo dado de baja y no se podrían volver a incorporar (A-1) |
| J4 | `VincularArticuloACatalogoAsync` recibe `comoPrincipal`. Si el artículo no tiene principal, el ítem queda como principal sin preguntar. Si ya tiene una, el valor por defecto es "adicional", que **no** toca el costo. Vincular como principal recalcula el costo del artículo y el de sus presentaciones, como hoy | El costo solo lo fija la principal (A-2). Un artículo sin principal no tiene a quién preguntarle el costo |
| J5 | `DesvincularArticuloDeCatalogoAsync(idArticulo)` se reemplaza por `DesvincularFuenteAsync(idCatalogoProveedor)`. Si era la principal, el artículo queda sin principal (no se elige otra sola) | Con varias fuentes, "desvincular el artículo" es ambiguo: lo que se quita es una fuente |
| J6 | "Está vinculado" pasa a leer `CATALOGOS_PROVEEDORES.id_articulo` en `CatalogoProveedorQueryService`, en la incorporación y en la resolución ítem → artículo de pedidos (H4) | Una fuente adicional también está vinculada: se puede pedir y no se vuelve a incorporar |

### Fase L3a — Back-end, migración e importador

**Depende de:** L1 y Track 6.6 (✅ en `main`). **Migración:** sí, con migración de datos.

1. ✅ Cerrar A-16 (opción A) y actualizar el rector (2026-10-29).
2. ✅ **Dominio** (J1): `CatalogoProveedor.IdArticulo` con sus operaciones y las de `Articulo`, más tests de dominio de cada regla. (`f6a2014`)
3. ✅ **Persistencia y migración:** FK `CATALOGOS_PROVEEDORES.id_articulo → ARTICULOS` nullable, `Restrict`, con índice. La migración con datos copia cada vínculo activo actual a `id_articulo`: todo vínculo existente pasa a ser **la fuente principal** de su artículo. Test de integración sobre datos sembrados, que cubre también que un artículo dado de baja no se migra. Antes de aplicarla a `RetailDb` local se hace un respaldo. (`f6a2014`)
4. ✅ **Servicios** (J2-J6): incorporación, vincular, desvincular, formulario de artículo y baja. La propagación del importador (RF-07) **no cambia**: ya recorre solo la fuente principal. Escrituras en `67006d8` (4a). En 4b se cerró el `set` de `Articulo.IdCatalogoProveedor`: la principal solo cambia por `MarcarFuentePrincipal` y `QuitarFuentePrincipal`, y los tests que verifican restricciones de la base la escriben con `Entry(...).Property(...)` a propósito.
5. ✅ **Consultas (J6):** "vinculado" pasa a leer `CATALOGOS_PROVEEDORES.id_articulo` con artículo activo en `CatalogoProveedorQueryService` y en la resolución ítem → artículo de pedidos (H4). La consulta de las fuentes de un artículo (proveedor · costo · fecha, la principal marcada) **se trasladó a L3b**, donde está su primer consumidor (el combo del modal de pedido): agregarla antes dejaba una API sin uso (YAGNI).
6. ✅ DER y mapa actualizados.

### Fase L3b — Interfaz

**Depende de:** L3a. **Migración:** no.

#### Decisiones de diseño de L3b (aprobadas el 2026-10-29)

| Id | Decisión | Fundamento |
| :--- | :--- | :--- |
| K1 | La consulta de fuentes se integra en la vista previa: cada `RenglonVistaPreviaDto` trae sus `Fuentes` (proveedor, código, costo, fecha de la lista, si es principal, cantidad en el borrador de su proveedor) y `Destinos` incluye a todos los proveedores de todas las fuentes. Una consulta por lote, proyectada a DTO | El combo cambia de fuente sin volver a la base; una consulta por renglón sería N+1 (Ley 8). L4 reutiliza la proyección para un artículo |
| K2 | La fuente por defecto de cada renglón es el ítem pedido: desde Artículos, la principal (A-18); desde el importador, el ítem elegido | Respeta lo que el usuario eligió en la pantalla de origen |
| K3 | El combo cambia la fuente del renglón y lo mueve al grupo de su proveedor (crea el grupo si no existe y quita el que queda vacío). Dos renglones que terminan en la misma fuente se suman al confirmar | A-18; el validador del lote rechaza un ítem repetido |
| K4 | Decisión (a) del estudiante: la coincidencia por código de barras del importador también ofrece artículos que ya tienen fuentes; el ítem se vincula como adicional (J4) | Con fuentes múltiples, un artículo con principal puede sumar otra fuente |
| K5 | Decisión (b) del estudiante: el filtro por distribuidor de Artículos incluye los artículos con ese distribuidor como cualquier fuente (y sus presentaciones derivadas); "Ninguno" pasa a ser "sin ninguna fuente" | Coherencia entre los dos filtros |
| K6 | Si Artículos está filtrado por un distribuidor, el modal de pedido preselecciona la fuente de ese distribuidor (proveedor preferido en la vista previa) | Filtrar por X para reponer y recibir otro proveedor por defecto sería confuso; coherente con K5 |

0. Consulta `AsNoTracking` de las fuentes de un artículo, proyectada a DTO (Ley 8), con su test de integración (trasladada desde L3a).
1. `VincularArticuloModalDialog` permite vincular a un artículo que ya tiene fuentes e indicar si queda como principal (J4).
2. Modal de pedido: desde Artículos, cada renglón se pide por defecto a la **fuente principal**. Un combo de fuente por renglón (proveedor · costo · fecha, ordenado por costo, la principal marcada) permite cambiarla, y al cambiarla el renglón pasa al grupo de su proveedor (A-18).
3. La grilla de Artículos sigue con **una fila por artículo** (A-18). El indicador "+n" es de L4 (A-19).
4. Tests de ViewModel, auditoría XAML y prueba de humo STA si se crea una vista nueva.
5. Las decisiones (a) y (b) que L3a dejó pendientes se cerraron como K4 y K5.

## Fase L4 — Fuentes en el formulario de artículo (RF-28)

**Depende de:** L3b. **Decisión del rector:** A-19 (reemplaza el modal "Fuentes del artículo" de A-18). **Migración:** no.

#### Decisiones de diseño de L4 (aprobadas el 2026-10-29)

| Id | Decisión | Fundamento |
| :--- | :--- | :--- |
| M1 | Si el artículo no tiene principal, la primera fuente agregada en el formulario queda como principal | Mismo criterio que J4: sin principal no hay de dónde tomar el costo |
| M2 | Marcar una principal en el formulario actualiza en vivo el costo de reposición del formulario (vista previa); al guardar, la regla la aplica el dominio | Hoy "Vincular" ya lo hace; el usuario ve el precio de venta resultante antes de guardar |
| M3 | Quitar la principal deja el artículo sin principal; no se elige otra sola y el costo queda como está | Coherente con J5 |
| M4 | DTO propio `FuenteArticuloDto` (Articulos) con la consulta `ObtenerFuentesAsync(idArticulo)` en `IArticuloQueryService`, `AsNoTracking` y proyectada; no se reutiliza `FuenteRenglonDto` | `CantidadEnBorrador` es de pedidos: el formulario no debe depender de `DTOs/Pedidos` |
| M5 | `ArticulosViewModel` carga las fuentes antes de abrir el diálogo y las pasa a `MostrarDialogoModificar` | El servicio de diálogos no consulta datos (patrón de categorías y marcas) |
| M6 | Un ítem de este artículo que se quitó en el formulario sin guardar vuelve a ofrecerse habilitado en el selector | En la base sigue vinculado a este artículo: sin esta regla no habría forma de arrepentirse antes de Guardar |
| M8 | Un artículo sin principal pero con fuentes adicionales se puede pedir desde Artículos: la consulta paginada trae `IdFuentePedido` (la principal o, si no hay, la más barata vigente, por costo e id) y el modal la preselecciona (K2). No se marca como principal ni cambia el costo (J5, M3 y A-2 siguen vigentes), y la grilla sigue mostrando "Sin principal +n" (2026-10-29, decisión del estudiante) | Antes la fila decía "no tiene proveedor vinculado" aunque tuviera fuentes. El back-end ya resolvía una fuente adicional (J6, K1); faltaba solo qué id mandar. Una subconsulta correlacionada sobre la página evita una consulta por artículo (Ley 8). El modal muestra la fecha de cada fuente, así que una lista vieja y barata se ve antes de confirmar |
| M7 | El aviso del importador que abre el formulario queda fuera de L4 y se registra como deuda | Hoy no existe; exige inyectar `IArticuloDialogService` y cargar categorías y marcas en el importador (R-2). El "+n" de la grilla ya da el acceso |
| M9 | **Con fuente principal, el costo de reposición no se edita a mano.** Al guardar (alta o modificación), `AplicarFuentesDelFormularioAsync` vuelve a marcar la principal aunque no haya cambiado, así su costo reemplaza siempre al del formulario; sin principal se respeta el costo cargado. El formulario lo muestra como texto de solo lectura con su origen ("Fijado por la fuente principal: …") y, al abrir la edición, ya presenta el costo de la principal. Reemplaza el "conserva el costo editado a mano" de M2 (2026-10-30, decisión del estudiante, rediseño del modal de artículo) | Ocultar el campo solo en la UI dejaría la regla como convención visual: el servicio es la frontera que la garantiza para cualquier llamador. Dos fuentes de verdad para el costo (la principal y el campo) confundían qué valor ganaba |
| M10 | **Un servicio no tiene fuentes.** El dominio rechaza vincular un ítem de catálogo a un servicio (`CatalogoProveedor.AsignarArticulo`) y marcarle principal (`Articulo.MarcarFuentePrincipal`); los validadores de alta y modificación rechazan `EsServicio` con `IdsFuentes` no vacío, que cubre las fuentes que el artículo ya tenía y el agregado no conoce. En el formulario, "Servicio" queda deshabilitado mientras haya fuentes (con el motivo en el tooltip) y "Agregar fuente" se deshabilita para un servicio; un servicio con fuentes de datos anteriores sigue mostrando la sección para poder quitarlas. Elegir Servicio ya no pone el stock en 0 en el formulario: lo hacen el DTO y el dominio al guardar (2026-10-30, decisión del estudiante) | Un servicio (fotocopias, anillados, RF-04) no se compra: se compran sus insumos, que son artículos propios. Sin la regla, un servicio con principal se podía pedir (RF-29) y al recibir la compra el dominio lanzaba "Un servicio no maneja stock", lejos del error de origen. Liberar las fuentes en silencio al elegir Servicio se descartó: borra datos sin que el usuario lo vea |
| M11 | **Una sola columna para el código del artículo**: `codigo_barras` guarda el código de barras de fábrica o un código interno corto (p. ej. "11" para fotocopias). Solo dígitos, hasta 50, sin exigir largo GS1 ni dígito verificador. La regla vive en `ReglasCodigoBarras.EsCodigoDeArticuloValido` / `ExigirCodigoDeArticulo`; la aplican `Articulo.ActualizarDatos` (alta, edición y presentaciones) y los tres validadores con `CodigoDeArticulo()`. Los formularios rotulan "Código de barras o interno" y filtran dígitos (2026-10-30, decisión del estudiante) | El POS ya busca primero la coincidencia exacta cuando se tipean dígitos, así que un código corto funciona sin cambios. Dos columnas (B) exigían migración y unicidad entre columnas (un código interno no puede repetir uno de barras), que un índice no resuelve, para el caso raro de un artículo con ambos: los que necesitan código corto (servicios, sueltos) son los que no traen código de fábrica. El verificador se descartó porque un código interno no lo tiene y uno de proveedor mal armado igual identifica al producto |

Vista aprobada: renglones de dos líneas en un `ItemsControl` (opción A), con la principal elegida por `RadioButton` vía comando y el quitar por botón; el selector baja a 760×560 con una tarjeta del artículo.

**Pasos:**

1. ✅ **Aplicación:** `IdsFuentes` en `CrearArticuloDto` / `ActualizarArticuloDto`, reglas del validador (principal entre las fuentes, sin repetidos) y `AplicarFuentesDelFormularioAsync` en `InventarioService` (reemplaza a la J2-a), con `DescartarCambios` también en el alta.
2. ✅ **Infraestructura:** `FuenteArticuloDto`, `ObtenerFuentesAsync` y `ArticuloDto.CantidadFuentes` por subconsulta, con tests de integración.
3. ✅ **UI**, en tres partes. **3a**: sección Fuentes del formulario con `FuenteFormularioFila`, carga de las fuentes desde `ArticulosViewModel` (M5), tests de ViewModel, de clic real (◉ y ✕) y de humo STA. **3b**: selector unificado (`ContextoSelectorFuente`, excluidos, deshabilitados con el artículo dueño, M6, 760×560) y "+n" en la columna Distribuidor ("Sin principal +n" si no hay principal). A pedido del estudiante, el doble clic en una fila de Artículos abre su edición (se ignora sobre botones y casillas de la fila). **3c** (M8): pedir sin principal a la fuente más barata; el motivo de un artículo sin ninguna fuente pasa a ser "no tiene fuentes".

* **Sección Fuentes** en `ArticuloFormDialog` (alta y edición): lista ordenada por costo con proveedor, código, costo, fecha de la lista (en hora local) y la principal marcada. La fecha visible es la defensa contra la lista vieja y barata que motivó A-2.
* **Agregar fuente:** abre `SeleccionarCatalogoProveedorDialog`, de a un ítem. Los ítems vinculados a otro artículo aparecen deshabilitados con el nombre de ese artículo (A-1), y los ya agregados en este formulario no se ofrecen. El selector se alinea con "Vincular ítem a artículo": tarjeta con el lado opuesto (aquí el artículo), buscador, grilla `DataGridRetailStyle` y pie de acciones, con las medidas de `SISTEMA_DE_DISENO.md`.
* **Elegir la principal y quitar fuentes:** sobre la lista, sin efectos hasta Guardar.
* **Guardar** envía el estado deseado: `CrearArticuloDto` / `ActualizarArticuloDto` suman `IdsFuentes`; la principal (`IdCatalogoProveedor`) debe estar entre ellas o ser nula (validador). `InventarioService` calcula la diferencia contra la base (agregar con `AsignarArticulo`, quitar con `LiberarArticulo`, principal con J2-a) en un solo `SaveChanges`, con `DescartarCambios` si el dominio rechaza (Ley 9). **Cancelar** descarta todo.
* **Grilla de Artículos:** "+n" junto a la principal en la columna Distribuidor (n = fuentes adicionales; "Sin principal +n" si no tiene principal), con `CantidadFuentes` calculada por subconsulta en `ArticuloQueryService` (Ley 8). Abre el formulario del artículo.
* **Importador:** el aviso para ítems vinculados con otras fuentes abre el formulario del artículo.
* Consulta `AsNoTracking` de las fuentes de un artículo, proyectada a DTO (Ley 8).
* Tests: de dominio y aplicación del cálculo de diferencias (agregar, quitar, cambiar principal, conflicto A-1), de integración de la consulta y del guardado atómico, de ViewModel de la sección y del selector, de binding con clic real (lección de L3b) y de humo STA.
---

## Fuera de Alcance (propuestas futuras, no implementar)

* Exportar el pedido emitido a Excel o PDF para enviarlo al distribuidor (podría reutilizar MiniExcel o `IS-05`).
* Sugerir la cantidad a pedir según el stock crítico (RF-08).
* Recepciones parciales (A-6).
* Aviso en el importador para los ítems vinculados con otras fuentes, que abra el formulario del artículo (deuda de L4, M7).

## Formato del Reporte del Agente al Terminar Cada Fase

1. Qué se hizo, capa por capa, con los archivos tocados.
2. Resultado del bucle de verificación (comandos y salida resumida).
3. Desvíos respecto de este paquete o del rector, y por qué (debería ser "ninguno").
4. Preguntas de defensa que el docente podría hacer sobre la fase.
5. Propuesta de commit (mensaje), **sin ejecutarlo**.
