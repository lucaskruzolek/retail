# 03. Patrones de Diseño (GoF y DDD Táctico)

Este módulo cataloga, analiza y explica los patrones de diseño aplicados en la solución `Retail.sln`, combinando su fundamentación teórica formal con su implementación real en C# 12 / .NET 8, diagramas Mermaid y preguntas de evaluación de cátedra:

---

## 🧭 Catálogo de Patrones Documentados

| Patrón / Mecanismo | Clasificación | Problema que Resuelve en Retail | Enlace al Artículo |
| :--- | :--- | :--- | :--- |
| **Repository & Unit of Work** | DDD Táctico / Enterprise Pattern | Desacopla la persistencia de los casos de uso, fuerza los límites de las raíces de agregado (`IAggregateRoot`) y coordina transacciones atómicas ACID. | [Ver Artículo](repository-and-unit-of-work.md) |
| **MVVM con Source Generators** | Presentación / GoF (*Observer* + *Command*) | Elimina el 80% del boilerplate de `INotifyPropertyChanged` mediante generación de código Roslyn en tiempo de compilación con `CommunityToolkit.Mvvm`. | [Ver Artículo](mvvm-con-source-generators.md) |
| **Adapter & Façade** | GoF Estructural | Aísla el hardware físico de mostrador (`FileDebugTicketPrinterService`) y el servicio de AFIP/ARCA (`MockArcaClient`), habilitando pruebas herméticas en CI/CD. | [Ver Artículo](adapter-y-facade.md) |
| **Soft Delete & Global Query Filters** | Persistencia / Temporal Data | Custodia la inalterabilidad contable prohibiendo el borrado físico (`DELETE`), inyectando automáticamente `WHERE [IsDeleted] = 0` en todas las consultas. | [Ver Artículo](soft-delete-y-global-filters.md) |
| **Strategy & Contingencia Fiscal** | GoF Comportamiento / Offline-First | Asegura que la librería nunca detenga sus cobros en mostrador si AFIP/ARCA cae, emitiendo comprobantes de contingencia reintentables (`RF-17`, `RF-18`). | [Ver Artículo](strategy-y-contingencia.md) |

---

## 📐 Estructura de Estudio
Cada artículo cuenta con:
1. **Fundamento Teórico:** Definición formal según Fowler, Evans, Martin o Gang of Four.
2. **Código Real:** Enlaces y fragmentos de `src/` comentados pedagógicamente.
3. **Diagramas Mermaid:** Flujos de secuencia y relaciones estructurales.
4. **Decisiones y Trade-offs:** Por qué se descartaron soluciones ingenuas o alternativas.
5. **Preguntas de Examen:** Autoevaluación para la mesa examinadora universitaria.
