# Informe de Auditoría Técnica: Módulo 2.2 — Proveedores e Importador Masivo de Catálogos

**Proyecto:** Retail (Sistema ERP & Punto de Venta para Librería)  
**Módulo Auditado:** Etapa 2 - Épica 2: Módulo 2.2 (*Proveedores e Importador Masivo Streaming con MiniExcel*)  
**Responsable Asignado:** Pablo Fernandez  
**Requisitos Vinculados:** [`RF-05`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L229), [`RF-07`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L231), [`RNF-02`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L251), [`RNF-03`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L252)  
**Normativa Aplicada:** [`AGENTS.md`](file:///c:/Users/lucas/Proyectos/retail/AGENTS.md) (Las 10 Leyes Inviolables, Clean Architecture, DDD, .editorconfig) y [`docs/roadmap/etapa-2-catalogo-stock.md`](file:///c:/Users/lucas/Proyectos/retail/docs/roadmap/etapa-2-catalogo-stock.md)  
**Fecha de Emisión:** 2026-09-21  

---

## 1. Resumen Ejecutivo y Dictamen Global

| Métrica / Dimensión | Calificación | Estado |
| :--- | :---: | :---: |
| **Completitud Funcional (ERS / Roadmap)** | **40%** | 🔴 **Incompleto** |
| **Arquitectura Limpia y Límites de Capas** | **55%** | 🔴 **Degradado** |
| **Rendimiento de Persistencia (Push-down SQL)** | **30%** | 🔴 **Incumple Ley 8** |
| **Reglas de Dominio e Invariantes DDD** | **50%** | 🟡 **Parcial / Bypasseado** |
| **Experiencia de Usuario e Integración UI** | **45%** | 🔴 **Bloqueos Funcionales** |
| **Cobertura de Pruebas Automatizadas (xUnit)** | **15%** | 🔴 **Crítico** |
| **Compilación y Recursos XAML** | **100%** | 🟢 **0 Errores / 0 Warnings** |

> [!CAUTION]
> **Dictamen: NO APROBADO PARA CIERRE DE ETAPA.**  
> Si bien la solución compila en Release sin advertencias y supera la auditoría estática de recursos XAML, la implementación del Módulo 2.2 presenta **bloqueos operativos en tiempo de ejecución**, **omisión de casos de uso centrales de la ERS** (vinculación a artículos preexistentes, detección inteligente de códigos de barras EAN, formularios modales de proveedores), **violaciones directas a las leyes de Clean Architecture y Push-down a SQL**, y un **déficit casi total de cobertura de tests**.

---

## 2. Hallazgos Críticos por Capa

### A. Capa de Dominio (`Retail.Domain`)

1. **Omisión de Atributo Crítico en [`CatalogoProveedor.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Domain/Entities/CatalogoProveedor.cs):**
   * **Requisito Afectado:** [`RF-05`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L229) y Roadmap 2.2 (Fase 2: *Detección y Sugerencia Inteligente*).
   * **Hallazgo:** La entidad `CatalogoProveedor` solo contiene `CodigoProveedor`, `DescripcionProveedor` y `CostoReposicion`. **No posee la propiedad `CodigoBarras`**.
   * **Impacto:** Impide verificar si el producto de la planilla del distribuidor coincide con un artículo propio por código EAN, haciendo imposible cumplir la funcionalidad de detección y vinculación asistida en un clic.
2. **Ambigüedad en Raíces de Agregado DDD (Ley 2):**
   * `CatalogoProveedor` implementa `IAggregateRoot` ([`CatalogoProveedor.cs:L8`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Domain/Entities/CatalogoProveedor.cs#L8)), pero en el mapa conceptual y el DER es una entidad interna subordinada a la raíz `Proveedor`. Al dotarlo de repositorio independiente (`IRepository<CatalogoProveedor>`), se erosiona el límite del agregado Proveedor.
3. **Ausencia de Invariantes y Métodos de Negocio:**
   * Tanto `Proveedor` como `CatalogoProveedor` son simples bolsas de propiedades anémicas (POCOs), sin validaciones de CUIT, normalización de cadenas ni custodia de invariantes de dominio.

---

### B. Capa de Infraestructura y Persistencia (`Retail.Infrastructure`)

1. **Violación de Ley 3 (Soft Delete) en [`ProveedorConfiguration.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Infrastructure/Persistence/Configurations/ProveedorConfiguration.cs#L28-L29):**
   * **Código actual:**
     ```csharp
     builder.HasIndex(p => p.Cuit)
         .IsUnique();
     ```
   * **Problema:** No cuenta con el filtro `.HasFilter("[deleted_at] IS NULL")`.
   * **Consecuencia:** Si un proveedor es dado de baja lógica (`MarkAsDeleted()`), SQL Server mantendrá el índice único activo, impidiendo volver a registrar un proveedor con ese CUIT o reabrirlo con nueva razón social. Rompe el estándar establecido en `ArticuloConfiguration` y `ClienteConfiguration`.
2. **Falta de Índice Compuesto en [`CatalogoProveedorConfiguration.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Infrastructure/Persistence/Configurations/CatalogoProveedorConfiguration.cs):**
   * No existe índice único filtrado para `(id_proveedor, codigo_proveedor, deleted_at)`. Esto permite que una re-importación corrupta inserte duplicados del mismo código para un mismo proveedor.
3. **Falsa Abstracción e Incumplimiento de Inversión de Dependencias (DIP / Ley 1):**
   * En [`IExcelCatalogParser.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Interfaces/Infrastructure/IExcelCatalogParser.cs) se definió la abstracción de infraestructura para parseo streaming con MiniExcel.
   * **Sin embargo, dicha interfaz NUNCA se implementó en `Retail.Infrastructure`**. La carpeta `src/Retail.Infrastructure/ExternalServices/Excel/` no existe, y el servicio no está registrado en el contenedor de dependencias (`DependencyInjection.cs`).

---

### C. Capa de Aplicación (`Retail.Application`)

1. **Acoplamiento Directo a Dependencia Externa en [`Retail.Application.csproj`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Retail.Application.csproj#L16):**
   * Para eludir la implementación en `Infrastructure`, se instaló `MiniExcel (1.34.2)` directamente en `Retail.Application`, violando la Ley 1 de Clean Architecture: *`Retail.Application` solo depende de Domain, FluentValidation y abstracciones de DI*.
2. **Violación Grave de la Ley 8 (Push-down to SQL y Evaluación en Servidor):**
   * En [`ProveedorService.cs:L139`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Services/ProveedorService.cs#L139):
     ```csharp
     var rows = MiniExcel.Query(archivoStream, useHeaderRow: true).ToList();
     ```
     MiniExcel ofrece un flujo diferido (`IEnumerable`). Ejecutar `.ToList()` vuelca masivamente **todas las filas en memoria RAM** como diccionarios dinámicos, destruyendo el streaming y arriesgando el límite de 300 MB de RAM (`RNF-03`).
   * En [`ProveedorService.cs:L212-L255`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Services/ProveedorService.cs#L212-L255) (`ListarItemsCatalogoAsync`):
     ```csharp
     var catalogos = await _catalogoRepository.FindAsync(c => c.IdProveedor == consulta.IdProveedor, ...);
     var articulos = await _articuloRepository.FindAsync(a => a.IdCatalogoProveedor != null, ...);
     ```
     **Descarga todos los catálogos del proveedor y todos los artículos vinculados de la tienda a la memoria del cliente** para luego ejecutar `Where`, `OrderBy`, `Skip` y `Take` mediante LINQ to Objects en memoria. Ante planillas de 10.000 a 50.000 filas, esto genera un impacto de latencia inaceptable.
3. **Violación de Encapsulación de Dominio y Ausencia de Redondeo en Markup:**
   * En [`ProveedorService.cs:L170`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Services/ProveedorService.cs#L170), [`L277`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Services/ProveedorService.cs#L277) y [`L300`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Services/ProveedorService.cs#L300):
     ```csharp
     articuloAsociado.PrecioVenta = precioCosto * (1 + articuloAsociado.PorcentajeGanancia / 100m);
     ```
     Se ignora el método canónico del dominio [`Articulo.CalcularPrecioVenta(...)`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Domain/Entities/Articulo.cs#L46) y [`Articulo.ActualizarCostoYRecalcularPrecio(...)`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Domain/Entities/Articulo.cs#L61), **omitiendo el redondeo obligatorio a dos decimales (`Math.Round(..., 2)`)**. Esto puede propagar importes con decimales flotantes a ventas y reportes fiscales.
4. **Ausencia Absoluta de Validadores FluentValidation:**
   * No existe la carpeta `src/Retail.Application/Validators/Proveedores/`.
   * Ni `CrearProveedorDto`, ni `MapeoColumnasDto`, ni `IncorporarCatalogoArticulosDto` cuentan con validación en fronteras. Incluso se detectó el comentario explícito en [`ProveedorService.cs:L69`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Services/ProveedorService.cs#L69):
     *`// Add basic logic here. For real apps, validation should be done with FluentValidation`*.
5. **Comportamiento Inconcluso en Cronometraje:**
   * En [`ProveedorService.cs:L206`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Services/ProveedorService.cs#L206):
     *`TiempoTranscurrido = TimeSpan.Zero // Needs timing logic but keeping it simple to satisfy the DTO`*.

---

### D. Capa de Presentación (`Retail.App` / MVVM / XAML)

1. **Bloqueo Funcional Total en la Incorporación de Artículos:**
   * En [`ImportadorCatalogosViewModel.cs:L270-L274`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/ViewModels/Proveedores/ImportadorCatalogosViewModel.cs#L270-L274):
     ```csharp
     if (!IdCategoriaDestino.HasValue)
     {
         MensajeError = "Debe seleccionar una categoría de destino.";
         return;
     }
     ```
   * En [`ImportadorCatalogosView.xaml:L380-L392`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Pages/ImportadorCatalogosView.xaml#L380-L392), en la barra inferior **sólo existe un campo para `% Ganancia sugerido`**. No existe ningún selector ni ComboBox para asignar `IdCategoriaDestino`.
   * **Resultado:** Cada vez que el operador selecciona artículos de la grilla y presiona *"Incorporar Seleccionados a Tienda"*, la acción es rechazada sistemáticamente con el mensaje de error *"Debe seleccionar una categoría de destino"*.
2. **Diálogo Desconectado y Anti-patrón de UX en [`IncorporarArticulosModalDialog.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Dialogs/IncorporarArticulosModalDialog.xaml):**
   * El diálogo modal `IncorporarArticulosModalDialog` fue maquetado pero **está completamente desconectado** (código huérfano; ningún ViewModel ni servicio de diálogos lo abre).
   * Su interfaz contiene un `TextBox` plano que le exige al usuario tipear un número entero: *"ID Categoría Destino"* ([`IncorporarArticulosModalDialog.xaml:L47`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Dialogs/IncorporarArticulosModalDialog.xaml#L47)).
   * Su ViewModel utiliza `MessageBox.Show(...)` nativo ([`IncorporarArticulosModalViewModel.cs:L29`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/ViewModels/Proveedores/IncorporarArticulosModalViewModel.cs#L29)), rompiendo el desacople de MVVM y testabilidad.
   * La vista hereda de `Window` plano en lugar de `ui:FluentWindow`, perdiendo la integración visual con el sistema de diseño.
3. **Formularios de ABM de Proveedores Simulados con Stubs:**
   * En [`ProveedorDialogService.cs:L21-L32`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Services/ProveedorDialogService.cs#L21-L32):
     ```csharp
     public async Task<bool> AbrirFormularioNuevoProveedorAsync()
     {
         MessageBox.Show("Formulario de nuevo proveedor en construcción.", "Aviso", ...);
         return false;
     }
     ```
   * No existe una ventana o diálogo modal `ProveedorFormDialog.xaml`. Los botones *"Nuevo Proveedor [F2]"* y *"Editar [F4]"* de [`ProveedoresView.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Pages/ProveedoresView.xaml) únicamente disparan un `MessageBox` informativo que no realiza ninguna acción.
4. **Paginación Inexistente o Ficticia:**
   * En `ProveedoresView`, no se implementó `PaginationBar`. Carga toda la base en memoria y filtra mediante LINQ to Objects.
   * En `ImportadorCatalogosView`, la propiedad `PaginaActual` está fija en `1`. No hay botones para avanzar o retroceder de página. Además, `TotalItemsCatalogo` se asigna con `ItemsCatalogo.Count` (máximo 50), reportando al usuario un total incorrecto sobre la planilla importada.
5. **Bug de Visibilidad en [`ProveedoresView.xaml:L131-L135`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Pages/ProveedoresView.xaml#L131-L135):**
   * El indicador de total de proveedores registrados en la cabecera está vinculado a:
     `Visibility="{Binding IsBusy, Converter={StaticResource BoolToVisConverter}}"`.
   * **Consecuencia:** Solo se muestra mientras la vista está ocupada cargando; una vez finalizada la carga, el contador desaparece de la interfaz.
6. **Omisión de Curaduría: Vinculación a Artículo Existente (`RF-05`):**
   * Aunque `IProveedorService` expone `VincularArticuloACatalogoAsync`, en `ImportadorCatalogosView` no hay ningún botón, columna de acción ni diálogo selector para asociar un ítem mayorista a un producto del catálogo local.

---

### E. Capa de Testing Automatizado (`tests/`)

| Área de Prueba | Estado Actual | Requerimiento de ERS / Roadmap |
| :--- | :---: | :--- |
| **Parsing Streaming MiniExcel** | ❌ **0 pruebas** | Test de estrés con 5.000 filas sintéticas ($< 3\text{ s}$, $\text{RAM} \le 300\text{ MB}$). |
| **Recálculo de Precios en Re-importación** | ❌ **0 pruebas** | Actualización automática de `CostoReposicion` y `PrecioVenta` al volver a cargar la planilla (`RF-05`). |
| **Casos de Uso `ProveedorService`** | ⚠️ **1 prueba aislada** | [`ImportadorHandlerTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.Application.UnitTests/Services/ImportadorHandlerTests.cs) solo prueba `IncorporarArticulosATiendaAsync` con NSubstitute. 0 pruebas para importación, listados o vinculación. |
| **ViewModels de UI (`Retail.App.UnitTests`)** | ❌ **0 pruebas** | Cero tests para `ProveedoresViewModel`, `ImportadorCatalogosViewModel` e `IncorporarArticulosModalViewModel`. |
| **Integración LocalDB (`Retail.Infrastructure`)** | ❌ **0 pruebas** | Ninguna prueba de persistencia relacional ni transacciones para `Proveedor` y `CatalogoProveedor`. |
| **Estándar de Aserciones** | ⚠️ **Desvío** | [`ProveedorDomainTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.Domain.UnitTests/Entities/ProveedorDomainTests.cs) utiliza `Assert.Equal` en vez de `FluentAssertions` (`.Should().Be(...)`). |

---

## 3. Matriz de Trazabilidad: Requisitos vs. Estado de Implementación

| ID | Requisito / Restricción | Estado | Brecha Identificada |
| :--- | :--- | :---: | :--- |
| **RF-05** | **Vinculación con Catálogos y Curaduría** | 🔴 **Incompleto** | • No existe acción en UI para asociar ítems a artículos existentes.<br>• Falta `CodigoBarras` en `CatalogoProveedor` para detección inteligente.<br>• La incorporación en lote falla por ausencia de selector de categorías. |
| **RF-07** | **Importación Streaming en Segundo Plano** | 🟡 **Parcial** | • Se utiliza MiniExcel pero se ejecuta `.ToList()` en memoria.<br>• No se delegó a `IExcelCatalogParser` en `Infrastructure`.<br>• El tiempo transcurrido es reportado como `TimeSpan.Zero`. |
| **RNF-02** | **Responsividad de UI (No Bloqueo)** | 🟢 **Conforme** | Las tareas de IO y base de datos se delegan mediante `Task.Run` y llamadas asíncronas. |
| **RNF-03** | **Memoria y Estabilidad ($\le 300\text{ MB}$)** | 🔴 **En Riesgo** | Materialización en memoria de listas completas de Excel y bases de datos con `.ToList()` prematuro. |
| **Ley 1** | **Clean Architecture Estricta** | 🔴 **No Conforme** | Dependencia de `MiniExcel` instalada en `Retail.Application` en vez de encapsularse en `Retail.Infrastructure`. |
| **Ley 3** | **Borrado Lógico y Filtered Indexes** | 🔴 **No Conforme** | Índice único en `PROVEEDORES.cuit` no contempla `[deleted_at] IS NULL`. |
| **Ley 8** | **Push-down to SQL** | 🔴 **No Conforme** | `ListarItemsCatalogoAsync` recupera todas las filas a memoria antes de filtrar y paginar con LINQ to Objects. |
| **Ley 9** | **Sistema de Diseño y Fluent UI** | 🟡 **Parcial** | Diálogo modal sin `ui:FluentWindow`, ABMs con stubs `MessageBox.Show`, falta selector de categorías. |
