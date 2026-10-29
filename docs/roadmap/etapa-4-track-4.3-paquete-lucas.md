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
| `static Pedido CrearBorrador(int idProveedor, int idUsuario)` | Ids > 0. Estado `Borrador`, `FechaCreacion` en UTC |
| `void AgregarRenglon(int idArticulo, int idCatalogoProveedor, string codigoProveedor, string descripcion, int cantidad, decimal costoPactado)` | Solo en `Borrador`. Cantidad ≥ 1, costo > 0, textos no vacíos. Si el ítem de catálogo ya está en el pedido, **suma la cantidad** y actualiza la foto (código, descripción y costo) con los valores recibidos |
| `void CambiarCantidad(int idCatalogoProveedor, int cantidad)` | Solo en `Borrador`. Cantidad ≥ 1. Si el renglón no existe, `DomainException` |
| `void QuitarRenglon(int idCatalogoProveedor)` | Solo en `Borrador`. Puede dejar el borrador vacío (no se cancela solo) |
| `void Emitir()` | `Borrador → Emitido`. Exige al menos un renglón. Registra `FechaEmision` |
| `void MarcarRecibido()` | `Emitido → Recibido`. Registra `FechaCierre`. **Contrato de frontera (§5.1)** |
| `void Cancelar(string motivo)` | `Borrador \| Emitido → Cancelado`. Motivo obligatorio (normalizado con `ReglasTexto`). Registra `FechaCierre` |
| `decimal TotalEstimado` | Calculado: Σ `cantidad × costoPactado`, redondeado con `ReglasMonetarias`. No se persiste |
| `byte[] RowVersion` | Concurrencia optimista (A-13) |

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

**Tests** en `tests/Retail.Application.UnitTests/Services/PedidoServiceTests.cs` (NSubstitute) y en los tests de los validadores. Cubrir: el borrador existe y no existe, el ítem no vinculado, el reintento por el índice único, el filtrado de `ObtenerParaCompraAsync` (`null` si no está `Emitido`; omite los artículos dados de baja) y el orden de `ListarEmitidosPorProveedorAsync`.

### L1.3. Infraestructura (`Retail.Infrastructure`)

| Archivo | Contenido |
| :--- | :--- |
| `Persistence/Configurations/PedidoConfiguration.cs` | Tabla `PEDIDOS` según §4.2 del rector: `estado` como `int`, `row_version` con `IsRowVersion()`, índice único filtrado `(id_proveedor) WHERE estado = 1 AND deleted_at IS NULL`, FKs `Restrict`, filtro de baja lógica, navegación con `HasField("_detalles")` |
| `Persistence/Configurations/DetallePedidoConfiguration.cs` | Tabla `DETALLE_PEDIDOS`: UK `(id_pedido, id_catalogo_proveedor)`, CHECK `cantidad >= 1` y `costo_pactado > 0`, precisión decimal igual a `DETALLE_COMPRAS` |
| `Persistence/Context/RetailDbContext.cs` | Solo `DbSet<Pedido>` (zona compartida, §6) |
| `Persistence/Services/PedidoQueryService.cs` | Proyecciones `AsNoTracking` y `Select` a DTO, sin `ToList()` prematuro |
| `Persistence/Migrations/…_AgregarPedidos.cs` | Generada con `dotnet ef`. Respetar la regla de migraciones de §7 del rector |

**Tests de integración** en `tests/Retail.Infrastructure.IntegrationTests/` (SQL Server de pruebas): el índice de borrador único, la UK de renglón, `row_version` (dos contextos marcan recibido el mismo pedido y el segundo falla) y las proyecciones de los dos métodos de frontera.

### L1.4. Cierre de L1

* Bucle de verificación completo (sin XAML en esta fase).
* Actualizar `MAPA_DEL_PROYECTO.md` (entidades, enum, excepción, servicio, DTOs, configuración y query service), el DER si difiere de §4.2 y el estado en §2 del rector.
* **Antes de mergear**, pedirle a Pablo que verifique los contratos de §5.2 contra el código (punto de sincronización).

---

## Fase L2a — `PedidosView` y "Agregar al pedido"

**Depende de:** L1. **Rama sugerida:** `feat/pedidos-ui`. **Migración:** no.  
**Lectura obligatoria:** `docs/SISTEMA_DE_DISENO.md` y `StyleGalleryView.xaml` (Ley 9).

| Pieza | Contenido |
| :--- | :--- |
| `Views/Pages/PedidosView.xaml` + `ViewModels/Pedidos/PedidosViewModel.cs` | Filtro por estado (Todos, Borradores, Emitidos, Recibidos, Cancelados) y por proveedor. Grilla con número, proveedor, fecha, antigüedad, cantidad de renglones, total estimado y badge de estado. Panel de detalle con los renglones. En `Borrador`: editar la cantidad, quitar renglón, **Emitir**, **Cancelar**. En `Emitido`: **Cancelar** con motivo. Sin navegación a Compras (A-12) |
| Diálogo `AgregarAlPedidoDialog` + ViewModel | Pide la cantidad (por defecto 1) y muestra proveedor, código, descripción y costo actual. Es una `FluentWindow`: necesita test de humo STA |
| `ImportadorCatalogosView` / `ImportadorCatalogosViewModel` | **Solo** un comando y un botón "Agregar al pedido" para el ítem seleccionado (R-2 del rector). Si el ítem no está vinculado, ofrecer primero la incorporación con el modal existente (`IncorporarArticulosModalDialog`) y, si se confirma, continuar |
| Navegación | Entrada "Pedidos" en el Sidebar (`MainViewModel`, zona de Lucas según §6) y registro en `App.xaml.cs` (solo agregar) |

Reglas: todo acceso a datos por `CargaSerializada` + `Task.Run` (Ley 4); los errores de dominio se muestran como alertas; las vistas usan solo recursos existentes (`scripts/audit-xaml.ps1`).

**Tests:** `PedidosViewModelTests` y `AgregarAlPedidoViewModelTests` (filtros, transiciones habilitadas por estado, confirmación de cancelación, flujo de "no vinculado → incorporar → agregar"), los tests nuevos de `ImportadorCatalogosViewModelTests` y el humo STA de `PedidosView` y `AgregarAlPedidoDialog` en `AppSmokeTests.cs`.

## Fase L2b — "Volver a pedir faltantes"

**Depende de:** L2a y **P2** (necesita `COMPRAS.id_pedido_origen`).

* `IPedidoService.VolverAPedirFaltantesAsync(int idPedido, int idUsuario)`: solo para pedidos `Recibido`. Compara los renglones del pedido con los de la compra que lo cerró (consulta de lectura sobre `DETALLE_COMPRAS` por `id_pedido_origen`; leer otra tabla en una consulta está permitido, modificar el agregado `Compra` no). Por cada artículo con `cantidad comprada < cantidad pedida`, agrega la diferencia al borrador del proveedor con el costo **actual** del catálogo. Devuelve cuántos renglones agregó.
* En `PedidosView`, la acción se habilita solo en pedidos `Recibido` y pide confirmación.
* Tests: con faltantes, sin faltantes, con un artículo comprado de más (se ignora) y con un ítem de catálogo dado de baja (se informa y se omite).

---

## Fase L3 — Fuentes múltiples y fuente principal

**Depende de:** L1 y cierre de la fase en curso del Track 6.6 (migraciones de `CatalogoProveedor`). **Migración:** sí, con migración de datos.

1. **Cerrar A-16 con el estudiante** (opción A o B del rector), con el impacto real medido sobre el código del Track 6.6 ya integrado. Actualizar el rector.
2. Dominio: el vínculo deja de ser 1:1. Reglas: un ítem se vincula a lo sumo a un artículo (A-1); nunca a una presentación derivada (RF-21); a lo sumo una fuente principal por artículo (A-2). Operaciones: vincular como fuente adicional, marcar como principal, desvincular (si se desvincula la principal, el artículo queda sin principal hasta que el usuario elija otra; no se elige sola).
3. Migración de datos: todo vínculo existente pasa a ser **la fuente principal** de su artículo. Test de integración que lo verifique sobre datos sembrados.
4. Importador (RF-07): propaga costo y precio **solo** a través de las fuentes principales.
5. `AgregarAlPedidoAsync`: cambiar únicamente la resolución ítem → artículo (L1.2).
6. UI: `VincularArticuloModalDialog` permite vincular a un artículo que ya tiene fuentes, indicando si queda como principal.
7. Actualizar el DER (las fuentes múltiples no están en PR-0) y el mapa.

## Fase L4 — Comparador de costos (RF-28)

**Depende de:** L3. Se hace después de la Fase 6 (UI) del Track 6.6 (R-2).

* Panel "Fuentes del artículo" en `ImportadorCatalogosView` para el ítem seleccionado, si está vinculado: todas las fuentes del mismo artículo con proveedor, código, descripción del proveedor, costo, fecha de actualización de la planilla y badge de principal. Se ordena por costo.
* Acciones por fila: **Agregar al pedido** (al borrador de **ese** proveedor) y **Marcar como principal**.
* Consulta `AsNoTracking` por `IdArticulo`, con proyección a DTO (Ley 8).
* La fecha de cada lista es visible: una lista vieja y barata es la trampa que motivó A-2.
* Tests de ViewModel, de la consulta (integración) y de humo STA si se agrega un control nuevo.

---

## Fuera de Alcance (propuestas futuras, no implementar)

* Exportar el pedido emitido a Excel o PDF para enviarlo al distribuidor (podría reutilizar MiniExcel o `IS-05`).
* Sugerir la cantidad a pedir según el stock crítico (RF-08).
* Recepciones parciales (A-6).

## Formato del Reporte del Agente al Terminar Cada Fase

1. Qué se hizo, capa por capa, con los archivos tocados.
2. Resultado del bucle de verificación (comandos y salida resumida).
3. Desvíos respecto de este paquete o del rector, y por qué (debería ser "ninguno").
4. Preguntas de defensa que el docente podría hacer sobre la fase.
5. Propuesta de commit (mensaje), **sin ejecutarlo**.
