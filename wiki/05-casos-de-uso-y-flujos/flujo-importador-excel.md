# Flujo de Importación Masiva: Streaming con MiniExcel sin Bloqueo de UI

### Módulo: 05. Casos de Uso y Flujos de Negocio de Punta a Punta
**Audiencia:** Desarrolladores, estudiantes universitarios y mesa evaluadora de cátedra  
**Requisitos Vinculados:** `RF-05` (Catálogo de Proveedores) y `RF-07` (Importador Masivo de Listas de Precios)  
**Principios Rectores:** Ley 4 (Responsividad de UI Dispatcher) y Restricción RNF-03 ($\le 300\text{ MB}$ RAM)  
**Tecnología Principal:** `MiniExcel 1.34.2` sobre `Task.Run`  
**Archivos de Código:**
* Interfaz: [`IExcelCatalogParser.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Interfaces/Infrastructure/IExcelCatalogParser.cs)
* Entidades: [`CatalogoProveedor.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Domain/Entities/CatalogoProveedor.cs) y [`Articulo.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Domain/Entities/Articulo.cs)

---

## 1. 📖 Fundamento Teórico y Académico

### 1.1 La Dinámica Comercial con Distribuidores Mayoristas
Una librería trabaja cotidianamente con múltiples distribuidores editoriales y papeleros (ej. Ledesma, Rivadavia, Estrada, Bic). Periódicamente, los proveedores envían **planillas de cálculo de Excel (`.xlsx`)** con listas de precios que contienen entre **10.000 y 50.000 artículos**.

El desafío de ingeniería es doble:
1. **La Sobrecarga de Memoria en Lectura DOM (OpenXML vs. Streaming):**  
   Librerías convencionales como `ClosedXML` o el SDK oficial de Microsoft `DocumentFormat.OpenXml` cargan el árbol jerárquico completo del documento en memoria (modelo DOM). En una planilla de 30.000 filas, esto genera alocaciones de $300\text{ MB}$ a $800\text{ MB}$ de RAM en el proceso, provocando pausas severas del recolector de basura (*GC Pauses*) o caídas por `OutOfMemoryException`.
2. **La Responsividad de Mostrador ($< 15\text{ ms}$):**  
   Si la lectura del archivo se ejecutara en el hilo principal de WPF (`UI Dispatcher`), la ventana de mostrador se congelaría durante minutos, impidiendo escanear artículos y atender clientes.

### 1.2 La Solución: MiniExcel y Streaming Forward-Only
En Retail adoptamos **MiniExcel**, un motor de procesamiento de archivos Excel de ultra-bajo consumo basado en el patrón **Streaming / Forward-Only**:
* No construye un árbol de objetos en memoria.
* Lee los bytes del archivo ZIP/XML en un búfer continuo y emite las filas una a una.
* El consumo de memoria RAM permanece constante en **menos de $25\text{ MB}$**, sin importar si la planilla tiene 100 filas o 100.000 filas.

---

## 2. 💻 Aplicación Concreta en el Código de Retail

### 2.1 El Contrato de Infraestructura: `IExcelCatalogParser`
En [`src/Retail.Application/Interfaces/Infrastructure/IExcelCatalogParser.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Interfaces/Infrastructure/IExcelCatalogParser.cs):

```csharp
public interface IExcelCatalogParser
{
    IAsyncEnumerable<FilaCatalogoExcelDto> ParsearCatalogoAsync(
        Stream excelStream, 
        CancellationToken cancellationToken = default);
}
```
Observa el uso de **`IAsyncEnumerable<T>`**: las filas no se devuelven en una lista gigante `List<T>`, sino que se consumen de forma asíncrona fila por fila a medida que se leen del disco.

### 2.2 Despacho Asíncrono en Hilo Secundario con `Task.Run`
En el ViewModel de importación, el proceso se delega al ThreadPool para dejar el hilo de UI completamente libre a 60 FPS:

```csharp
[RelayCommand]
private async Task IniciarImportacionAsync()
{
    EstaProcesando = true;
    ProgresoPorcentaje = 0;

    // Reporte de progreso desacoplado hacia el Dispatcher de WPF
    var progressHandler = new Progress<ProgresoImportacionDto>(reporte =>
    {
        ProgresoPorcentaje = reporte.Porcentaje;
        FilasProcesadasTexto = $"Procesadas {reporte.FilasProcesadas} de {reporte.TotalFilas}";
    });

    try
    {
        // LEY 4: Delegación a hilo secundario con Task.Run
        var resultado = await Task.Run(() => 
            _proveedorService.ImportarListaPreciosAsync(RutaArchivoExcel, ProveedorId, progressHandler));

        MensajeExito = $"Importación finalizada con éxito. Actualizados: {resultado.Actualizados}";
    }
    finally
    {
        EstaProcesando = false;
    }
}
```

### 2.3 Recálculo Automático por Markup de la Raíz `Articulo`
Cuando una fila del Excel coincide con un artículo existente vinculado al proveedor, se actualiza el costo de reposición y se **recalcula automáticamente el precio de venta en mostrador** aplicando su margen pactado:

```csharp
// Método de negocio en Articulo.cs (Dominio)
public void ActualizarCostoYRecalcularPrecio(decimal nuevoCosto)
{
    if (nuevoCosto <= 0)
        throw new DomainException("El costo de reposición debe ser mayor a cero.");

    CostoCompra = nuevoCosto;
    
    // Formula de markup: PrecioVenta = Costo * (1 + PorcentajeGanancia / 100)
    PrecioVenta = Math.Round(CostoCompra * (1 + (PorcentajeGanancia / 100m)), 2);
    UpdatedAt = DateTime.UtcNow;
}
```

---

## 3. 📊 Diagrama Explicativo: Arquitectura de Streaming y Reporte UI

```mermaid
flowchart TD
    subgraph UI_Thread["Hilo Principal de UI (STA Dispatcher a 60 FPS)"]
        VIEW["ImportadorView.xaml\n(Barra de Progreso + Cancelar)"]
        VM["ImportadorViewModel\n(Escucha IProgress<T>)"]
        VIEW <-->|DataBinding| VM
    end

    subgraph ThreadPool["Hilo Secundario (Task.Run / Background Worker)"]
        PARSER["MiniExcel Streaming Parser\n(Lee archivo .xlsx en disco búfer < 25 MB)"]
        SVC["ProveedorService.ImportarListaPreciosAsync"]
        DOM["Articulo.ActualizarCostoYRecalcularPrecio()"]
        
        PARSER -->|Emite Fila por Fila| SVC
        SVC -->|Aplica Markup| DOM
        SVC -.->|IProgress.Report()| VM
    end

    subgraph Database["SQL Server LocalDB"]
        BATCH["Guardado por Lotes (Chunks de 500 filas)\nUnitOfWork.SaveChangesAsync()"]
        DOM --> BATCH
    end
```

---

## 4. ⚖️ Decisiones de Diseño y Trade-offs

### ¿Por qué MiniExcel en vez de OpenXML o ClosedXML?
* **ClosedXML:** Carga el archivo completo en memoria. Un catálogo mayorista de librería con 25.000 filas consumiría más de $400\text{ MB}$ de RAM, violando el requisito no funcional `RNF-03` ($\le 300\text{ MB}$ de memoria total de la app).
* **MiniExcel:** Implementa un lector basado en `XmlReader` con streaming nativo hacia adelante. Procesa el archivo en bloques mínimos de memoria, reduciendo el consumo a menos de $25\text{ MB}$ y ejecutando la importación en una fracción del tiempo ($< 3\text{ segundos}$ para 10.000 filas).

### ¿Por qué actualizar el precio de venta automáticamente por Markup?
* En un escenario inflacionario, si el costo mayorista de una caja de bolígrafos sube de $1.000 a $1.200 y el sistema solo actualizara el costo sin ajustar el precio de venta ($1.500), el margen comercial de la librería se erosionaría de inmediato.
* El recálculo automático garantiza que el margen de ganancia porcentual pactado se mantenga siempre intacto sin requerir edición manual artículo por artículo.

---

## 5. 🎓 Preguntas de Examen de Cátedra (Autoevaluación)

### Pregunta 1: *"¿Cómo garantizan que la lectura de un archivo Excel con 30.000 filas no bloquee la interfaz gráfica de WPF?"*
> **Respuesta Modelo del Estudiante:**  
> "Mediante la aplicación estricta de la **Ley 4 de nuestra arquitectura**: ninguna tarea pesada de I/O o procesamiento masivo se ejecuta en el `UI Dispatcher` (el hilo principal STA de WPF).  
> La invocación del servicio de importación se envuelve en `Task.Run()`, derivando la ejecución a un hilo secundario del *ThreadPool*. Para mantener al usuario informado sobre el avance sin congelar la ventana, utilizamos el patrón estándar de .NET `IProgress<T>`. El hilo secundario emite periódicamente reportes de avance que el framework despacha automáticamente al hilo de UI para actualizar la barra de progreso y el contador de filas sin tirones visuales ni caídas en la tasa de refresco a $60\text{ fps}$."

### Pregunta 2: *"¿Cuál es la diferencia arquitectónica fundamental entre el procesamiento DOM y el procesamiento por Streaming en lectura de archivos?"*
> **Respuesta Modelo del Estudiante:**  
> "En el **procesamiento DOM** (como el que implementan librerías como ClosedXML), el motor parsea el archivo XML comprimido completo y crea en memoria un grafo gigante de objetos representando cada celda, fila, estilo y formato de la hoja. La complejidad espacial es $O(n)$ proporcional al tamaño del archivo, disparando el consumo de memoria RAM.  
> En el **procesamiento por Streaming** (implementado por MiniExcel), el motor lee el archivo como un flujo continuo hacia adelante (*Forward-Only*), manteniendo en memoria únicamente la fila que se está evaluando en ese instante. En cuanto la fila se procesa, se descarta para el Garbage Collector. La complejidad espacial es $O(1)$ (constante), consumiendo menos de $25\text{ MB}$ de memoria independientemente de la cantidad de filas que contenga la planilla."
