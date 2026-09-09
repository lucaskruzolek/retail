# Sistema de Logging y Manejo Global de Excepciones: Resiliencia en Mostrador

### Módulo: 04. Sistemas Transversales
**Audiencia:** Desarrolladores, estudiantes universitarios y mesa evaluadora de cátedra  
**Manual Normativo de Referencia:** [`docs/SISTEMA_DE_LOGGING.md`](file:///c:/Users/lucas/Proyectos/retail/docs/SISTEMA_DE_LOGGING.md)  
**Tecnologías:** Serilog 4.2.0 | `Microsoft.Extensions.Logging` | WPF STA Dispatcher  
**Archivos de Código:**
* Host y Trampas: [`App.xaml.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/App.xaml.cs)
* Diálogo de Error: [`UnhandledExceptionDialog.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Dialogs/UnhandledExceptionDialog.xaml)

---

## 1. 📖 Fundamento Teórico y Académico

### 1.1 Observabilidad en Sistemas de Escritorio
A diferencia de los servidores web en la nube (donde un error 500 solo afecta a una petición HTTP aislada), en una aplicación de escritorio de mostrador:
* **El peligro del "Crash to Desktop":** Una excepción no capturada en el hilo principal de la interfaz provoca la muerte instantánea del proceso (`Crash`). La ventana desaparece, el cliente queda esperando en la fila y la transacción queda a medio completar.
* **Diagnóstico Post-Mortem:** Si un cajero reporta que "el sistema se cerró solo", sin un archivo de bitácora rotativo y estructurado, el equipo de desarrollo no tiene forma de reproducir el bug ni identificar la causa raíz.

### 1.2 Inversión de Dependencias (DIP) y Logging Estructurado
El sistema aplica rigurosamente el principio de Inversión de Dependencias:
1. **Desacoplamiento Absoluto:** Las capas de Dominio, Aplicación e Infraestructura jamás referencian la clase estática de Serilog (`Serilog.Log`). En su lugar, inyectan la abstracción estándar de .NET **`Microsoft.Extensions.Logging.ILogger<T>`**.
2. **Logging Estructurado (*Structured Logging*):** En lugar de concatenar cadenas de texto plano, los mensajes se registran mediante *Message Templates* (`"Venta #{VentaId} registrada por ${Total}"`). Esto preserva las propiedades como metadatos indexables, permitiendo búsquedas semánticas precisas.

---

## 2. 💻 Aplicación Concreta en el Código de Retail

### 2.1 Configuración de Serilog y Rotación de Archivos
En [`src/Retail.App/App.xaml.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/App.xaml.cs), Serilog se configura como el sumidero (*sink*) principal con rotación diaria y retención acotada:

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/retail-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 31,     // Conserva un mes de historial (RNF-03)
        fileSizeLimitBytes: 10_485_760, // 10 MB por archivo
        rollOnFileSizeLimit: true)
    .CreateBootstrapLogger();
```

### 2.2 Las 3 Trampas Globales de Excepciones
En [`App.xaml.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/App.xaml.cs), registramos tres escuchadores de emergencia que cubren el 100% de los hilos del proceso:

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);

    // Trampa 1: Excepciones no controladas en el hilo STA de la interfaz de usuario
    DispatcherUnhandledException += OnDispatcherUnhandledException;

    // Trampa 2: Excepciones en hilos secundarios o del ThreadPool
    AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

    // Trampa 3: Tareas asíncronas huérfanas no observadas
    TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
}
```

### 2.3 Salvaguarda de Mostrador: `OnDispatcherUnhandledException`
Cuando ocurre un fallo imprevisto en la UI, interceptamos el evento, registramos el error en Serilog y mostramos una ventana de diálogo amigable en lugar de permitir la caída de la aplicación:

```csharp
private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
{
    Log.Fatal(e.Exception, "Excepción crítica no controlada en el UI Dispatcher de WPF");

    // Despliega el diálogo Fluent en el hilo principal
    var dialog = new UnhandledExceptionDialog(e.Exception);
    dialog.ShowDialog();

    // SALVAGUARDA DE MOSTRADOR: Marca el error como gestionado
    // para evitar que Windows cierre abruptamente el proceso
    e.Handled = true;
}
```

### 2.4 El Diálogo de Resiliencia: `UnhandledExceptionDialog`
En [`src/Retail.App/Views/Dialogs/UnhandledExceptionDialog.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Dialogs/UnhandledExceptionDialog.xaml):
* Hereda de `ui:FluentWindow` y aplica material **Mica**.
* Muestra un mensaje amigable al operador ("Ocurrió un inconveniente técnico inesperado").
* Ofrece un botón de **"Copiar Detalle Técnico"** que copia al portapapeles el stack trace, la versión del ejecutable y la hora exacta para enviarla por WhatsApp o correo al equipo de soporte.

---

## 3. 📊 Diagrama Explicativo: Flujo de Resiliencia ante Excepciones

```mermaid
sequenceDiagram
    autonumber
    actor Cajero as Cajero
    participant UI as Ventana de Mostrador (WPF)
    participant Host as App.xaml.cs (Dispatcher Trap)
    participant Log as Serilog (logs/retail-*.log)
    participant Dlg as UnhandledExceptionDialog (Mica)

    UI->>UI: Ocurre fallo inesperado (ej. NullReference o falla de driver)
    Note over UI: La excepción no fue atrapada en el ViewModel
    
    UI->>Host: Dispara DispatcherUnhandledException
    Host->>Log: Log.Fatal(ex, "Excepción no controlada")
    Note over Log: Guarda traza completa, timestamp y contexto en disco

    Host->>Dlg: new UnhandledExceptionDialog(ex).ShowDialog()
    Dlg-->>Cajero: Muestra pantalla amigable con botón "Copiar Informe"
    
    Cajero->>Dlg: Presiona "Continuar Operación"
    Dlg-->>Host: Cierra el diálogo
    
    Host->>Host: e.Handled = true
    Note over Host,UI: El proceso NO muere; el cajero puede seguir cobrando
```

---

## 4. ⚖️ Decisiones de Diseño y Trade-offs

### ¿Por qué la Ley 1 de Logging prohíbe interpolar cadenas (`$"..."`)?
* **Pérdida de Estructura:** Si escribimos `LogInformation($"Venta {id} por {total}")`, el mensaje se compila como un texto plano simple. En una herramienta de diagnóstico, es imposible filtrar consultas como `"todas las ventas donde Total > 50000"`.
* **Sobrecarga de Memoria:** La interpolación de cadenas crea nuevos objetos `string` en el heap de memoria en cada escaneo de producto, forzando pausas innecesarias en el Garbage Collector. Con plantillas (`"Venta {Id} por {Total}"`), Serilog optimiza la alocación de memoria.

### ¿Por qué `e.Handled = true` en lugar de dejar caer la aplicación?
* En un mostrador con clientes esperando, un crash intempestivo es el peor escenario operativo posible.
* Marcar `e.Handled = true` mantiene la ventana activa, permitiendo al cajero cerrar el turno ordenadamente, reintentar la operación o consultar al encargado sin perder el trabajo acumulado.

---

## 5. 🎓 Preguntas de Examen de Cátedra (Autoevaluación)

### Pregunta 1: *"¿Por qué está prohibido invocar la clase estática `Serilog.Log` dentro de los servicios de la capa de Aplicación?"*
> **Respuesta Modelo del Estudiante:**  
> "Porque violaría el **Principio de Inversión de Dependencias (DIP)** de SOLID y la jerarquía de dependencias de Clean Architecture. La capa de Aplicación debe depender únicamente de abstracciones y no de una librería de logging concreta de terceros.  
> Al inyectar la interfaz estándar `Microsoft.Extensions.Logging.ILogger<T>`, la capa de negocio no sabe ni le importa qué motor escribe los logs. `Serilog` queda relegado a su rol natural de infraestructura en el punto de entrada (`App.xaml.cs`). Si el día de mañana cambiamos Serilog por NLog o Application Insights, no tenemos que alterar ni una sola línea de los servicios de negocio ni de las pruebas unitarias."

### Pregunta 2: *"¿Cómo evita el sistema de logging que los archivos en disco crezcan indefinidamente hasta saturar el disco rígido de la caja registradora?"*
> **Respuesta Modelo del Estudiante:**  
> "A través de dos políticas complementarias configuradas en Serilog:  
> 1. **Límite de Tamaño y Rotación:** Se establece un tope de $10\text{ MB}$ por archivo con `fileSizeLimitBytes: 10_485_760` y la directiva `rollOnFileSizeLimit: true`. Si en un día de mucho tráfico se generan muchos registros, el log se divide en partes numeradas.  
> 2. **Retención Acotada:** Se define `retainedFileCountLimit: 31` con intervalo diario (`RollingInterval.Day`). De este modo, Serilog purga automáticamente los archivos de log con más de 31 días de antigüedad, manteniendo el consumo en disco acotado a menos de $300\text{ MB}$ en todo momento (`RNF-03`)."
