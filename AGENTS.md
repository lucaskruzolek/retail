# Instrucciones y Barandillas para Agentes de Código (AGENTS.md)

### Proyecto: Retail (Sistema ERP & Punto de Venta para Librería)
**Plataforma:** .NET 8 LTS | C# 12 | WPF (Windows) | Entity Framework Core 8 | SQL Server Express / LocalDB  
**Arquitectura:** Clean Desktop Monolith con DDD (Domain-Driven Design) y MVVM  
**Contexto:** Proyecto de cátedra universitaria. El código debe ser pedagógico, riguroso, fuertemente tipado y sin atajos técnicos.

---

## 🎯 Protocolo Inicial de Lectura (Paso Cero Obligatorio)

Antes de buscar archivos o escribir código, todo agente debe:
1. **Consultar el Mapa Semántico:** Leer [`docs/MAPA_DEL_PROYECTO.md`](file:///c:/Users/lucas/Proyectos/retail/docs/MAPA_DEL_PROYECTO.md) para ubicar en qué capa, archivo y clase reside la responsabilidad deseada. **No realices búsquedas a ciegas con grep si el archivo está indexado en el mapa.**
2. **Consultar el Roadmap:** Leer [`docs/Roadmap de Implementacion.md`](file:///c:/Users/lucas/Proyectos/retail/docs/Roadmap%20de%20Implementacion.md) para identificar la etapa actual y los entregables esperados. No implementes código de etapas futuras si los contratos base no están listos.

---

## ⚖️ Las 7 Leyes Inviolables de la Arquitectura

### 1. Regla de Dependencia Estricta (Clean Architecture)
* La jerarquía de dependencias es unidireccional y está forzada por el compilador:
  $$\text{Retail.App} \longrightarrow \text{Retail.Application} \longleftarrow \text{Retail.Infrastructure}$$
  $$\text{Retail.Application} \longrightarrow \text{Retail.Domain}$$
* `Retail.Domain` **no tiene dependencias externas** (ni NuGet, ni EF Core, ni WPF).
* `Retail.Application` solo depende de `Domain`, `FluentValidation` y abstracciones de DI.
* `Retail.App` (UI) jamás interactúa con `RetailDbContext` ni ejecuta consultas a la base de datos. Toda operación pasa por ViewModels que invocan servicios de `Retail.Application`.

### 2. Patrones Tácticos DDD y Raíces de Agregado
* **Solo las Raíces de Agregado (`IAggregateRoot`) tienen Repositorio:**  
  La persistencia se restringe a `IRepository<T> where T : BaseEntity, IAggregateRoot`.
* **Prohibición de Repositorios para Entidades Internas:**  
  Está estrictamente prohibido crear repositorios como `DetalleVentaRepository` o `PagoVentaRepository`. Las entidades secundarias se manipulan exclusivamente a través de los métodos de su raíz (`Venta.AgregarItem(...)`, `Venta.ImputarPago(...)`).

### 3. Borrado Lógico Obligatorio (*Soft Delete*)
* En sistemas comerciales y de facturación fiscal, el borrado físico (`DELETE FROM ...`) está prohibido.
* Toda entidad hereda de [`BaseEntity`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Domain/Common/BaseEntity.cs). Para dar de baja, se invoca `entity.MarkAsDeleted()`. Entity Framework Core filtra automáticamente estos registros mediante *Global Query Filters* (`HasQueryFilter(e => !e.IsDeleted)`).

### 4. Responsividad de UI y Dispatcher de WPF
* Ninguna operación de base de datos, lectura de archivos Excel o comunicación HTTP con `arcasdk` debe ejecutarse en el hilo principal de la interfaz (`UI Dispatcher`).
* Utilizar siempre llamadas asíncronas (`async/await`) y delegar tareas pesadas a `Task.Run` para garantizar la fluidez de mostrador ($< 15\text{ ms}$).

### 5. Cero Tolerancia a Advertencias (`TreatWarningsAsErrors`)
* El archivo [`Directory.Build.props`](file:///c:/Users/lucas/Proyectos/retail/Directory.Build.props) impone `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` y `<Nullable>enable</Nullable>`.
* Código con variables nulas no gestionadas, advertencias del compilador o `using` innecesarios provocará el fallo inmediato de la compilación.

### 6. Aislamiento de Hardware y Facturación Fiscal (Mocks)
* Para pruebas y desarrollo local, no asumas la existencia física de una impresora térmica ni certificados fiscales de AFIP/ARCA:
  * Utilizar `FileDebugTicketPrinterService` para verificar la salida de tickets de texto.
  * Mantener `"UseMockArca": true` en [`appsettings.json`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/appsettings.json) para simular la obtención de CAE.

### 7. Estilo y Convenciones de Código (.editorconfig)
* Estilo de llaves **Allman** (llaves en nueva línea).
* Indentación de **4 espacios** en C# y XAML; **2 espacios** en JSON y YAML.
* Nomenclatura BDD para pruebas unitarias: `Metodo_Condicion_ResultadoEsperado` (permitido mediante exención en `Directory.Build.props`).

---

## 🔄 Bucle de Verificación Obligatorio (*Inner Loop* del Agente)

Antes de dar por concluida cualquier modificación o nueva funcionalidad, el agente **debe ejecutar y validar localmente la siguiente secuencia de comandos**:

```powershell
# 1. Compilación en Release con cero advertencias
dotnet build Retail.sln --configuration Release

# 2. Ejecución de la suite completa de pruebas unitarias e integración
dotnet test Retail.sln --configuration Release --no-build

# 3. Verificación estricta de formato y estilo de código
dotnet format Retail.sln --verify-no-changes
```

> [!CAUTION]
> Si cualquiera de estos tres comandos devuelve código de salida distinto de `0`, la tarea **no está terminada**. El agente debe corregir los errores antes de responder al usuario.

---

## 🛠️ Stack Tecnológico de Referencia

| Componente | Paquete / Tecnología | Versión |
| :--- | :--- | :--- |
| **Runtime & SDK** | .NET 8 LTS / C# 12 | 8.0.x / SDK 10+ |
| **Frontend de Escritorio** | WPF con CommunityToolkit.Mvvm | 8.3.2 |
| **Inyección de Dependencias** | Microsoft.Extensions.Hosting | 8.0.1 |
| **ORM & Base de Datos** | Entity Framework Core (SQL Server / LocalDB) | 8.0.11 |
| **Validaciones** | FluentValidation | 11.11.0 |
| **Planillas de Distribuidores**| MiniExcel (Streaming de bajo consumo) | 1.34.2 |
| **Criptografía** | BCrypt.Net-Next | 4.0.3 |
| **Testing** | xUnit, FluentAssertions (6.12.2), NSubstitute (5.3.0)| Últimas estables |
