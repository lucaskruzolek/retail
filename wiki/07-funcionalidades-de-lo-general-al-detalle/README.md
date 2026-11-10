# 07. Funcionalidades de lo general al detalle

Cada documento de esta carpeta recorre **una funcionalidad completa** de forma global y progresiva, para estudiarla: empieza por el flujo más abstracto (casos de uso, modelo de datos y ciclos de vida) y en cada nivel lo detalla más (lo que ve el usuario, qué capa hace cada cosa, los mecanismos internos y los algoritmos), y cierra con secciones de preparación para la defensa.

A diferencia del resto de la wiki, estos documentos son **HTML autocontenido**: están pensados para leerse en un navegador e imprimirse. Usan Mermaid 11.4.1 desde un CDN para los diagramas, por lo que necesitan conexión a internet para verlos; sin conexión muestran el texto fuente de cada diagrama. Al imprimir, las secciones desplegables se abren solas y los diagramas anchos salen en páginas apaisadas. Los artículos breves de cada flujo siguen en [05. Casos de Uso y Flujos](../05-casos-de-uso-y-flujos/README.md).

| Funcionalidad | Requisitos | Documento |
| :--- | :--- | :--- |
| **Importador de catálogos:** importación de listas de precios, clasificación del catálogo, incorporación a la tienda y fuentes múltiples de un artículo | `RF-05`, `RF-07`, `RF-21`, `RF-27`, `RF-28` | [importador-de-catalogos.html](importador-de-catalogos.html) |
| **Copia de seguridad:** copia manual, diaria y previa a migrar o a restaurar; verificación, bloqueo entre procesos, copias huérfanas y retención | `RF-22`, `RF-23`, `RNF-08`, `RF-03` | [copia-de-seguridad.html](copia-de-seguridad.html) |
| **Restauración de una copia:** desde el historial, con control dual (Administrador y Gerente) y por consola; base temporal e intercambio, validaciones, historial que sobrevive y red de seguridad | `RF-22`, `RF-23`, `RF-03` | [restauracion.html](restauracion.html) |

## Cómo se mantienen

* Los diagramas se escriben como texto Mermaid dentro de `<pre class="mermaid">`, con `<`, `>` y `&` escapados como entidades HTML (`&lt;br/&gt;` para un salto de línea en una etiqueta).
* Antes de entregar un cambio, abrir el documento en el navegador y confirmar que ningún diagrama muestra "Syntax error". En el modo headless de Edge se puede verificar sin abrir la ventana: `msedge --headless --virtual-time-budget=20000 --dump-dom <archivo>` y buscar ese texto.
* **Modo oscuro automático.** Los colores son variables CSS en `:root`; bajo `@media (prefers-color-scheme: dark)` se redefinen, y el script elige el tema `dark` o `neutral` de Mermaid según la preferencia del sistema. Mermaid fija el tema al dibujar, así que si cambia el modo con la página abierta se recarga para redibujar. Al imprimir, la paleta vuelve a la clara y los diagramas dibujados en oscuro se invierten con un filtro (`.modo-oscuro pre.mermaid svg`). Los tres documentos comparten estas reglas: un cambio de paleta se hace en los tres.
* Para probar el modo oscuro sin cambiar el sistema: `msedge --headless --blink-settings=preferredColorScheme=0 --virtual-time-budget=20000 --screenshot=<png> <archivo>` (en Edge, `0` es oscuro y `1` es claro).
* Cada afirmación sobre el sistema se contrasta con el código, y la fecha de esa revisión figura en el subtítulo del documento.
