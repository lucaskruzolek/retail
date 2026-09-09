# Preguntas Trampa de Profesores: Guía de Defensa Oral de Cátedra

### Módulo: 06. Vademécum para la Defensa Oral de Cátedra
**Audiencia:** Lucas Kruzolek, Pablo Fernandez y mesa evaluadora de cátedra  
**Objetivo Pedagógico:** Proveer argumentos rigurosos, citas formales de ingeniería de software y respuestas modelo irrebatibles para la defensa técnica oral del proyecto frente a docentes universitarios.  
**Misión Anti-Vibecoding:** Demostrar dominio conceptual exhaustivo sobre cada decisión de diseño adoptada en `Retail.sln`.

---

## 🏛️ Bloque 1: Arquitectura de Software y Clean Architecture

### Pregunta 1.1: *"En pleno 2026, ¿por qué eligieron una arquitectura monolítica de escritorio en lugar de una arquitectura de microservicios o una aplicación web moderna en la nube?"*

* **🎯 Qué busca evaluar el docente:**  
  Detectar si el estudiante se deja llevar por modas tecnológicas (*Hype-Driven Development*) o si sabe seleccionar la arquitectura adecuada en función de los requerimientos no funcionales del negocio (*Architectural Fitness*).
* **📚 Citas de Respaldo:**  
  *Martin Fowler* (*MonolithFirst*), *L. Peter Deutsch* (*The 8 Fallacies of Distributed Computing*) y la *Ley de Amdahl*.
* **💡 Respuesta Modelo del Estudiante:**  
  > "Aplicamos el principio de idoneidad contextual de la ingeniería de software. Un punto de venta de mostrador comercial (*Retail POS*) tiene dos restricciones no funcionales críticas: **latencia ultra-baja ($< 15\text{ ms}$)** para que el escaneo de artículos sea instantáneo y **disponibilidad ininterrumpida ante caídas de conectividad a internet**.  
  > Los microservicios y las aplicaciones web en la nube asumen que la red es confiable y que la latencia es cero (las dos primeras falacias de la computación distribuida). Si se corta internet o hay degradación del ISP, una tienda con backend en la nube queda paralizada sin poder cobrar.  
  > Por eso elegimos un **Monolito de Escritorio Limpio (Clean Desktop Monolith)** en .NET 8: se ejecuta en un solo proceso local con latencias en memoria sub-milisegundo y transacciones ACID locales. Sin embargo, no es un 'monolito espagueti': está rigurosamente desacoplado en 4 capas concéntricas conforme a la Clean Architecture de Robert C. Martin, lo que nos otorga la simplicidad operativa de un monolito y la mantenibilidad modular de un sistema desacoplado."

---

### Pregunta 1.2: *"Si el cliente decide abrir una segunda caja registradora en el mismo local comercial o expandirse a otra sucursal, ¿tienen que tirar todo su software a la basura?"*

* **🎯 Qué busca evaluar el docente:**  
  Verificar si el acoplamiento con la base de datos local es destructivo o si existe verdadera inversión de dependencias.
* **📚 Citas de Respaldo:**  
  *Dependency Inversion Principle (DIP)* de SOLID y *Ports and Adapters* de Alistair Cockburn.
* **💡 Respuesta Modelo del Estudiante:**  
  > "Absolutamente no. Gracias a la Clean Architecture, la capa de Aplicación y la capa de Dominio son **100% ciegas a la ubicación física de la base de datos**. Consumen la interfaz `IRepository<T>` y `IUnitOfWork`.  
  > Si la librería abre una segunda caja registradora en el mismo local, solo se instala una instancia de SQL Server Express compartida en la red local y se actualiza la cadena de conexión `RetailDbConnection` en el archivo [`appsettings.json`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/appsettings.json) de cada terminal. No se modifica ni una sola línea de código C#.  
  > Si el comercio se expandiera a una cadena con sincronización centralizada en la nube, la capa de Dominio permanece intacta y solo se implementaría un adaptador de sincronización en la capa de Infraestructura."

---

### Pregunta 1.3: *"¿Por qué la capa de Dominio (`Retail.Domain`) no tiene instalado el paquete NuGet de Entity Framework Core?"*

* **🎯 Qué busca evaluar el docente:**  
  Evaluar si el alumno comprende el principio de **Persistencia Ignorante (*Persistence Ignorance*)**.
* **📚 Citas de Respaldo:**  
  *Robert C. Martin* (*Clean Architecture: A Craftsman's Guide to Software Structure and Design*).
* **💡 Respuesta Modelo del Estudiante:**  
  > "Porque en Clean Architecture las dependencias apuntan exclusivamente hacia adentro: el Dominio es el núcleo de mayor nivel de abstracción y **no debe depender de ningún detalle tecnológico exterior**.  
  > Si agregáramos EF Core al Dominio, comenzaríamos a ensuciar las entidades con anotaciones como `[Table]`, `[Key]` o `[ForeignKey]`, acoplando nuestras reglas comerciales puras a las particularidades de un ORM específico.  
  > En Retail, el Dominio es C# puro (.NET 8 básico). Todas las configuraciones relacionales, tipos de datos SQL, nombres de tablas e índices filtrados se configuran por fuera, en la capa de `Retail.Infrastructure`, mediante **Fluent API** (`IEntityTypeConfiguration<T>`). Si mañana Microsoft discontinuara EF Core o decidiéramos migrar a otro mecanismo de persistencia, nuestro núcleo de negocio permanecería 100% inalterado."

---

## 💾 Bloque 2: Persistencia, Bases de Datos y SQL Server

### Pregunta 2.1: *"En su base de datos no veo que ejecuten sentencias `DELETE`. ¿Por qué implementaron borrado lógico (`Soft Delete`) y qué impacto tiene en los índices únicos?"*

* **🎯 Qué busca evaluar el docente:**  
  Conocimiento de normativas contables/fiscales y el manejo de restricciones de integridad referencial.
* **📚 Citas de Respaldo:**  
  *Ley de Facturación y Código de Comercio*, y *C.J. Date* (*An Introduction to Database Systems*).
* **💡 Respuesta Modelo del Estudiante:**  
  > "En un sistema comercial y ERP, el borrado físico (`DELETE FROM`) es una mala práctica inaceptable. Destruye la trazabilidad histórica de comprobantes exigida por la AFIP y viola la integridad referencial: no se puede borrar físicamente un artículo que ya fue vendido hace tres meses sin romper la clave foránea en `detalle_ventas` o ejecutar un destructivo borrado en cascada.  
  > Toda entidad hereda de [`BaseEntity.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Domain/Common/BaseEntity.cs) con las propiedades `IsDeleted` y `DeletedAt`. Mediante **Global Query Filters** en `RetailDbContext`, EF Core inyecta automáticamente `WHERE [IsDeleted] = 0` en todas las consultas del sistema.  
  > Respecto al impacto en los índices únicos, un borrado lógico estándar genera conflictos si se intenta reinsertar un artículo con el mismo código. En Retail lo solucionamos aplicando **Índices Filtrados (*Filtered Indexes*)** en SQL Server: `builder.HasIndex(a => a.CodigoBarras).IsUnique().HasFilter("[codigo_barras] IS NOT NULL AND [is_deleted] = 0")`, garantizando que la restricción de unicidad solo aplique a registros activos."

---

### Pregunta 2.2: *"¿Por qué la Ley 8 de su arquitectura prohíbe terminantemente materializar datos en memoria con `.ToList()` antes de filtrar?"*

* **🎯 Qué busca evaluar el docente:**  
  Diferencia entre evaluación en servidor relacional (*Server Evaluation / Push-down*) y evaluación en cliente (*Client Evaluation*).
* **📚 Citas de Respaldo:**  
  Documentación oficial de EF Core y el requisito no funcional de memoria RAM $\le 300\text{ MB}$ (`RNF-03`).
* **💡 Respuesta Modelo del Estudiante:**  
  > "Invocar `.ToList()` prematuramente fuerza a Entity Framework Core a ejecutar un `SELECT *` de la tabla entera y transformar miles de filas en objetos C# en la memoria RAM del proceso. Si el catálogo tiene 20.000 artículos, traerlos a la memoria para filtrarlos con LINQ to Objects consumiría cientos de megabytes, saturaría el recolector de basura (*Garbage Collector*) y provocaría caídas de rendimiento en el mostrador.  
  > Al mantener las consultas sobre **`IQueryable`**, aplicamos el principio **Push-down to SQL**: la traducción de las cláusulas `Where`, `OrderBy`, `Take` y funciones de coincidencia (`EF.Functions.Like`) se traslada al motor de SQL Server LocalDB. El motor relacional utiliza sus índices B-Tree y su caché optimizado para resolver la búsqueda en microsegundos, retornando a la memoria de la aplicación únicamente la página de 20 o 50 registros que el cajero necesita visualizar."

---

## 🎨 Bloque 3: Presentación, Concurrencia y el UI Dispatcher de WPF

### Pregunta 3.1: *"¿Qué es el UI Dispatcher en WPF y qué sucedería si una consulta pesada de base de datos se ejecuta en el hilo principal de la aplicación?"*

* **🎯 Qué busca evaluar el docente:**  
  Comprensión del modelo de subprocesos STA (*Single-Threaded Apartment*) y la asincronía con `async/await`.
* **📚 Citas de Respaldo:**  
  *Jeffrey Richter* (*CLR via C#*) y el requisito de fluidez de mostrador $< 15\text{ ms}$.
* **💡 Respuesta Modelo del Estudiante:**  
  > "WPF opera bajo el modelo de apartamento de subproceso único (STA). Existe un único hilo principal (el hilo de interfaz) que procesa continuamente la cola de mensajes de Windows (`GetMessage`/`DispatchMessage`) a una tasa de $60\text{ fotogramas por segundo}$.  
  > Si un desarrollador ejecuta una consulta síncrona a la base de datos o lee un archivo Excel masivo en ese hilo, el bucle del Dispatcher se bloquea. El sistema operativo Windows marca la ventana como *'No responde'*, las animaciones se congelan y, lo más grave en una librería comercial, el búfer de entrada del teclado pierde caracteres del lector láser de código de barras.  
  > Por eso nuestra **Ley 4** exige que toda operación de I/O sea asíncrona (`async/await`) y que los procesos pesados de importación se deriven a hilos del ThreadPool mediante `Task.Run()`, manteniendo el Dispatcher libre en todo momento."

---

### Pregunta 3.2: *"¿Qué son los Source Generators de C# y por qué los prefirieron frente a librerías de Dynamic Proxy o la escritura manual de `INotifyPropertyChanged`?"*

* **🎯 Qué busca evaluar el docente:**  
  Conocimiento sobre la evolución del compilador Roslyn y técnicas de metaprogramación en C# 12.
* **📚 Citas de Respaldo:**  
  Microsoft .NET Community Toolkit Team y arquitectura del compilador Roslyn.
* **💡 Respuesta Modelo del Estudiante:**  
  > "Históricamente, implementar MVVM requería escribir entre 15 y 20 líneas de código repetitivo (*boilerplate*) por cada propiedad para disparar `OnPropertyChanged`. Para evitarlo, algunos equipos usaban librerías de modificación binaria en tiempo de ejecución (*IL Weaving* o proxies dinámicos), lo cual degradaba el rendimiento y hacía que el código fuera muy difícil de depurar.  
  > Los **Source Generators** de `CommunityToolkit.Mvvm` operan de forma radicalmente distinta: se ejecutan **durante la compilación**. Inspeccionan los atributos `[ObservableProperty]` y `[RelayCommand]` y emiten código C# puro en clases parciales (`*.g.cs`).  
  > Esto nos da tres ventajas indiscutibles: **cero costo de rendimiento en tiempo de ejecución** (cero reflexión), **tipado fuerte inmediato en el editor** con detección de errores en tiempo de compilación y **depurabilidad total**, ya que podemos colocar breakpoints dentro del código generado por Roslyn."

---

## 🧩 Bloque 4: Domain-Driven Design (DDD) y Reglas de Negocio

### Pregunta 4.1: *"¿Por qué la entidad `DetalleVenta` no tiene un `IDetalleVentaRepository` en su solución?"*

* **🎯 Qué busca evaluar el docente:**  
  Entendimiento de los límites transaccionales de los Agregados en DDD.
* **📚 Citas de Respaldo:**  
  *Eric Evans* (*Domain-Driven Design: Tackling Complexity in the Heart of Software*) y la **Ley 2** de AGENTS.md.
* **💡 Respuesta Modelo del Estudiante:**  
  > "En DDD, un Agregado es un límite de consistencia transaccional gobernado por una única **Raíz de Agregado (*Aggregate Root*)**. La entidad `Venta` es la raíz, mientras que `DetalleVenta` y `PagoVenta` son entidades secundarias internas que no tienen ciclo de vida independiente.  
  > Si creáramos un `DetalleVentaRepository`, permitiríamos que cualquier servicio o ventana invoque `_detalleRepo.DeleteAsync(item)` de forma aislada. Esto rompería las invariantes del negocio: el campo `Total` de la venta no se actualizaría, el stock del artículo no se repondría y el balance fiscal quedaría desfasado.  
  > En nuestro sistema, la persistencia se restringe estrictamente mediante tipado genérico a `IRepository<T> where T : BaseEntity, IAggregateRoot`. Para alterar un ítem, el código está obligado a invocar métodos de la raíz (`venta.AgregarItem(...)` o `venta.EliminarItem(...)`), garantizando la consistencia atómica del agregado completo."

---

### Pregunta 4.2: *"¿Por qué un Presupuesto NO descuenta stock en el momento en que se genera?"*

* **🎯 Qué busca evaluar el docente:**  
  Comprensión de las dinámicas comerciales reales frente a simplificaciones ingenuas de programación.
* **📚 Citas de Respaldo:**  
  Requisito funcional `RF-11` y `RF-12` de la Especificación Formal de Requisitos (ERS).
* **💡 Respuesta Modelo del Estudiante:**  
  > "Porque en una librería minorista, un presupuesto es una mera cotización informativa con precios pactados válidos por 15 días, no una orden de compra en firme.  
  > Si el sistema descontara o reservara stock al presupuestar, un cliente que pide presupuesto por 40 cuadernos escolares para una institución inmovilizaría físicamente la mercadería en la góndola. Si ese cliente finalmente no compra, el comercio pierde ventas reales con otros clientes que entran al local con el dinero en mano.  
  > El stock solo se descuenta de forma atómica cuando el presupuesto se convierte efectivamente en venta en el mostrador mediante el caso de uso `CobrarVentaAsync`."

---

## 🛡️ Bloque 5: Resiliencia, Fallos y Facturación Fiscal

### Pregunta 5.1: *"¿Qué sucede si un rayo o un problema del proveedor de internet deja a la librería sin conexión justo cuando el cajero está por cobrar una venta?"*

* **🎯 Qué busca evaluar el docente:**  
  Capacidad de diseñar sistemas tolerantes a fallos con arquitectura *Offline-First* (`RF-17`).
* **📚 Citas de Respaldo:**  
  Normativa de Contingencia Fiscal de la AFIP/ARCA y patrón *Fallback / Degraded Operation*.
* **💡 Respuesta Modelo del Estudiante:**  
  > "El sistema jamás bloquea la venta ni hace un `ROLLBACK` de la transacción comercial. Un cliente con dinero en mano en la caja de una librería no puede ser rechazado porque un servidor gubernamental o un enlace de internet esté caído.  
  > Nuestro servicio fiscal atrapa la excepción de red y activa el modo de **Contingencia Fiscal (`RF-17`)**: la venta se guarda localmente en SQL Server, el stock se descuenta, se emite un comprobante interno rotulado como *'DOCUMENTO NO FISCAL - CONTINGENCIA'* y el estado de la venta queda asentado como `EstadoFiscalEnum.ErrorFiscalReintentable`.  
  > Posteriormente, cuando el enlace de internet se restablece, el encargado utiliza la **Consola Gerencial de Reintentos (`RF-18`)** para transmitir el lote completo de ventas pendientes a los servidores de AFIP/ARCA con un solo clic, obteniendo el CAE correspondiente de forma ordenada y conforme a la ley."

---

### Pregunta 5.2: *"Si ocurre un error imprevisto de programación (un `NullReferenceException` no atrapado), ¿por qué su aplicación no se cierra intempestivamente?"*

* **🎯 Qué busca evaluar el docente:**  
  Diseño del subsistema de diagnóstico, resiliencia de hilos y experiencia de usuario ante desastres.
* **📚 Citas de Respaldo:**  
  Manual [`docs/SISTEMA_DE_LOGGING.md`](file:///c:/Users/lucas/Proyectos/retail/docs/SISTEMA_DE_LOGGING.md) y arquitectura de trampas de `App.xaml.cs`.
* **💡 Respuesta Modelo del Estudiante:**  
  > "Porque implementamos una arquitectura de **trampas globales en tres niveles** en [`App.xaml.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/App.xaml.cs). En el hilo principal de la interfaz, el evento `DispatcherUnhandledException` intercepta cualquier fallo no gestionado.  
  > En lugar de permitir el colapso del proceso (*Crash to Desktop*), el manejador registra la traza completa en el log diario de Serilog (`logs/retail-.log`), despliega el diálogo no destructivo [`UnhandledExceptionDialog.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Dialogs/UnhandledExceptionDialog.xaml) informando al cajero y permitiéndole copiar el reporte técnico al portapapeles, y finalmente establece **`e.Handled = true`**.  
  > Esto previene que el sistema operativo Windows destruya el proceso, permitiendo al operador cerrar el turno ordenadamente o continuar la operación comercial sin perder el estado de la caja."
