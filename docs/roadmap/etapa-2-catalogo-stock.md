# Etapa 2: Épica 2 - Catálogo, Artículos e Importador Masivo

### Proyecto: Retail (Sistema ERP & Punto de Venta para Librería)
**Documento Maestro:** [`Roadmap de Implementacion.md`](file:///c:/Users/lucas/Proyectos/retail/docs/Roadmap%20de%20Implementacion.md) | **Mapa Semántico:** [`MAPA_DEL_PROYECTO.md`](file:///c:/Users/lucas/Proyectos/retail/docs/MAPA_DEL_PROYECTO.md)  
**Requisitos Vinculados:** [`RF-04`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L228), [`RF-05`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L229), [`RF-06`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L230), [`RF-07`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L231), [`RF-08`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L232), [`RNF-02`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L251), [`RNF-03`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L252)

---

> **Objetivo:** Centralizar el padrón de productos físicos y artesanías sin código de barras, proteger la unicidad con índices filtrados en base de datos, advertir stocks críticos y permitir la actualización de costos mediante planillas Excel de distribuidores en segundo plano.

## Módulos e Interfaces Asignados

### Módulo 2.1: Catálogo Propio de Artículos y Alertas de Stock (Estado: ✅ 100% Completado)
**Responsable:** Lucas Kruzolek

* **Interfaz Visual:** [`ArticulosView.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Pages/ArticulosView.xaml) (grilla de productos con búsqueda en tiempo real, badges de alerta de stock crítico `RF-08`, filtros por categoría y toggle de stock bajo), [`ArticuloFormDialog.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Dialogs/ArticuloFormDialog.xaml) (diálogo modal de alta/edición de artículo con cálculo dinámico en tiempo real de precio de venta por markup).
* **ViewModels y Servicios UI:** [`ArticulosViewModel.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/ViewModels/Articulos/ArticulosViewModel.cs), [`ArticuloFormViewModel.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/ViewModels/Articulos/ArticuloFormViewModel.cs), [`IArticuloDialogService.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Services/IArticuloDialogService.cs) y [`ArticuloDialogService.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Services/ArticuloDialogService.cs).
* **Lógica y Casos de Uso:** [`IInventarioService.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Interfaces/Services/IInventarioService.cs), [`InventarioService.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Services/InventarioService.cs) (CRUD de artículos, borrado lógico `MarkAsDeleted`, búsqueda multicriterio con push-down a SQL Server `RF-06`, categorización y alertas `RF-08`). Validadores FluentValidation: [`CrearArticuloValidator.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Validators/Articulos/CrearArticuloValidator.cs) y [`ActualizarArticuloValidator.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Validators/Articulos/ActualizarArticuloValidator.cs).
* **Dominio:** Agregado [`Articulo.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Domain/Entities/Articulo.cs) (`IAggregateRoot`), [`Categoria.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Domain/Entities/Categoria.cs) (`IAggregateRoot`), [`Marca.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Domain/Entities/Marca.cs) (`IAggregateRoot`). Fórmula de markup reactivo:
  $$\text{PrecioVenta} = \text{CostoReposicion} \times \left(1 + \frac{\text{PorcentajeGanancia}}{100}\right)$$
  Soporte de código de barras opcional/nulo para artesanías y servicios (`RF-04`).
* **Persistencia:** [`ArticuloConfiguration.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Infrastructure/Persistence/Configurations/ArticuloConfiguration.cs) con Filtered Unique Index en SQL Server:
  ```csharp
   builder.HasIndex(a => a.CodigoBarras)
          .IsUnique()
          .HasFilter("[codigo_barras] IS NOT NULL AND [deleted_at] IS NULL");
  ```
  [`CategoriaConfiguration.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Infrastructure/Persistence/Configurations/CategoriaConfiguration.cs) y [`MarcaConfiguration.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Infrastructure/Persistence/Configurations/MarcaConfiguration.cs).
* **Testing:** Pruebas unitarias de invariantes y markup en [`ArticuloTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.Domain.UnitTests/Entities/ArticuloTests.cs) (14 tests); pruebas de orquestación y validación en [`InventarioServiceTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.Application.UnitTests/Services/InventarioServiceTests.cs) (11 tests) y [`ArticuloValidatorTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.Application.UnitTests/Validators/ArticuloValidatorTests.cs) (6 tests); pruebas de UI y reactividad en [`ArticulosViewModelTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.App.UnitTests/ViewModels/ArticulosViewModelTests.cs) (9 tests) y [`ArticuloFormViewModelTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.App.UnitTests/ViewModels/ArticuloFormViewModelTests.cs) (8 tests); e integración en LocalDB validando unicidad filtrada en [`RetailDbContextTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.Infrastructure.IntegrationTests/RetailDbContextTests.cs).

---

### Módulo 2.2: Proveedores e Importador Masivo Streaming con MiniExcel
**Responsable:** Pablo Fernandez

#### 1. Arquitectura y Ciclo Operativo de Dos Fases
Para reflejar con precisión la dinámica real de comercios minoristas (donde una planilla de distribuidor contiene miles de ítems pero la tienda sólo comercializa una fracción), el importador se estructura en dos fases:

* **Fase 1: Ingesta Streaming a Catálogo de Referencia y Recálculo Recurrente:**
  * **Aislamiento de Catálogo (No Contaminación):** La ingesta masiva con `MiniExcel` en `Task.Run` (`RNF-02`, `RNF-03`) puebla exclusivamente la tabla de referencia [`CATALOGOS_PROVEEDORES`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Domain/Entities/CatalogoProveedor.cs). **Queda prohibido volcar masivamente filas a la tabla `ARTICULOS`**, previniendo la degradación de búsquedas en el POS, falsas alertas de stock crítico (`RF-08`) y distorsión del inventario físico.
  * **Actualización Recurrente Automática (`RF-05`, `RF-19`):** Al procesar la lista de costos, el sistema detecta atómicamente todos los artículos propios que ya se encontraban vinculados (`Articulo.IdCatalogoProveedor == item.IdCatalogo`), actualiza su `CostoReposicion` y recalcula automáticamente su `PrecioVenta` aplicando su `PorcentajeGanancia` registrado.
  * **Reporte de Ingesta:** Emite resumen con total de filas leídas, nuevos registros en catálogo de proveedor, precios de venta actualizados en la tienda propia y filas descartadas por error.

* **Fase 2: Explorador y Curaduría de Catálogo (Incorporación y Vinculación):**
  * Consola interactiva para explorar los ítems del proveedor seleccionado con paginación y búsqueda en servidor (`Push-down to SQL`).
  * **Filtros por Estado de Vinculación:**
    * `[Todos]`: Lista completa provista por el distribuidor.
    * `[Sin incorporar]`: Ítems del proveedor que no forman parte del catálogo propio (candidatos a incorporarse).
    * `[Ya en tienda]`: Ítems vinculados a un artículo propio, mostrando el nombre comercial interno, precio de venta vigente y stock actual.
  * **Acciones de Curaduría para Ítems No Vinculados:**
    * **Incorporación Rápida a la Tienda (Individual o en Lote - Batch):** Permite seleccionar uno o varios ítems de proveedor (mediante checkboxes) e incorporarlos a la tabla `ARTICULOS` asignándoles una categoría y un porcentaje de markup sugerido (ej. 40%), creando los registros locales con su `IdCatalogoProveedor` enlazado.
    * **Vinculación a Artículo Existente:** Permite asociar un ítem del proveedor a un artículo propio preexistente (evitando duplicar productos creados a mano).
    * **Detección y Sugerencia Inteligente:** Si la planilla del proveedor incluye código de barras EAN que coincide con un artículo propio no vinculado, la interfaz destaca la coincidencia y ofrece un botón de vinculación en un clic (`[Vincular a existente]`).

#### 2. Componentes e Interfaces Asignados a Pablo
* **Interfaz Visual:**
  * `ProveedoresView.xaml`: ABM y padrón de distribuidores/proveedores mayoristas.
  * `ImportadorView.xaml`: Asistente de selección de planillas `.xlsx`/`.csv` con vista previa de mapeo de columnas, barra de progreso asíncrona, reporte de ingesta y **Explorador de Catálogo** con filtros por estado (`Sin incorporar` / `Ya en tienda`).
  * `IncorporarArticulosModalDialog.xaml`: Diálogo modal ágil para definir categoría y markup antes de confirmar la promoción individual o masiva a `ARTICULOS`.
* **ViewModels:** `ProveedoresViewModel.cs`, `ImportadorCatalogosViewModel.cs` e `IncorporarArticulosModalViewModel.cs`.
* **Lógica y Casos de Uso (`IProveedorService`):**
  * `ImportarPlanillaProveedorAsync(Stream archivoStream, MapeoColumnasDto mapeo, IProgress<int>? progreso, CancellationToken ct)`: Ingesta streaming y recálculo automático de artículos vinculados.
  * `ListarItemsCatalogoAsync(ConsultaCatalogoProveedorDto consulta, CancellationToken ct)`: Consulta paginada con filtros por texto, código y estado de vinculación.
  * `IncorporarArticulosATiendaAsync(IncorporarCatalogoArticulosDto dto, CancellationToken ct)`: Transacción que promueve ítems de proveedor a `ARTICULOS` con su `IdCatalogoProveedor`.
  * `VincularArticuloACatalogoAsync(int idArticulo, int idCatalogoProveedor, CancellationToken ct)`: Asocia un artículo propio preexistente a un ítem de catálogo mayorista.
* **Dominio:** Entidades [`Proveedor.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Domain/Entities/Proveedor.cs) (`IAggregateRoot`) y [`CatalogoProveedor.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Domain/Entities/CatalogoProveedor.cs).
* **Persistencia:** `ProveedorConfiguration.cs` y `CatalogoProveedorConfiguration.cs`.
* **Testing:**
  * Pruebas de parsing streaming con MiniExcel sobre archivo sintético de 5.000 filas ($< 3\text{ s}$, RAM $\le 300\text{ MB}$).
  * Pruebas unitarias y de integración del recálculo automático de precios en artículos propios vinculados tras la re-importación.
  * Pruebas de incorporación individual y en lote verificando integridad referencial con `Articulo`.

---

## Prevención de Sobreescritura y Criterio de Aceptación
* **Prevención de Sobreescritura:** Lucas es el propietario de `ArticulosView`, `Articulo` y su configuración de base de datos. Pablo es el propietario de `ProveedoresView`, `ImportadorView`, `Proveedor`, `CatalogoProveedor` y el parser de MiniExcel. Ambas interfaces son páginas independientes conectadas al frame de navegación.
* **Criterio de Aceptación Integrado:** 
  1. Se pueden crear productos artesanales sin código de barras sin colisiones de índice (`RF-04`).
  2. Se importa una lista de distribuidor de 5.000 filas en segundo plano sin congelar la UI, poblando `CATALOGOS_PROVEEDORES` sin generar sobrepoblación en `ARTICULOS`.
  3. Los artículos previamente vinculados actualizan de forma automática su costo de reposición y precio de venta en catálogo propio (`RF-05`).
  4. El explorador de catálogo permite filtrar ítems no vinculados y promoverlos a `ARTICULOS` (individualmente o en lote) o enlazarlos con artículos existentes con sugerencia automática por código de barras.
  5. Los artículos con stock bajo exhiben alertas visuales (`RF-08`).
