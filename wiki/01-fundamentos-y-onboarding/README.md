# 01. Fundamentos y Onboarding

Este módulo reúne la fundamentación arquitectónica inicial, la guía de configuración del entorno local y las políticas de disciplina técnica necesarias para comenzar a desarrollar en la solución `Retail.sln`:

---

## 🧭 Catálogo de Documentos del Módulo

| Documento | Enfoque Pedagógico | Propósito y Alcance en el Proyecto | Enlace al Artículo |
| :--- | :--- | :--- | :--- |
| **Clean Desktop Monolith** | Arquitectura de Mostrador de Alta Disponibilidad | Justificación de la elección de un monolito de escritorio en .NET 8 y WPF frente a microservicios o aplicaciones web en la nube. Latencia $< 15\text{ ms}$, resiliencia ante caídas de internet y falacias de la computación distribuida. | [Ver Artículo](clean-desktop-monolith.md) |
| **Entorno de Desarrollo Local** | Guía de Onboarding Paso a Paso | Requisitos previos (SDK .NET 8, LocalDB, IDEs), clonado, compilación en Release, ejecución de pruebas xUnit y primer arranque con semillero automático. | [Ver Artículo](entorno-desarrollo-local.md) |
| **Convenciones, Disciplina y Calidad** | Estándares y Rigor de Cátedra | Estilo Allman en llaves (`.editorconfig`), cero tolerancia a advertencias (`TreatWarningsAsErrors` y `Nullable enable`), nomenclatura BDD en pruebas y gobernanza de 100% Peer Review entre Lucas y Pablo. | [Ver Artículo](convenciones-y-calidad.md) |

---

## 🚀 Ciclo de Iteración Rápida del Desarrollador (*Inner Loop*)

Antes de proponer cualquier cambio o abrir un Pull Request, todo colaborador debe ejecutar la siguiente tríada de comandos en su terminal:

```powershell
# 1. Compilación estricta en Release sin advertencias
dotnet build Retail.sln --configuration Release

# 2. Ejecución de las 4 suites de pruebas unitarias e integración
dotnet test Retail.sln --configuration Release --no-build

# 3. Verificación automática de estilo y formato (.editorconfig)
dotnet format Retail.sln --verify-no-changes
```
