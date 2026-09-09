# Etapa 2: Épica 2 - Catálogo, Artículos e Importador Masivo

### Proyecto: Retail (Sistema ERP & Punto de Venta para Librería)
**Documento Maestro:** [`Roadmap de Implementacion.md`](file:///c:/Users/lucas/Proyectos/retail/docs/Roadmap%20de%20Implementacion.md) | **Mapa Semántico:** [`MAPA_DEL_PROYECTO.md`](file:///c:/Users/lucas/Proyectos/retail/docs/MAPA_DEL_PROYECTO.md)  
**Requisitos Vinculados:** [`RF-04`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L228), [`RF-05`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L229), [`RF-06`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L230), [`RF-07`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L231), [`RF-08`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L232), [`RNF-02`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L251), [`RNF-03`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L252)

---

> **Objetivo:** Centralizar el padrón de productos físicos y artesanías sin código de barras, proteger la unicidad con índices filtrados en base de datos, advertir stocks críticos y permitir la actualización de costos mediante planillas Excel de distribuidores en segundo plano.

## Módulos e Interfaces Asignados

### Módulo 2.1: Catálogo Propio de Artículos y Alertas de Stock
**Responsable:** Lucas Kruzolek

* **Interfaz Visual:** `ArticulosView.xaml` (grilla de productos con búsqueda en tiempo real, badges de alerta de stock mínimo `RF-08`, modal reactivo de alta/edición de artículo con cálculo dinámico de precio de venta al ingresar costo y porcentaje de ganancia).
* **ViewModels:** `ArticulosViewModel.cs` y `ArticuloDetalleViewModel.cs`.
* **Lógica y Casos de Uso:** `IInventarioService` (CRUD de artículos, borrado lógico `MarkAsDeleted`, búsqueda por código o texto, categorización). `CrearArticuloValidator`.
* **Dominio:** Agregado `Articulo` (`IAggregateRoot`), `Categoria`, `Marca`. Fórmula de markup:
  $$\text{PrecioVenta} = \text{CostoReposicion} \times \left(1 + \frac{\text{PorcentajeGanancia}}{100}\right)$$
  Soporte de código de barras opcional/nulo para artesanías y servicios.
* **Persistencia:** `ArticuloConfiguration.cs` con Filtered Unique Index en SQL Server:
  ```csharp
  builder.HasIndex(a => a.CodigoBarras)
         .IsUnique()
         .HasFilter("[codigo_barras] IS NOT NULL");
  ```
  `CategoriaConfiguration.cs` y `MarcaConfiguration.cs`.
* **Testing:** Prueba de integración en LocalDB validando que múltiples artesanías con código `NULL` coexistan sin infringir unicidad; pruebas unitarias de markup y de ViewModel.

---

### Módulo 2.2: Proveedores e Importador Masivo Streaming con MiniExcel
**Responsable:** Pablo Fernandez

* **Interfaz Visual:** `ProveedoresView.xaml` (ABM de distribuidores) e `ImportadorView.xaml` (asistente de selección de planillas `.xlsx`/`.csv` locales, vista previa interactiva para mapeo de columnas y barra de progreso asíncrona no bloqueante).
* **ViewModels:** `ProveedoresViewModel.cs` e `ImportadorCatalogosViewModel.cs`.
* **Lógica y Casos de Uso:** `IProveedorService` e `ImportarPlanillaProveedorAsync`: implementación de `ExcelCatalogParser` con MiniExcel en segundo plano (`Task.Run`) para no saturar la memoria (`RNF-02`, `RNF-03`), actualización masiva de costos de catálogo.
* **Dominio:** Agregado `Proveedor` (`IAggregateRoot`), `CatalogoProveedor`, reglas de vinculación de artículos a códigos de proveedor (`RF-05`).
* **Persistencia:** `ProveedorConfiguration.cs` y `CatalogoProveedorConfiguration.cs`.
* **Testing:** Pruebas unitarias de parsing con archivo Excel sintético de 5.000 filas ($< 3\text{ s}$ de lectura, $\le 300\text{ MB}$ de RAM); pruebas de integración de base de datos.

---

## Prevención de Sobreescritura y Criterio de Aceptación
* **Prevención de Sobreescritura:** Lucas es el propietario de `ArticulosView`, `Articulo` y su configuración de base de datos. Pablo es el propietario de `ProveedoresView`, `ImportadorView`, `Proveedor`, `CatalogoProveedor` y el parser de MiniExcel. Ambas interfaces son páginas independientes conectadas al frame de navegación.
* **Criterio de Aceptación Integrado:** Se pueden crear productos artesanales sin código de barras sin colisiones de índice; se importa una lista de distribuidor de 5.000 filas en segundo plano sin congelar la UI, actualizando costos y enlazando con artículos existentes; los artículos con stock bajo exhiben alertas visuales.
