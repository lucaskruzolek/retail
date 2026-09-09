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
3. **Consultar el Sistema de Diseño (Al trabajar en UI):** Si la tarea involucra vistas o controles en `Retail.App`, leer obligatoriamente [`docs/SISTEMA_DE_DISENO.md`](file:///c:/Users/lucas/Proyectos/retail/docs/SISTEMA_DE_DISENO.md) y [`StyleGalleryView.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Dev/StyleGalleryView.xaml) antes de escribir código XAML.

---

## ⚖️ Las 9 Leyes Inviolables de la Arquitectura

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

### 8. Optimización de Persistencia y Delegación al Motor Relacional (Push-down to SQL)
* **Evaluación en Servidor:** Las consultas de búsqueda, filtrado, ordenamiento y agregación deben componerse sobre `IQueryable` para ejecutarse en SQL Server / LocalDB. Queda estrictamente prohibido materializar tablas en memoria con `.ToList()` prematuro para luego filtrar con LINQ to Objects en el cliente.
* **Proyecciones y No-Tracking:** Para consultas de solo lectura en grillas y catálogos, utilizar siempre `.AsNoTracking()` y proyectar directamente a DTOs (`.Select(x => new ...)`), evitando el overhead del Change Tracker de EF Core y manteniendo la RAM $\le 300\text{ MB}$ (`RNF-03`).
* **Traducción Nativa de Funciones:** Utilizar funciones traducibles de EF Core (`EF.Functions.Like`, cotejos de intercalación *collation* insensibles a tildes/mayúsculas) y agregaciones nativas (`SumAsync`, `CountAsync`) antes que implementar comparaciones de texto o cálculos de balance en C#.
* **Frontera Inviolable con el Dominio (CQRS / DDD):** Esta optimización aplica exclusivamente a la capa de persistencia y consultas (lado Lectura). La lógica mutacional de negocio, recálculo de precios por markup, descuento de stock e invariantes de estado pertenecen con exclusividad a las Raíces de Agregado en `Retail.Domain`. Queda prohibido trasladar reglas de negocio a *Stored Procedures*, *Triggers* o funciones escalares de base de datos.

### 9. Fidelidad Estética y Sistema de Diseño (Windows 11 Fluent + Retail)
* Antes de crear, modificar o maquetar cualquier vista, ventana o diálogo XAML en `Retail.App`, el agente debe consultar obligatoriamente [`docs/SISTEMA_DE_DISENO.md`](file:///c:/Users/lucas/Proyectos/retail/docs/SISTEMA_DE_DISENO.md) y [`StyleGalleryView.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Dev/StyleGalleryView.xaml) como living styleguide de referencia.
* Queda terminantemente prohibido implementar interfaces sin respetar los contratos visuales, tokens semánticos, tipografía dual (`Cascadia Code` para importes y `Segoe UI Variable` para interfaz general) y componentes de mostrador allí especificados.

---

## 🧘 Principio de Simplicidad Pragmática y Código Esencial (Adaptación Ponytail)

Para evitar la sobreingeniería, el código muerto y las alucinaciones de boilerplate, todo agente debe regirse por la siguiente disciplina de desarrollo:

### 1. La Escalera de Decisión (Detente en el primer peldaño que resuelva el problema)
1. **¿Es necesario construirlo? (YAGNI):** No implementes requerimientos futuros ni features no solicitadas en la ERS o el Roadmap actual.
2. **¿Ya existe en este codebase?:** Reutiliza helpers, diccionarios XAML, contratos, validadores y excepciones existentes. No dupliques lógica.
3. **¿La biblioteca estándar de .NET 8 / C# 12 ya lo resuelve?:** Usa capacidades nativas del lenguaje y runtime (records, pattern matching, collection expressions, LINQ optimizado) antes de inventar utilidades custom.
4. **¿Una dependencia ya instalada lo cubre?:** Aprovecha al máximo `CommunityToolkit.Mvvm` (source generators), `FluentValidation`, `MiniExcel` y `BCrypt.Net-Next`. Prohibido escribir boilerplate que estas librerías resuelven automáticamente.
5. **Solo entonces:** Escribe el mínimo código limpio, robusto y fuertemente tipado que resuelva la tarea.

### 2. Corrección en la Causa Raíz (*Root Cause, Not Symptom*)
* Un reporte de bug describe un síntoma. Rastrea todos los llamadores de la función o método afectado y corrige la causa compartida en la raíz (en la entidad de dominio o en el servicio de aplicación).
* Colocar un parche superficial en un único ViewModel o manejador de evento deja a los demás llamadores con el bug latente e introduce inconsistencias en el sistema.

### 3. Reglas Antiproliferación de Código
* **Cero abstracciones no solicitadas:** No crees interfaces, fábricas o capas intermedias no requeridas explícitamente por la arquitectura.
* **Cero dependencias nuevas:** Prohibido agregar paquetes NuGet sin justificación y aprobación previa.
* **Cero boilerplate innecesario:** Prefiere soluciones directas y predecibles (*Boring over clever*).
* **Menor diff funcional posible:** La solución más concisa y correcta que resuelva el problema de raíz. Un cambio apresurado en el lugar incorrecto es un segundo bug.

### 4. Lo No Negociable (Rigor de Cátedra Universitaria)
* **Entendimiento previo:** Lee la tarea y recorre el flujo completo de punta a punta antes de escribir código.
* **Validación en fronteras:** Validación estricta en la capa de aplicación con `FluentValidation`.
* **Cero atajos técnicos:** Queda prohibido recortar esquinas con soluciones precarias (como bloqueos globales, escaneos $O(n^2)$ o heurísticas frágiles).
* **Integridad de Capas y Archivos:** "Menor diff" jamás significa colapsar las capas de Clean Architecture o amontonar clases en un solo archivo. Cada responsabilidad se ubica en su propio archivo conforme a [`docs/MAPA_DEL_PROYECTO.md`](file:///c:/Users/lucas/Proyectos/retail/docs/MAPA_DEL_PROYECTO.md).
* **Testing Formal con xUnit:** Todo código no trivial debe estar respaldado por pruebas unitarias o de integración en xUnit (`Retail.*.UnitTests`), con FluentAssertions y nomenclatura BDD (`Metodo_Condicion_ResultadoEsperado`). No se admiten scripts ad-hoc ni asserts informales.
* **Claridad Pedagógica:** Código legible, auto-documentado y respetando el estilo Allman de `.editorconfig`. No sacrifiques legibilidad por compactar código en líneas únicas (*one-liners*) incomprensibles.

---

## 🔄 Bucle de Verificación Inteligente (*Inner Loop* del Agente)

El bucle de verificación técnica no debe ejecutarse a ciegas. Su aplicación queda sujeta al siguiente criterio de **discreción inteligente**:

### 1. Condiciones de Disparo y Excepciones
* **Aplica Exclusivamente a Código Compilable:** Se ejecuta únicamente cuando la tarea involucre modificaciones en archivos de código fuente C# (`.cs`), interfaces XAML (`.xaml`), configuraciones de proyecto (`.csproj`) o directivas de compilación (`Directory.Build.props`, `.editorconfig`).
* **Exención Estricta (No ejecutar bucle):** Quedan **estrictamente exceptuados** cambios que solo involucren archivos de documentación (`.md`), diagramas Mermaid (`.mmd`), control de versiones (`.gitignore`) o tareas meramente analíticas, explicativas y de planificación. En estos casos, **está prohibido disparar compilaciones y suites de tests innecesarias**.

### 2. Discreción de Alcance (Ciclo Focalizado vs. Verificación Completa)
* **Durante el desarrollo iterativo (Ciclo Rápido):** Si se modifica un componente aislado (por ejemplo, una entidad en `Retail.Domain` o un servicio en `Retail.Application`), el agente tiene discreción para ejecutar únicamente la suite de tests correspondiente (ej. `dotnet test tests/Retail.Domain.UnitTests/`) para obtener feedback inmediato sin demoras.
* **Cierre de Etapa o Hito Completo (Verificación Release):** Al dar por concluida una etapa del Roadmap (ej. Etapa 0.6, Etapa 1.1), implementar un módulo completo o previo a un PR, el agente debe ejecutar la validación integral:

```powershell
# 1. Compilación en Release con cero advertencias
dotnet build Retail.sln --configuration Release

# 2. Ejecución de la suite completa de pruebas unitarias e integración
dotnet test Retail.sln --configuration Release --no-build

# 3. Verificación estricta de formato y estilo de código
dotnet format Retail.sln --verify-no-changes
```

### 3. Sincronización Obligatoria del Mapa Semántico
Si la tarea agregó, renombró o eliminó archivos estructurales (entidades, enumeraciones, excepciones de dominio, contratos de interfaz, DTOs, vistas XAML o flujos de CI/CD), el agente **debe actualizar [`docs/MAPA_DEL_PROYECTO.md`](file:///c:/Users/lucas/Proyectos/retail/docs/MAPA_DEL_PROYECTO.md)** indexando las nuevas responsabilidades y enlaces de archivo antes de responder al usuario.

> [!CAUTION]
> En verificaciones de código compilable, si cualquiera de los comandos devuelve un código de salida distinto de `0`, o si se omitió la actualización de [`docs/MAPA_DEL_PROYECTO.md`](file:///c:/Users/lucas/Proyectos/retail/docs/MAPA_DEL_PROYECTO.md) ante cambios estructurales, la tarea **no está terminada**. El agente debe corregir los errores antes de notificar al usuario.

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
