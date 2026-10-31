# Track 6.6, Fase 8: Plan de Implementación — Catálogo del Proveedor Clasificado

### Proyecto: Retail (Sistema ERP & Punto de Venta para Librería)

> **Para el agente que lea esto:** plan de la Fase 8 del Track 6.6, acordado con el estudiante el 2026-10-31. Las decisiones de fondo son **M-72 a M-77** (sección 12 de [`etapa-6-track-6.6-catalogacion-inteligente.md`](file:///c:/Users/lucas/Proyectos/retail/docs/roadmap/etapa-6-track-6.6-catalogacion-inteligente.md)). El motor de clasificación vive en [`ClasificacionService.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Services/ClasificacionService.cs). Rama: `feat/catalogo-clasificado`.
>
> **Lectura obligatoria antes del paso 8.2 (Nivel 3 de AGENTS.md):** [`SISTEMA_DE_PERSISTENCIA.md`](file:///c:/Users/lucas/Proyectos/retail/docs/SISTEMA_DE_PERSISTENCIA.md) y, por las claves foráneas nuevas, [`DER.mmd`](file:///c:/Users/lucas/Proyectos/retail/docs/DER.mmd). **Antes del paso 8.4:** [`SISTEMA_DE_DISENO.md`](file:///c:/Users/lucas/Proyectos/retail/docs/SISTEMA_DE_DISENO.md) y [`StyleGalleryView.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Dev/StyleGalleryView.xaml).

---

## 1. Estado de Avance

| Paso | Contenido | Estado |
| :--- | :--- | :---: |
| 8.0 | Decisiones M-72 a M-77 y este plan | ✅ 2026-10-31 |
| 8.1 | Dominio: sugerencia y estado en `CatalogoProveedor`, preferencia en `Proveedor` | ✅ 2026-10-31: volver a pendiente borra también los ids sugeridos; un texto vacío o igual no cuenta como cambio |
| 8.2 | Persistencia: mapeo, migración, índices y consultas del explorador | ✅ 2026-10-31: migración `AgregarClasificacionSugeridaAlCatalogo` (sin aplicar a la base local) |
| 8.3 | Aplicación: `MotorDeClasificacion`, `ClasificacionCatalogoService`, contratos del importador y del explorador | ✅ 2026-10-31: tests de Dominio (813), Aplicación (963) e integración del catálogo (39) en verde; `ConsultaCatalogoProveedorValidator` nuevo (ids positivos; "sin categoría" excluye una categoría), la paginación la sigue normalizando la consulta |
| 8.4 | Interfaz: toggle al importar, diálogo de valores sin traducir, botón, filtros y columnas del explorador | Después del refactor de UI minimalista (M-77) |
| 8.5 | Cierre: ERS, DER, mapa, track, verificación Release | ⏳ |

---

## 2. El diseño

El objetivo es **descubrir**: el usuario filtra el catálogo del proveedor por las categorías y marcas **del comercio** sin haber incorporado los artículos (M-72). La taxonomía del comercio es el dato maestro y la única que ve el usuario. Los textos del proveedor se conservan como dato de origen (M-12) y no se ofrecen como filtro.

Se sigue la práctica de ETL de separar el dato crudo del derivado:

| Capa | Dónde | Naturaleza |
| :--- | :--- | :--- |
| Crudo | `DescripcionProveedor`, `CodigoBarras`, `RubroProveedor`, `CategoriaProveedor`, `MarcaProveedor` | Tal como lo escribe el proveedor |
| Normalizado | Término, segunda palabra, palabras de marca, prefijo del titular (`ItemAClasificar`) | Se calcula en cada pasada del motor |
| Conformado | `IdCategoriaSugerida`, `IdMarcaSugerida`, `FechaClasificacion` | Derivado y descartable: se recalcula con el botón |

```mermaid
stateDiagram-v2
    [*] --> Pendiente: ítem nuevo
    Pendiente --> Clasificado: el motor lo procesa
    Clasificado --> Pendiente: cambia descripción, código de barras<br/>o un texto del proveedor
    Clasificado --> Clasificado: solo cambia el costo
    Clasificado --> Clasificado: botón "Clasificar catálogo"
```

**Pendiente** (`FechaClasificacion` nula) no es lo mismo que **sin categoría** (con fecha y `IdCategoriaSugerida` nula): el segundo indica que falta una regla (M-74).

La clasificación del catálogo es **pasiva** (M-73): no arma bloques de términos nuevos ni pide revisión por ítem. La sugerencia guardada nunca llega sola al artículo: el modal de incorporación vuelve a correr el motor y el usuario confirma (M-34).

### Cuándo se clasifica (M-75, M-76)

```mermaid
sequenceDiagram
    actor U as Usuario
    participant VI as Importar planilla
    participant PS as ProveedorService
    participant CC as ClasificacionCatalogoService

    U->>VI: Archivo + toggle "Clasificar catálogo"
    VI->>PS: ImportarPlanillaProveedorAsync (guarda filas y la preferencia)
    alt preferencia activa
        VI->>CC: ObtenerValoresSinTraducirAsync
        opt hay valores y el usuario es Gerente o Encargado
            VI->>U: Traducir valores (asignar u omitir)
        end
        VI->>CC: ClasificarAsync(soloPendientes: true)
    end
    U->>CC: Botón "Clasificar catálogo" → ClasificarAsync(soloPendientes: false)
```

---

## 3. Pasos

### 8.1 Dominio

| Archivo | Cambio |
| :--- | :--- |
| `Entities/CatalogoProveedor.cs` | `IdCategoriaSugerida`, `IdMarcaSugerida` (`int?`) y `FechaClasificacion` (`DateTime?`), con setter privado. `RegistrarClasificacionSugerida(int? idCategoria, int? idMarca)` asigna los tres (fecha en UTC). `ActualizarPrecio` y `ActualizarClasificacionDelProveedor` dejan la clasificación pendiente **solo si cambia efectivamente** la descripción, el código de barras o alguno de los textos del proveedor. |
| `Entities/Proveedor.cs` | `ClasificaCatalogo` (`bool`, setter privado) y `ConfigurarClasificacionDelCatalogo(bool clasifica)`. |

Sin navegaciones a `Categoria` ni `Marca` desde `CatalogoProveedor`: los ids bastan para el dominio y la consulta del explorador hace el cruce.

### 8.2 Persistencia

| Archivo | Cambio |
| :--- | :--- |
| `CatalogoProveedorConfiguration.cs` | `id_categoria_sugerida` e `id_marca_sugerida` (FK opcionales a `CATEGORIAS` y `MARCAS`, `Restrict`), `fecha_clasificacion`. Índices `(id_proveedor, id_categoria_sugerida)` e `(id_proveedor, id_marca_sugerida)` filtrados por `[deleted_at] IS NULL`. |
| `ProveedorConfiguration.cs` | `clasifica_catalogo`, `bit`, valor por defecto `0`. |
| Migración `AgregarClasificacionSugeridaAlCatalogo` | Los ítems existentes quedan pendientes. |
| `CatalogoProveedorQueryService.cs` | Filtros de `ObtenerCatalogoPaginadoAsync` por categoría, marca y "sin categoría"; proyección de los nombres sugeridos (solo de categorías y marcas activas: una dada de baja se ve como sin categoría, M-74). Lectura de los ítems de un proveedor para clasificar (todos o pendientes) y de sus valores distintos de rubro, categoría y marca, resueltos en SQL. |

### 8.3 Aplicación

| Archivo | Cambio |
| :--- | :--- |
| `Services/Clasificacion/MotorDeClasificacion.cs` (nuevo, `internal`) | Las reglas de `ClasificacionService` se mueven sin cambios de comportamiento (M-77). |
| `Services/ClasificacionService.cs` | Delega en el motor; sus tests actuales no cambian. |
| `Interfaces/Services/IClasificacionCatalogoService.cs`, `Services/ClasificacionCatalogoService.cs` (nuevos) | `ObtenerValoresSinTraducirAsync(idProveedor)` y `ClasificarAsync(idProveedor, soloPendientes)`, que guarda con una sola unidad de trabajo, como el importador. |
| `MapeoColumnasDto`, `ProveedorService` | El importador recibe la preferencia y la guarda en el proveedor. |
| `ConsultaCatalogoProveedorDto`, `CatalogoProveedorDto` y su validador | Filtros y columnas sugeridas. |

### 8.4 Interfaz (pendiente)

Toggle "Clasificar catálogo" en el diálogo de importación, que arranca con la preferencia del proveedor; diálogo "Traducir valores del catálogo" que reutiliza `TraducirValoresPasoViewModel` (con su test de humo STA); botón "Clasificar catálogo", filtros por categoría, marca y "Sin categoría" y columnas sugeridas en el explorador. El modal de incorporación no cambia.

---

## 4. Riesgos

| Riesgo | Mitigación |
| :--- | :--- |
| Conflicto con el refactor de UI en curso | La interfaz (8.4) se hace después de integrarlo. |
| Un ítem no aparece en su categoría porque se clasificó con reglas viejas | Filtro "sin categoría" y botón "Clasificar catálogo" (M-75). |
| Memoria al clasificar un catálogo grande (RNF-03) | Misma escala que la importación, que ya carga el catálogo en una unidad de trabajo; se mide con el catálogo real en 8.5. |
