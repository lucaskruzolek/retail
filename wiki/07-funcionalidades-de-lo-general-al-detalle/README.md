# 07. Funcionalidades de lo general al detalle

Cada documento de esta carpeta recorre **una funcionalidad completa** de forma global y progresiva, para estudiarla: empieza por el flujo más abstracto (casos de uso, modelo de datos y ciclos de vida) y en cada nivel lo detalla más (lo que ve el usuario, qué capa hace cada cosa, los mecanismos internos y los algoritmos), y cierra con secciones de preparación para la defensa.

Estos documentos son **HTML**: están pensados para leerse en un navegador e imprimirse, y se abren con doble clic, sin servidor. Comparten los estilos, el comportamiento y la marca de [`wiki/recursos/`](../recursos/), así que se copian con esa carpeta, no sueltos. La entrada es la [portada del desarrollador](../index.html), que los lista junto con el resto de la documentación. Usan Mermaid 11.4.1 desde un CDN para los diagramas, por lo que necesitan conexión a internet para verlos; sin conexión muestran el texto fuente de cada diagrama. Al imprimir, las secciones desplegables se abren solas, los diagramas anchos salen en páginas apaisadas y se ocultan la barra lateral y la navegación. Los artículos breves de cada flujo siguen en [05. Casos de Uso y Flujos](../05-casos-de-uso-y-flujos/README.md).

| Funcionalidad | Requisitos | Documento |
| :--- | :--- | :--- |
| **Anatomía de un CRUD** *(fundamentos)*: las 13 piezas de un módulo con el código real de Cliente, inyección de dependencias y Unit of Work | `RF-20`, `RNF-03` | [anatomia-de-un-crud.html](anatomia-de-un-crud.html) |
| **Importador de catálogos:** importación de listas de precios, clasificación del catálogo, incorporación a la tienda y fuentes múltiples de un artículo | `RF-05`, `RF-07`, `RF-21`, `RF-27`, `RF-28` | [importador-de-catalogos.html](importador-de-catalogos.html) |
| **Copia de seguridad:** copia manual, diaria y previa a migrar o a restaurar; verificación, bloqueo entre procesos, copias huérfanas y retención | `RF-22`, `RF-23`, `RNF-08`, `RF-03` | [copia-de-seguridad.html](copia-de-seguridad.html) |
| **Restauración de una copia:** desde el historial, con control dual (Administrador y Gerente) y por consola; base temporal e intercambio, validaciones, historial que sobrevive y red de seguridad | `RF-22`, `RF-23`, `RF-03` | [restauracion.html](restauracion.html) |
| **Tablero y reportes:** indicadores del período contra el anterior, cinco reportes con filtros resueltos en SQL (ventas por período, ranking, rentabilidad, stock crítico y sin movimiento), costo congelado al vender y exportación a Excel y PDF | `RF-24`, `RF-25`, `RF-26`, `RNF-09` | [tablero-y-reportes.html](tablero-y-reportes.html) |
| **Pedidos a proveedores:** pedir desde Artículos y desde el importador, un borrador por proveedor, foto del costo en cada renglón, lote en una sola transacción, concurrencia y cierre atómico con la compra | `RF-29`, `RF-28`, `RF-19` | [pedidos-a-proveedores.html](pedidos-a-proveedores.html) |

La portada también muestra los recorridos que faltan escribir, con sus requisitos: la lista está en [`wiki/recursos/recorridos.js`](../recursos/recorridos.js).

## Cómo se mantienen

* Los diagramas se escriben como texto Mermaid dentro de `<pre class="mermaid">`, con `<`, `>` y `&` escapados como entidades HTML (`&lt;br/&gt;` para un salto de línea en una etiqueta).
* Antes de entregar un cambio, abrir el documento en el navegador y confirmar que ningún diagrama muestra "Syntax error". En el modo headless de Edge se puede verificar sin abrir la ventana: `msedge --headless --virtual-time-budget=20000 --dump-dom <archivo>` y buscar ese texto.
* **Modo oscuro automático.** Los colores son variables CSS en `:root`; bajo `@media (prefers-color-scheme: dark)` se redefinen, y el script elige el tema `dark` o `neutral` de Mermaid según la preferencia del sistema. Mermaid fija el tema al dibujar, así que si cambia el modo con la página abierta se recarga para redibujar. Al imprimir, la paleta vuelve a la clara y los diagramas dibujados en oscuro se invierten con un filtro (`.modo-oscuro pre.mermaid svg`). Estas reglas viven en `recursos/comun.css` y `recursos/comun.js`: un cambio de paleta se hace una sola vez.
* Para probar el modo oscuro sin cambiar el sistema: `msedge --headless --blink-settings=preferredColorScheme=0 --virtual-time-budget=20000 --screenshot=<png> <archivo>` (en Edge, `0` es oscuro y `1` es claro).
* Cada afirmación sobre el sistema se contrasta con el código, y la fecha de esa revisión figura en el subtítulo del documento.
* **Recursos comunes** (`wiki/recursos/`):

  | Archivo | Qué contiene |
  | :--- | :--- |
  | `comun.css` | Paleta clara y oscura, tipografía, tablas, notas, desplegables, barra de marca, barra lateral, portada e impresión |
  | `comun.js` | Mermaid con el tema según el modo, desplegables abiertos al imprimir, barra lateral con la sección activa, anterior y siguiente, y tarjetas de la portada |
  | `recorridos.js` | La única lista de recorridos, con su estado (`escrito` o `pendiente`) y sus requisitos |
  | `marca/logo.svg` | El ícono de la marca (provisorio: `webhook` de Boxicons 3, MIT). Para cambiar la marca se reemplaza este archivo |

* **Agregar un recorrido:** copiar la cabecera y el pie de uno existente (los `<link>` a `comun.css`, la `<header class="barra">`, `<body class="recorrido">` y los tres `<script>` del final), escribir el contenido con `id` en cada `<h2>`, y pasar su entrada de `recorridos.js` a `"escrito"`. La barra lateral, anterior y siguiente, y la tarjeta de la portada se arman solas. Los `<h3>` reciben su `id` al cargar, generado del texto.
* **Fundamentos:** un documento que explica cómo funciona el sistema y no una funcionalidad (como *Anatomía de un CRUD*) se marca en `recorridos.js` con `fundamento: true`: aparece en la portada y en la barra lateral, pero no cuenta en la cobertura de funcionalidades.
* **Verificar los enlaces** antes de entregar: `powershell -ExecutionPolicy Bypass -File scripts/verificar-enlaces-wiki.ps1`. Comprueba los enlaces relativos y sus anclas, los enlaces a este repositorio en GitHub contra los archivos locales, y que `recorridos.js` coincida con los archivos que existen.
