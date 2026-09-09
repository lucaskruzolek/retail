# 04. Sistemas Transversales de Soporte Operativo

Este módulo documenta los subsistemas fundacionales que sostienen la operación, persistencia, estética y ciclo de vida de la solución `Retail.sln`:

---

## 🧭 Catálogo de Sistemas Documentados

| Subsistema | Misión en Retail | Tecnologías y Mecanismos Clave | Enlace al Artículo |
| :--- | :--- | :--- | :--- |
| **Sistema de Persistencia** | Garantizar transacciones ACID, integridad relacional e indexación de alta velocidad en mostrador. | SQL Server LocalDB, EF Core 8, Fluent API, *Filtered Index* para artesanías y optimización *Push-down to SQL*. | [Ver Artículo](sistema-de-persistencia.md) |
| **Sistema de Diseño UI** | Brindar ergonomía visual, fluidez táctil y atajos de teclado para operaciones de alta presión en mostrador. | Windows 11 Fluent Design, WPF-UI, paleta Borravino (`#9D0F33`), tipografía dual (`Cascadia Code` / `Segoe UI Variable`) y `StyleGalleryView`. | [Ver Artículo](sistema-de-diseno-ui.md) |
| **Sistema de Logging y Errores** | Proveer observabilidad post-mortem y resiliencia para evitar caídas intempestivas (*Crash to Desktop*). | Serilog rotativo (`logs/retail-.log`), retención de 31 días, trampas globales en hilo STA y `UnhandledExceptionDialog`. | [Ver Artículo](sistema-de-logging-y-errores.md) |
| **Sistema de CI/CD** | Automatizar la aduana de calidad en Release y el empaquetado de ejecutables auto-contenidos para mostrador. | GitHub Actions (`ci.yml`, `release.yml`), runner `windows-latest`, `TreatWarningsAsErrors` y 100% Peer Review. | [Ver Artículo](sistema-de-ci-cd.md) |

---

## 📐 Estructura de Estudio
Cada artículo cuenta con:
1. **Fundamento Teórico:** Principios de ingeniería de software y normativas técnicas.
2. **Código Real:** Enlaces directos a archivos de configuración, diccionarios XAML y flujos en `src/` y `.github/`.
3. **Diagramas Mermaid:** Arquitecturas de ejecución, jerarquías de estilos y flujos de automatización.
4. **Decisiones y Trade-offs:** Análisis comparativo de alternativas tecnológicas evaluadas y descartadas.
5. **Preguntas de Examen:** Cuestionarios de autoevaluación para la mesa evaluadora de cátedra.
