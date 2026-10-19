# Defensa Técnica: Respaldo y Restauración de la Base

### Módulo: 06. Vademécum para la Defensa Oral de Cátedra
**Audiencia:** Lucas Kruzolek, Pablo Fernandez y mesa evaluadora de cátedra  
**Requisitos Vinculados:** `RF-22` (Respaldo y Restauración), `RF-23` (Administración de la Base por Comandos), `RNF-08` (Integridad y Respaldo Diario), `RNF-05` (Seguridad de Persistencia) y `RF-03` (Roles)  
**Objetivo Pedagógico:** Tener a mano, por tema, la pregunta como la haría un docente (incluidas las preguntas capciosas), una **respuesta corta** para decir en voz alta y una **respuesta desarrollada** con enlaces al capítulo que la fundamenta.  
**Guía completa de apoyo:** [`wiki/05-casos-de-uso-y-flujos/respaldo-y-restauracion/`](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/README.md) · **Decisiones y riesgos:** [`etapa-6-track-6.3-backup-restore.md`](file:///c:/Users/lucas/Proyectos/retail/docs/roadmap/etapa-6-track-6.3-backup-restore.md)

---

## 🧭 Cómo Usar Esta Página

| Si tenés… | Hacé esto |
| :--- | :--- |
| 5 minutos | Leé el [guion de presentación](#-guion-de-5-minutos-para-presentar-el-módulo) y la [matriz de trade-offs](#-matriz-de-trade-offs-decisión--beneficio--costo--alternativa-descartada) |
| Una hora | Recorré las **respuestas cortas** de los 8 bloques y practicalas en voz alta |
| La noche anterior | Leé completas las preguntas marcadas con 🔥 (las más difíciles) y los límites del bloque 8 |

Convenciones: 🎯 qué busca evaluar el docente · 🗣️ respuesta corta (para decir en voz alta) · 📖 respuesta desarrollada · 🔥 pregunta difícil o capciosa. Los identificadores `D-n` (decisiones), `R-n` (riesgos) y `A-/B-/C-/D-nn` (escenarios de error del [capítulo 07](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/07-escenarios-de-error-y-corrupcion.md)) remiten al registro de diseño y a esa guía.

> [!IMPORTANT]
> La regla de oro de esta defensa: **distinguir lo que está probado por un test, lo verificado a mano y lo deducido del código**, y decirlo. Un hueco reconocido con honestidad suma; un hueco escondido y descubierto por la mesa resta. Los límites están en el [capítulo 09](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/09-como-se-prueba.md) (sección 6) y en el bloque 8 de esta página.

---

## 🏛️ Bloque 1: Arquitectura

### Pregunta 1.1: *"¿Por qué sacaron las migraciones del arranque de la aplicación y crearon una herramienta de consola aparte?"*

* **🎯 Qué busca evaluar el docente:** Si entienden que *arrancar* una aplicación no debe tener efectos secundarios sobre los datos, y la relación entre operación y reglas de negocio.
* **🗣️ Respuesta corta:** "Porque una migración automática al arrancar no deja lugar para una copia previa ni para el control de un operador. Con `Retail.Cli`, `migrar` hace primero una copia `PreMigracion` y recién después aplica el esquema, y la app solo verifica y muestra un mensaje accionable (D-10, D-11)."
* **📖 Respuesta desarrollada:** Antes, `OnStartup` migraba y sembraba datos. Eso tenía tres problemas: (1) no había dónde tomar una copia de seguridad *antes* de modificar el esquema; (2) una app recién actualizada podía alterar la base del comercio sin que nadie lo decidiera; (3) no había forma de recuperarse de una base rota, porque para restaurar hacía falta la app y para entrar a la app hacía falta la base. `Retail.Cli` es una segunda raíz de composición que **reutiliza los servicios de Application** (los scripts `.ps1` son envoltorios finos, no duplican reglas) y se ocupa de `estado`, `migrar`, `inicializar`, `datos-demo`, `backup` y `restaurar`. La app ahora **verifica** al arrancar y, si algo falla, muestra el `DiagnosticoArranqueDialog` con el comando listo para copiar y se cierra; nunca ejecuta esos comandos. Ver [capítulo 05](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/05-retail-cli.md) (secciones 1 y 7) y [capítulo 01](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/01-fundamentos-y-decisiones.md) (3.2). Teoría: separación de responsabilidades y *fail fast*.

---

### Pregunta 1.2: *"¿Por qué el SQL de `BACKUP` y `RESTORE` está en Infrastructure y no en el servicio que lo necesita?"*

* **🎯 Qué busca evaluar el docente:** Principio de Inversión de Dependencias y Clean Architecture aplicados a un caso concreto.
* **🗣️ Respuesta corta:** "Porque `BackupService` depende de un puerto, `IEjecutorBackupBaseDatos`, que vive en Application. SQL Server es un detalle que se enchufa desde afuera. Así el servicio se prueba con dobles y se podría cambiar de motor sin tocar la orquestación."
* **📖 Respuesta desarrollada:** La flecha de dependencia es `Retail.App → Retail.Application ← Retail.Infrastructure` (Ley 1). Los puertos (`IEjecutorBackupBaseDatos`, `IRestauradorBaseDatos`, `IBaseRestaurada`, `IBloqueoRestauracion`, `IArchivosBackup`, `IEsquemaBaseDatos`) los define Application; Infrastructure los implementa con ADO.NET. Beneficio medible: `RestauracionServiceTests` sustituye todos los puertos con `NSubstitute` y prueba el **orden** de los pasos con `Received.InOrder` sin tocar un motor ([capítulo 09](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/09-como-se-prueba.md), 2.2). Ver [capítulo 02](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/02-componentes-por-capa.md) (1.2).

---

### Pregunta 1.3: *"¿Por qué usan ADO.NET para el respaldo si la Ley 8 manda delegar al motor desde EF Core?"* 🔥

* **🎯 Qué busca evaluar el docente:** Si saben el alcance real de una regla y justifican una excepción.
* **🗣️ Respuesta corta:** "La Ley 8 aplica a consultas de datos de negocio. `BACKUP DATABASE` no admite una transacción de usuario y EF Core no tiene API para copias; es una excepción justificada y acotada a Infrastructure."
* **📖 Respuesta desarrollada:** `BACKUP` y `RESTORE` son comandos de administración del motor, no consultas de lectura o escritura de tablas. EF Core envuelve las operaciones en transacciones, y `BACKUP` no puede ejecutarse dentro de una. Además el trabajo *sí* se delega al motor: es SQL Server quien lee páginas y escribe el archivo. Las sentencias usan parámetros para valores (`@ruta`, `@nombre`) y delimitan con corchetes los identificadores, que no pueden ser parámetros (`ComandosSqlServer.Delimitar`, que duplica los `]`). La frontera con el dominio se mantiene: las reglas (qué copia es restaurable, quién puede restaurar) viven en `RegistroBackup` y en los servicios, nunca en SQL. Ver [capítulo 01](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/01-fundamentos-y-decisiones.md) (2.1).

---

### Pregunta 1.4: *"Las tablas de respaldo no tienen claves foráneas. ¿No es un error de modelado?"*

* **🎯 Qué busca evaluar el docente:** Integridad referencial frente a requisitos de recuperación.
* **🗣️ Respuesta corta:** "Es deliberado. El historial se reinserta en una base restaurada donde el usuario o la copia pueden no existir. El operador se guarda como una foto (nombre y Id) y la `Referencia` GUID identifica el hecho sin depender de una clave autonumérica."
* **📖 Respuesta desarrollada:** Al restaurar, el historial vive dentro de la base que se reemplaza (R-1). Se lo lee antes y se lo reinserta en la temporal deduplicando por `Referencia`. Con una FK a `USUARIOS` la reinserción fallaría si el Gerente de la copia tiene otro Id. Por eso `REGISTROS_RESTAURACION` guarda `NombreUsuario` como dato confiable (el `IdUsuario` es el de la base reemplazada). Se compensa con validaciones en el dominio (`Registrar_SinGerente_LanzaDomainException`) y restricciones propias (`CK_REGISTROS_BACKUP_Operador`, índice filtrado `IX_REGISTROS_BACKUP_EnCurso`). Ver [capítulo 02](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/02-componentes-por-capa.md) (pregunta 4 y 4.3).

---

### Pregunta 1.5: *"¿Dónde se garantiza que solo se restaura una copia verificada? ¿No alcanza con ocultarla en la lista?"*

* **🎯 Qué busca evaluar el docente:** Defensa en profundidad y dónde viven las invariantes.
* **🗣️ Respuesta corta:** "Se garantiza en tres lugares: el agregado (`EsRestaurable` solo es verdadero para `Verificado`), el servicio (rechaza una copia no restaurable) y el archivo (se vuelve a calcular el SHA-256 antes de restaurar). La lista es solo comodidad."
* **📖 Respuesta desarrollada:** El ciclo de vida `EnCurso → Exitoso → Verificado` está custodiado por `RegistroBackup`, que lanza `TransicionBackupInvalidaException` ante un salto inválido (probado en `RegistroBackupTests`). `RestaurarDesdeHistorialAsync` rechaza con *"La copia elegida no es restaurable: solo se restauran copias verificadas cuyo archivo se conserva."* si `registro is null || !registro.EsRestaurable`. Y aunque el registro diga `Verificado`, `ExigirArchivoSinCambiosAsync` recalcula huella y tamaño bajo el bloqueo exclusivo (B-01). Es el patrón de [capítulo 01](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/01-fundamentos-y-decisiones.md) (sección 4): agregado + servicio + base.

---

## 💾 Bloque 2: La Copia de Seguridad

### Pregunta 2.1: *"Ya tienen `WITH CHECKSUM`. ¿Para qué sirven `RESTORE VERIFYONLY` y el SHA-256? ¿No es redundante?"*

* **🎯 Qué busca evaluar el docente:** Si distinguen qué detecta cada control de integridad.
* **🗣️ Respuesta corta:** "Detectan cosas distintas. `CHECKSUM` protege el contenido al copiar; `VERIFYONLY` prueba que el archivo es legible y completo; el SHA-256 detecta cualquier cambio posterior del archivo. Ninguno prueba autenticidad."
* **📖 Respuesta desarrollada:** `BACKUP ... WITH CHECKSUM` hace que el motor verifique cada página al leerla: si la base ya estaba dañada, la copia falla en lugar de guardar basura. `RESTORE VERIFYONLY ... WITH CHECKSUM` relee el archivo entero sin restaurarlo. El SHA-256 (calculado por la app con *streaming*) cubre cada byte del archivo y se compara de nuevo antes de restaurar: detecta un pendrive que copió mal, *bit rot* o un archivo reemplazado. Nada de esto prueba que una restauración funcione "en cualquier circunstancia" (espacio, permisos, esquema): por eso hay tests que restauran de verdad (R-8). Tabla completa en [capítulo 01](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/01-fundamentos-y-decisiones.md) (2.3).

---

### Pregunta 2.2: *"¿Por qué guardan el registro de la copia como `EnCurso` antes de copiar, y no al terminar?"*

* **🎯 Qué busca evaluar el docente:** Razonamiento sobre fallas parciales y registro de intención.
* **🗣️ Respuesta corta:** "Porque así un corte deja evidencia del intento y el índice único filtrado impide una segunda copia simultánea. Si registráramos al terminar, una copia muerta no dejaría rastro y dos copias podrían correr juntas."
* **📖 Respuesta desarrollada:** Es el patrón de *write-ahead* aplicado al historial: primero se declara la intención, después se hace el trabajo. El índice `IX_REGISTROS_BACKUP_EnCurso` (único y filtrado por estado) resuelve R-12 *en la base*, no solo en C#; es el mismo patrón que `IX_TURNOS_CAJA_TurnoAbierto`. La consecuencia incómoda (un registro huérfano si el proceso muere) se resuelve con la autorreparación B-0 (pregunta 2.3). Si falla la copia, `RegistrarFalloAsync` borra el parcial y deja el registro `Fallido` con `CancellationToken.None`, para que el historial refleje lo ocurrido aunque el operador haya cancelado. Ver [capítulo 03](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/03-flujo-de-copia.md) (3.3).

---

### Pregunta 2.3: *"Si se corta la luz en medio de una copia, queda un registro 'en curso' para siempre. ¿Cómo distinguen una copia muerta de una que sigue corriendo?"* 🔥

* **🎯 Qué busca evaluar el docente:** Si recurren a un *timeout* frágil o a una garantía del motor.
* **🗣️ Respuesta corta:** "No miramos el reloj. Cada copia sostiene un bloqueo compartido de sesión de `sp_getapplock` y el motor lo suelta si el proceso muere. Si otra operación logra el bloqueo exclusivo, nadie está copiando y todo `EnCurso` o `Exitoso` es huérfano con certeza."
* **📖 Respuesta desarrollada:** Un plazo ("30 minutos sin avance") podría cerrar una copia legítima de una base grande. El bloqueo de lectores y escritor convierte la pregunta en un hecho del motor: las copias toman `Shared`, la restauración `Exclusive`, y `@LockOwner = 'Session'` ata el bloqueo a la conexión. `CerrarCopiasInterrumpidasAsync` intenta el exclusivo **sin esperar**; si lo obtiene, cierra los huérfanos con `RegistrarFallo` y recién después borra los restos. Si devuelve `null`, hay una operación viva y no se toca nada (A-03). Está probado con un test que mata la sesión con `KILL` ([capítulo 09](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/09-como-se-prueba.md), 4.1). Salvedad honesta: se simuló la caída con `KILL`; no se cortó la luz. Ver [capítulo 03](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/03-flujo-de-copia.md) (sección 4) y [capítulo 07](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/07-escenarios-de-error-y-corrupcion.md) (A-01 a A-04).

---

### Pregunta 2.4: *"¿Para qué un manifiesto `.json` si ya guardan todo en la tabla `REGISTROS_BACKUP`?"*

* **🎯 Qué busca evaluar el docente:** Pensar en el peor día: la base perdida.
* **🗣️ Respuesta corta:** "Porque la tabla vive dentro de la base. Si la base se pierde, el historial se pierde con ella. El manifiesto viaja junto al `.bak` y permite saber qué contiene y restaurarlo en una PC nueva (R-1, R-2)."
* **📖 Respuesta desarrollada:** El manifiesto lleva referencia, tipo, fecha, operador, nombre del `.bak`, tamaño, SHA-256, última migración y versión de la app. La restauración desde la consola lo lee (`LeerManifiestoAsync`) y valida **cada campo explícitamente** (el modificador `required` de C# solo exige que la propiedad figure en el JSON y acepta `null`); los mensajes y tests están en B-05. Además sirve para las copias `PreMigracion` de bases anteriores al historial (`RegistradaEnHistorial = false`), que solo existen en el manifiesto. Ver [capítulo 03](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/03-flujo-de-copia.md) (8.2).

---

### Pregunta 2.5: *"La Ley 3 prohíbe borrar. ¿Por qué la retención borra archivos? ¿Y por qué el registro no se marca como borrado?"*

* **🎯 Qué busca evaluar el docente:** Aplicar el borrado lógico con criterio, distinguiendo entidad de archivo.
* **🗣️ Respuesta corta:** "La Ley 3 protege las entidades de negocio. Lo que se borra físicamente es el archivo, que no es una entidad. El registro nunca se borra: cambia a `ArchivoPurgado`, que es una constancia de auditoría y deja de ser restaurable."
* **📖 Respuesta desarrollada:** El historial de copias es evidencia: cuándo se copió, quién, qué pasó. La retención conserva las 14 copias con archivo más recientes (`CantidadMaximaCopias`); para las demás borra el `.bak` y el `.json` y mueve el registro a `ArchivoPurgado` (transición validada por el dominio). Ni siquiera se usa `MarkAsDeleted()`: el estado ya expresa lo ocurrido. Refuerzo de seguridad: la cuenta de la tarea **no tiene `DELETE`** sobre `REGISTROS_BACKUP` (solo `SELECT, INSERT, UPDATE`) y un test lo exige (`CuentaDeLaTarea_FueraDeLoNecesario_NoTienePermiso`). Las copias `PreRestauracion` tienen su propia retención de 30 días (D-24): con la común, la copia de los datos reales desaparecería a las dos semanas de un restore equivocado. Ver [capítulo 03](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/03-flujo-de-copia.md) (9). *(Ver también la pregunta 6.6 sobre cómo se borran los archivos.)*

---

### Pregunta 2.6: *"¿Qué pasa si hay una copia en curso y alguien lanza otra? ¿Y si hay una restauración?"*

* **🎯 Qué busca evaluar el docente:** Concurrencia entre procesos distintos (app, consola, tarea).
* **🗣️ Respuesta corta:** "Dos copias no corren juntas: lo impiden el control de 'copia en curso' y el índice único. Una copia y una restauración tampoco, por el bloqueo de lectores y escritor del motor, que vale entre procesos."
* **📖 Respuesta desarrollada:** `sp_getapplock` vive en el motor, así que protege entre la app WPF, `Retail.Cli` y la tarea de las 21:00. Copia durante una restauración: espera hasta 10 s y rechaza con *"Hay una restauración de la base u otra copia iniciándose..."*. Restauración durante una copia: rechaza al instante. Tabla de mensajes y tests en A-12. Límite reconocido: si dos procesos pasaran el control de "copia en curso" exactamente a la vez, el perdedor chocaría con el índice y recibiría la excepción de EF sin traducir; no se verificó con una prueba de concurrencia real.

---

## 🔄 Bloque 3: La Restauración

### Pregunta 3.1: *"Hubiera sido más simple un `RESTORE ... WITH REPLACE` sobre la base en uso. ¿Por qué complicarlo con una base temporal?"* 🔥

* **🎯 Qué busca evaluar el docente:** Análisis de riesgo de una operación destructiva y su costo de mitigación.
* **🗣️ Respuesta corta:** "`WITH REPLACE` destruye la base vigente antes de saber si la copia sirve. Con la base temporal, todo lo que puede fallar ocurre en una base descartable y el cambio sobre la vigente se reduce a dos renombrados de segundos. El costo aceptado es el doble de espacio mientras dura (D-17)."
* **📖 Respuesta desarrollada:** Restaurar es la operación más peligrosa del sistema. Con `REPLACE`, si la copia está dañada, es de un esquema más nuevo o no tiene Gerente, la base en uso ya está perdida. Con la temporal se valida antes: huella, espacio (D-23), esquema, credencial, historial. Además reduce la indisponibilidad de "todo el `RESTORE`" a "el renombrado". Y deja una doble red de seguridad: la copia `PreRestauracion` y la base `RetailDb_Anterior_<fecha>` intacta. Comparación en el [capítulo 04](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/04-flujo-de-restauracion.md) (1.2). Alternativa descartada y registrada en D-17.

---

### Pregunta 3.2: *"Dicen que la base vigente queda intacta si la restauración falla. ¿Es totalmente cierto?"* 🔥 (pregunta capciosa)

* **🎯 Qué busca evaluar el docente:** Precisión: no sobrevender una garantía.
* **🗣️ Respuesta corta:** "Los datos de negocio sí quedan intactos hasta el intercambio. Pero con rigor: antes de ese punto la tabla de historial de la vigente sí cambia, porque se cierran las copias huérfanas y se registra la copia previa. Es historial, no datos de negocio."
* **📖 Respuesta desarrollada:** Antes de `PromoverAVigenteAsync` la base vigente recibe dos tipos de escritura, ambas en `REGISTROS_BACKUP`: (1) el cierre de copias huérfanas por B-0 (`EnCurso`/`Exitoso` → `Fallido`), que se hace y se guarda antes de la copia previa porque de lo contrario esta chocaría con el índice de copia en curso; (2) el registro de la copia `PreRestauracion`. Ninguna toca ventas, caja, stock ni usuarios. Por eso el mensaje al operador dice "La base vigente no cambió" y esta página dice "datos de negocio intactos": el matiz es la respuesta correcta. Ver [capítulo 04](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/04-flujo-de-restauracion.md) (pregunta 2 de defensa y pasos 5 y 10) y [capítulo 07](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/07-escenarios-de-error-y-corrupcion.md) (B-15). Si te preguntan por la copia previa: es una copia (lectura), no modifica datos.

---

### Pregunta 3.3: *"¿Qué es el punto de no retorno y por qué no se puede cancelar?"*

* **🎯 Qué busca evaluar el docente:** Atomicidad construida a mano cuando no hay una transacción que abarque todo.
* **🗣️ Respuesta corta:** "Es la llamada a `PromoverAVigenteAsync`. Se hace con `CancellationToken.None` porque cancelar entre los dos renombrados dejaría el sistema sin base llamada `RetailDb`: peor que terminar el intercambio."
* **📖 Respuesta desarrollada:** Un restore involucra archivos, varias bases y renombrados que ninguna transacción abarca, así que la atomicidad hay que diseñarla con un **orden** pensado: todo lo que puede fallar va antes. El intercambio son dos `ALTER DATABASE ... MODIFY NAME` consecutivos; en el medio no hay base vigente. Por eso: el servicio ignora la cancelación (`RestaurarDesdeHistorialAsync_IntercambioConTokenCancelable_PromueveSinCancelacion`), `CancelacionConsola` no deja que Ctrl+C mate el proceso (`e.Cancel = true`) y la UI bloquea el cierre del diálogo mientras corre. Contra un corte de energía justo ahí no hay defensa en la aplicación (C-04): los datos siguen íntegros en el servidor y la recuperación es manual. El test que prueba el orden usa `Received.InOrder` ([capítulo 09](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/09-como-se-prueba.md), 2.2.1).

---

### Pregunta 3.4: *"¿Qué pasa si falla el segundo renombrado? ¿Y si falla también la reversión?"*

* **🎯 Qué busca evaluar el docente:** Pensar en fallas dobles y en el peor caso.
* **🗣️ Respuesta corta:** "Si falla el segundo, se revierte el primero y la vigente queda intacta; hay un test que lo provoca poniendo la temporal `OFFLINE`. Si falla también la reversión, no queda base `RetailDb`, pero los datos siguen íntegros en la anterior y hay que recuperarlos a mano. Esa rama no tiene test."
* **📖 Respuesta desarrollada:** C-02: `RevertirPrimerPasoAsync` devuelve la anterior a su nombre y la deja `MULTI_USER`; mensaje *"la base vigente se conservó sin cambios"*. C-03: lanza *"requiere intervención técnica (ver el log)"* y escribe un `LogCritical`; la app mostrará el diagnóstico de arranque porque no encuentra la base. Cada sentencia del intercambio se ejecuta por separado, justamente para saber en qué estado quedó cada base. El primer renombrado reintenta 3 veces con 500 ms de pausa (M-1). Ver [capítulo 04](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/04-flujo-de-restauracion.md) (6) y [capítulo 07](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/07-escenarios-de-error-y-corrupcion.md) (C-01 a C-05).

---

### Pregunta 3.5: *"¿Por qué el intercambio usa `MODIFY NAME`? ¿No hay que renombrar también los archivos `.mdf`?"*

* **🎯 Qué busca evaluar el docente:** Conocimiento del modelo lógico/físico de SQL Server.
* **🗣️ Respuesta corta:** "`MODIFY NAME` cambia solo el nombre lógico en el catálogo. Eso hace el intercambio instantáneo aunque pese gigabytes y evita colisiones con los archivos de la base en uso. Después de restaurar, `RetailDb` tiene archivos con nombre `RetailDb_<fecha>_0.mdf`, y es normal."
* **📖 Respuesta desarrollada:** Los archivos de la temporal se crean con nombres únicos (`WITH MOVE`), porque la vigente sigue usando `RetailDb.mdf`. Renombrar archivos exigiría poner la base offline y mover bytes; con `MODIFY NAME` no se copia ni se mueve nada. Ver [capítulo 01](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/01-fundamentos-y-decisiones.md) (2.6 y 2.7).

---

### Pregunta 3.6: *"Restauran la copia de ayer: ¿se pierden las ventas de hoy?"* 🔥 (pregunta capciosa)

* **🎯 Qué busca evaluar el docente:** Entender qué hace realmente un restore y sus consecuencias fiscales.
* **🗣️ Respuesta corta:** "No se destruyen: quedan en la base `RetailDb_Anterior_<fecha>`, pero no están en la base en uso y no se fusionan solas. Y para ARCA, los comprobantes autorizados después de la copia no figuran en el sistema, por eso se muestra siempre el aviso de conciliación (R-3)."
* **📖 Respuesta desarrollada:** Restaurar es un **reemplazo**, no una fusión. Los datos posteriores a la copia siguen en la base anterior y en la copia `PreRestauracion` (esta última, 30 días). No hay un mecanismo de "traer lo que faltaba": recuperar esas ventas sería un trabajo manual. Por eso el restore no sirve para anular ventas ante ARCA, y el diálogo exige escribir `RESTAURAR` y muestra el aviso fiscal antes de habilitar el botón. Además se bloquea si hay un turno de caja abierto (R-11), controlado dos veces por el TOCTOU del `RESTORE` de minutos (C-2). Ver [capítulo 04](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/04-flujo-de-restauracion.md) (pregunta 5 y pasos 4 y 10) y [capítulo 07](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/07-escenarios-de-error-y-corrupcion.md) (B-13).

---

### Pregunta 3.7: *"¿Por qué la aplicación se reinicia sola después de restaurar?"*

* **🎯 Qué busca evaluar el docente:** Efectos de reemplazar una base con conexiones abiertas.
* **🗣️ Respuesta corta:** "Porque el intercambio corta las conexiones (`SET SINGLE_USER WITH ROLLBACK IMMEDIATE`) y el contexto de la app apunta a una base reemplazada. Se reinicia la instancia y se limpian los *pools* de conexiones."
* **📖 Respuesta desarrollada:** `PromoverAVigenteAsync` ejecuta `SqlConnection.ClearAllPools()` en un `finally` (aunque falle), porque las conexiones cortadas quedan inservibles en el *pool*. La aplicación que restauró arranca una instancia nueva y recién después cierra la actual (`IReinicioAplicacion`); si no puede, muestra "Reinicio manual necesario". Las otras terminales fallan en su próxima consulta hasta reiniciarse. Límite honesto: el reinicio real nunca se ejecutó en una prueba manual (C-06 del capítulo 07). Ver [capítulo 02](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/02-componentes-por-capa.md) (5.4).

---

## 💻 Bloque 4: La Herramienta de Consola (`Retail.Cli`)

### Pregunta 4.1: *"Eliminaron `blanquear-gerente` aunque resolvía el olvido de contraseña. ¿No es peor el producto?"* 🔥

* **🎯 Qué busca evaluar el docente:** Capacidad de revisar una decisión propia al descubrir un hueco de seguridad.
* **🗣️ Respuesta corta:** "En un comercio el personal comparte la sesión de Windows, así que 'tener acceso a la PC' no prueba ser el dueño. Con ese comando, un cajero podía tomar la cuenta del Gerente. La regla que quedó: ningún comando de `Retail.Cli` otorga acceso a una cuenta existente (D-13 revisada)."
* **📖 Respuesta desarrollada:** Se implementó primero (Fase 2) y se eliminó al revisar el modelo de amenazas: era una escalada de privilegios servida en bandeja. Se descartaron también los códigos de recuperación (un secreto más que guardar y perder). La recuperación queda **exclusivamente por correo** (futuro). El costo se acepta y se documenta como R-16: hoy un Gerente que olvida su contraseña solo puede ser blanqueado por **otro Gerente**; recomendación operativa, al menos dos Gerentes. Un test de arquitectura refuerza la separación: `AddApplicationServices_NoRegistraLasOperacionesPrivilegiadasDeAdministracion`. Ver [capítulo 08](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/08-seguridad-y-permisos.md) (3.1 y 3.2).

---

### Pregunta 4.2: *"¿Contra qué verifican la contraseña en `Retail.Cli restaurar` si la base puede estar rota o no existir?"*

* **🎯 Qué busca evaluar el docente:** Resolver un problema de autenticación circular (D-18/D-19).
* **🗣️ Respuesta corta:** "Contra la copia que se va a restaurar. Se la restaura en la base temporal y se verifica el BCrypt contra su tabla de usuarios: quien restaura demuestra que era Gerente en esa copia. Funciona en una PC nueva."
* **📖 Respuesta desarrollada:** Las alternativas, todas descartadas con razón (D-19): sin autenticación (un cajero reemplazaría la base), contra la vigente (falla justo cuando hace falta: PC nueva o base corrupta) y un procedimiento manual con SSMS (sin control ni auditoría). `VerificarCredencialEnLaCopiaAsync` rechaza, con la **misma** `CredencialesInvalidasException`, usuario inexistente, dado de baja, que no es Gerente o con `DebeCambiarPassword = true` (una contraseña que no eligió el dueño, como el `admin/Admin123!` sembrado en bases viejas). Límites: R-19 (sin límite de intentos; cada intento cuesta un `RESTORE` completo) y R-20 (ver pregunta 6.1). Ver [capítulo 08](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/08-seguridad-y-permisos.md) (3.3).

---

### Pregunta 4.3: *"Restaurar pide credenciales pero copiar no. ¿No es incoherente?"*

* **🎯 Qué busca evaluar el docente:** Distinguir el derecho a copiar del derecho a leer o reemplazar.
* **🗣️ Respuesta corta:** "Copiar produce un archivo en una carpeta protegida; no muestra datos, no da acceso a ninguna cuenta ni modifica nada. Restaurar reemplaza toda la base. Además la tarea programada corre sin nadie presente: no podría pedir una contraseña."
* **📖 Respuesta desarrollada:** El contenido del `.bak` lo protege la ACL de la carpeta, y su límite es R-17. Una copia no otorga acceso (D-13) y está acotada: una sola copia en curso y retención de 14. En cambio, la copia **manual** desde la app sí exige ser Gerente y reautenticarse (D-3, D-8), porque la inicia una persona sentada en el mostrador. `CrearBackupDelSistemaAsync` solo acepta los tipos `Automatico` y `PreMigracion`. Ver [capítulo 08](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/08-seguridad-y-permisos.md) (2.6 y 3.5).

---

### Pregunta 4.4: *"¿Por qué Ctrl+C no mata el proceso en `Retail.Cli`?"*

* **🎯 Qué busca evaluar el docente:** Cancelación cooperativa y consistencia en operaciones críticas.
* **🗣️ Respuesta corta:** "Porque un corte entre los dos renombrados dejaría el sistema sin base vigente. `CancelacionConsola` captura el Ctrl+C, cancela el `CancellationToken` y deja que la operación se detenga sola; pasado el punto de no retorno, el servicio ignora la cancelación."
* **📖 Respuesta desarrollada:** `e.Cancel = true` le dice a .NET que el programa se ocupa; la primera pulsación pide la cancelación y las siguientes solo repiten el aviso *"cerrar la ventana ahora puede dañar la base"*. En una copia cancelada se borra el parcial y el registro queda `Fallido` ("Copia cancelada."). Si la tarea corre sin consola, el constructor traga la excepción y la copia corre igual. Límite: `CancelacionConsola` no tiene tests (no existe un proyecto de pruebas de `Retail.Cli`). Ver [capítulo 05](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/05-retail-cli.md) (4).

---

## ⏰ Bloque 5: Automatización

### Pregunta 5.1: *"¿Por qué una tarea del Programador de tareas y no un servicio en segundo plano dentro de la app o el SQL Server Agent?"* 🔥

* **🎯 Qué busca evaluar el docente:** Elegir la solución más simple que cumpla el requisito (YAGNI) y reconocer una revisión de diseño.
* **🗣️ Respuesta corta:** "SQL Express no incluye el Agent. Y un servicio dentro de la app solo copia si la app está abierta. Un servicio de Windows aparte sería un componente más. La tarea corre sin sesión, sin app y sin código propio de '¿corresponde copiar?' (D-28)."
* **📖 Respuesta desarrollada:** El diseño inicial (D-14) era híbrido: un servicio en la app más el Programador de tareas, para cubrir LocalDB, que solo existe con la sesión del usuario iniciada. Al pasar a **SQL Server Express** (D-27), un servicio de Windows siempre activo, esa limitación desapareció, y con ella el componente y una familia de reglas por diseñar, probar y defender (¿pasaron 24 horas?, ¿cuenta una copia manual?, ¿se reintenta?). D-28: solo la tarea de las 21:00, con "iniciar si se perdió", sin reintentos. Se pagó el costo de no tener alternativa dentro de la app, pero se ganó previsibilidad. Ver [capítulo 01](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/01-fundamentos-y-decisiones.md) (3.5, con el diagrama de la evolución).

---

### Pregunta 5.2: *"¿Por qué corre como `SYSTEM`? ¿No es la cuenta más peligrosa?"*

* **🎯 Qué busca evaluar el docente:** Mínimo privilegio y modelo de amenazas.
* **🗣️ Respuesta corta:** "Corre sin sesión iniciada y sin guardar contraseñas (D-29). El peligro es real, así que se blindan sus dos puntos de contacto: el `.exe` que ejecuta y lo que escribe. Y en SQL tiene solo cuatro permisos, sin acceso a `USUARIOS`."
* **📖 Respuesta desarrollada:** La alternativa, un usuario con contraseña guardada, se rompe en silencio el día que esa contraseña cambia y es un secreto más. Los cuatro permisos: `CREATE ANY DATABASE` (de servidor), `db_backupoperator`, `SELECT` sobre `__EFMigrationsHistory` y `SELECT, INSERT, UPDATE` sobre `REGISTROS_BACKUP`. No puede leer `password_hash`, no puede tocar ventas, no puede hacer `DELETE` ni cambiar el esquema; un test con `EXECUTE AS LOGIN` lo prueba. Las dos escaladas cubiertas están en la pregunta 6.4. Ver [capítulo 08](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/08-seguridad-y-permisos.md) (4).

---

### Pregunta 5.3: *"¿Qué pasa si la PC está apagada a las 21:00, o si SQL Server todavía no arrancó cuando corre la tarea?"*

* **🎯 Qué busca evaluar el docente:** Resiliencia operativa y tratamiento de condiciones de arranque.
* **🗣️ Respuesta corta:** "La tarea tiene 'iniciar si se perdió': corre al encender la PC. Si el motor todavía no responde, `Retail.Cli backup` espera hasta 2 minutos (reintenta cada 10 segundos) y luego copia una sola vez. Si sigue sin responder, sale con código 1 y deja el motivo en el log."
* **📖 Respuesta desarrollada:** Es un comportamiento de D-28 y del escenario D-01: esperar al motor no es un reintento de la copia; es esperar a que la dependencia esté disponible. La tarea se registra con `-MultipleInstances IgnoreNew` y `-ExecutionTimeLimit` de 2 horas (D-06). El Programador de tareas solo guarda un código de salida, por eso el motivo exacto se escribe en el log. Honestidad: `Retail.Cli` no tiene proyecto de pruebas, así que esta espera se verificó **a mano**, no con un test. Ver [capítulo 07](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/07-escenarios-de-error-y-corrupcion.md) (D-01, D-06) y [capítulo 05](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/05-retail-cli.md) (6.5).

---

### Pregunta 5.4: *"¿Por qué la tarea ejecuta la CLI de `Program Files` y no una copia propia?"*

* **🎯 Qué busca evaluar el docente:** Aprender de un defecto descubierto después del merge.
* **🗣️ Respuesta corta:** "La primera versión copiaba la CLI a otra carpeta, pero esa copia no se actualizaba con la app: tras una actualización con migraciones, la CLI vieja decía que la base era más nueva y la copia diaria fallaba todas las noches. Ahora la tarea apunta a la CLI de la instalación, que solo modifican los administradores."
* **📖 Respuesta desarrollada:** Corrección posterior al PR #23 (rama `fix/tarea-backup-cli-instalada`). Para que sea seguro, el script **se niega a registrar la tarea** si alguien sin privilegios puede reemplazar el ejecutable: revisa la carpeta de instalación, todo su contenido y cada carpeta superior hasta la raíz (porque quien puede renombrar `Retail POS` puede apartar la carpeta original), nombrando los grupos por SID. Registrar la tarea contra la CLI de desarrollo se rechaza a propósito. Ver [capítulo 01](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/01-fundamentos-y-decisiones.md) (3.5) y [capítulo 06](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/06-scripts-y-sql.md) (3.4).

---

### Pregunta 5.5: *"¿Por qué pasaron de LocalDB a SQL Server Express?"*

* **🎯 Qué busca evaluar el docente:** Paridad entre desarrollo y producción.
* **🗣️ Respuesta corta:** "Porque producción usa Express y desarrollar sobre el mismo motor hace aparecer los problemas reales de permisos de la cuenta del servicio. Además elimina configuraciones que solo tenían sentido para LocalDB (D-27)."
* **📖 Respuesta desarrollada:** LocalDB es una instancia por usuario de Windows (R-4), que solo existe con la sesión iniciada. Con Express el motor corre como servicio con su propia cuenta, lo que obliga a dar permisos a la carpeta de respaldos y es precisamente lo que hay que probar. Afecta a todo el equipo, por eso va en una rama y PR propios (`chore/sql-server-express`). Para el código de las capas no cambia nada: solo la configuración. Ver [capítulo 01](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/01-fundamentos-y-decisiones.md) (2.8, 3.5).

---

## 🔐 Bloque 6: Seguridad

### Pregunta 6.1: *"El hash SHA-256 del manifiesto coincide. ¿La copia es auténtica?"* 🔥 (pregunta capciosa)

* **🎯 Qué busca evaluar el docente:** Distinguir integridad de autenticidad.
* **🗣️ Respuesta corta:** "No. El hash prueba integridad: que el archivo es el mismo que describe el manifiesto. Quien trae un `.bak` puede traer un `.json` acorde. La autenticidad la aporta, en parte, la credencial de un Gerente verificada dentro de la copia, y el riesgo residual es R-20."
* **📖 Respuesta desarrollada:** **Integridad** = ¿este archivo es el que se escribió? (SHA-256, `CHECKSUM`). **Autenticidad** = ¿quién lo creó, es de *este* negocio? (nada criptográfico lo prueba). El ataque: fabricar en otro SQL Server una base válida con su propio Gerente y contraseña, generar el `.bak` y el `.json` con la huella correcta y restaurar. La huella coincide porque la calculó el atacante, la credencial coincide porque la fijó él. Se **mitiga**, no se elimina: copia `PreRestauracion` de 30 días, base anterior, `REGISTROS_RESTAURACION` (constancia inmutable con `NombreUsuario`, fecha y origen), aviso en la app durante 7 días y aviso de conciliación con ARCA. Una solución real exigiría firmar las copias con una clave que el atacante no tenga: no se implementó (YAGNI y fuera de alcance). Ver [capítulo 08](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/08-seguridad-y-permisos.md) (3.4).

---

### Pregunta 6.2: *"¿Qué es lo que el sistema NO protege?"* 🔥

* **🎯 Qué busca evaluar el docente:** Madurez para reconocer los límites de un modelo de amenazas.
* **🗣️ Respuesta corta:** "R-17: si alguien usa la misma identidad de Windows que tiene permisos en SQL Server, puede saltearse la aplicación con SSMS o `sqlcmd`. No se resuelve en el código: requiere cuentas separadas y permisos mínimos en el servidor."
* **📖 Respuesta desarrollada:** Con esa identidad se puede leer `USUARIOS` (hashes), cambiar un rol, alterar ventas, hacer `BACKUP` a una carpeta propia, `RESTORE` o `DROP DATABASE`, y leer los `.bak` (la cuenta de la app tiene *Modificar* en la carpeta). La aplicación cliente es **no confiable por definición** (corre en la PC de quien podría atacarla); la única barrera real es lo que el motor le permite a la identidad con la que se conecta. Es un riesgo aceptado y documentado. La práctica recomendada para producción (no implementada): cuentas de Windows separadas por persona, cajero como usuario estándar, la app diaria sin `sysadmin`, copias fuera de la PC y cifradas, al menos dos Gerentes. A futuro, un servicio privilegiado intermediario (*broker*). Ver [capítulo 08](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/08-seguridad-y-permisos.md) (7).

---

### Pregunta 6.3: *"¿Por qué le dan `CREATE ANY DATABASE` a `SYSTEM`? Suena a mucho poder."*

* **🎯 Qué busca evaluar el docente:** Justificación de un permiso de servidor con evidencia.
* **🗣️ Respuesta corta:** "Porque `RESTORE VERIFYONLY` lo exige aunque no cree ninguna base, y sin verificar no hay copia `Verificado`. Hay un test que lo prueba: sin ese permiso la verificación falla. Es estrecho: permite crear bases nuevas, no leer las existentes."
* **📖 Respuesta desarrollada:** `Verificacion_SinElPermisoDeServidor_FallaPorFaltaDeCreateDatabase` hace un `BACKUP` con la cuenta sin el permiso (funciona por `db_backupoperator`) y un `VERIFYONLY` que falla con un mensaje que menciona `CREATE DATABASE`. El riesgo residual es de **disponibilidad** (crear bases vacías y consumir disco), no de confidencialidad. `-Desregistrar -RevocarPermisos` ofrece revocarlo, pero solo si lo concedió este script (otro software como `SYSTEM` podría necesitarlo). Salvedad: el comportamiento exacto de propiedad de las bases creadas responde a la documentación del producto y no se probó. Ver [capítulo 08](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/08-seguridad-y-permisos.md) (4.3).

---

### Pregunta 6.4: *"Expliquen las dos escaladas de privilegios que contemplaron con `SYSTEM`. ¿Por qué no usan `%ProgramData%` para los logs?"* 🔥

* **🎯 Qué busca evaluar el docente:** Conocimiento real de seguridad en Windows, no solo la teoría.
* **🗣️ Respuesta corta:** "Primera: `SYSTEM` ejecutando un `.exe` que un usuario puede reemplazar. Segunda: `SYSTEM` escribiendo logs en una carpeta que un usuario controla y puede convertir en punto de montaje. `ProgramData` se descartó por *folder squatting*: cualquier usuario puede crear carpetas ahí y apropiarse de `Retail POS` antes que la instalación."
* **📖 Respuesta desarrollada:** (1) Si el ejecutable está en una carpeta escribible, el usuario lo reemplaza y a las 21:00 corre su programa como `SYSTEM`. Defensa: instalación como administrador en `Program Files` y el script que se niega a registrar la tarea si la carpeta, su contenido o cualquier carpeta superior es modificable por grupos amplios o por la cuenta de la app (probado con `GetEscrituraEnInstalacion_*`). (2) El usuario convierte una carpeta de logs en un *junction* hacia una carpeta protegida; lo que escribe `SYSTEM` se desvía. Defensa: una sola regla, logs en `%LOCALAPPDATA%\Retail POS\logs` de la cuenta que ejecuta; para `SYSTEM` es su `systemprofile`, donde solo escriben `SYSTEM` y administradores. La instalación queda sin carpetas escribibles por usuarios. Ver [capítulo 08](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/08-seguridad-y-permisos.md) (4.4 y 4.5).

---

### Pregunta 6.5: *"¿Cómo se protegen los `.bak`? ¿Están cifrados?"*

* **🎯 Qué busca evaluar el docente:** Protección de datos personales (Ley 25.326) y honestidad sobre lo no implementado.
* **🗣️ Respuesta corta:** "No están cifrados: SQL Express no comprime ni cifra copias (R-5). Los protege la ACL de `C:\RetailData\Backups`, sin Todos ni Usuarios. Mientras las copias sean locales alcanza; antes de enviarlas a cualquier destino remoto el cifrado es obligatorio."
* **📖 Respuesta desarrollada:** Un `.bak` es la base completa: clientes con datos personales y hashes de contraseñas. La ACL deja Control total a Administradores y `SYSTEM`, y *Modificar* al servicio de SQL Server y a la cuenta que usa la app. En la primera ejecución real se descubrió que `SetAccessRuleProtection($true, $true)` **copiaba** las reglas heredadas y `RemoveAccessRule` no las quitaba (Windows las seguía marcando heredadas en memoria): quedaban *Usuarios autenticados (M)* y *Usuarios (RX)*. Se corrigió con `($true, $false)` más `PurgeAccessRules`, y un test reproduce el caso fabricando una carpeta que hereda permisos amplios. Cifrado a futuro: `ZipArchive` y `Aes` de .NET, sin NuGet (D-16). Ver [capítulo 08](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/08-seguridad-y-permisos.md) (5 y 6).

---

### Pregunta 6.6: *"¿Encontraron algún problema de seguridad mientras documentaban el módulo?"* 🔥

* **🎯 Qué busca evaluar el docente:** Autocrítica técnica y manejo responsable de un hallazgo.
* **🗣️ Respuesta corta:** "Sí. La retención borraba archivos usando la ruta guardada en la tabla, sin comprobar que estuviera dentro de la carpeta de respaldos. Como la tarea corre como `SYSTEM`, quien pudiera escribir en esa tabla podía lograr un borrado arbitrario como `SYSTEM`. Lo corregimos en una rama aparte: el borrado solo ocurre dentro de la carpeta de respaldos y solo de archivos `.bak`."
* **📖 Respuesta desarrollada:** `AplicarRetencionAsync` (y la limpieza de huérfanas) llama a `EliminarCopia(RutaArchivo)`, que hace `File.Delete` del `.bak` y de su `.json`. La cuenta de la tarea tiene `INSERT, UPDATE` sobre `REGISTROS_BACKUP`: con la misma identidad de R-17 se podía insertar un registro `Verificado` con la ruta de un archivo ajeno y fecha vieja; la siguiente copia diaria, como `SYSTEM`, lo borraba al aplicar la retención. No lo mencionaba el registro de diseño y fue una deducción del código, sin prueba que lo reprodujera al momento de documentarlo ([capítulo 08](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/08-seguridad-y-permisos.md), 7.2). Lección: la autorización no termina en el rol; todo dato que llega de la base y se usa como ruta es **entrada no confiable**.

> [!NOTE]
> **Pendiente de confirmación por el orquestador:** este hallazgo se encontró al escribir esta documentación y su corrección está en la rama `fix/respaldos-borrado-seguro` (borrado solo dentro de la carpeta de respaldos y solo de archivos `.bak`). Hasta que esa rama se integre a `main`, el comportamiento descrito en los capítulos 03 y 08 sigue siendo el de `main`. Cuando se confirme el merge, actualizar esta respuesta (de "lo corregimos en una rama" a "se corrigió en el PR #…") y el capítulo 08, sección 7.2.

---

### Pregunta 6.7: *"¿Por qué no alcanza con ocultar el botón 'Restaurar'? ¿Para qué existe el rol Administrador si el Gerente puede todo?"*

* **🎯 Qué busca evaluar el docente:** Autorización en la capa correcta y mínimo privilegio.
* **🗣️ Respuesta corta:** "Ocultar es comodidad, no seguridad: otro código puede llamar al servicio. La regla se verifica en Application (D-9), leyendo el rol de la base en cada operación. El Administrador es soporte: ve el historial y gestiona cajeros y encargados, pero no copia ni restaura, porque un `.bak` es la base completa con datos personales (D-3)."
* **📖 Respuesta desarrollada:** El orden es *autorización de rol → reautenticación → trabajo*: primero se descarta al que no tiene derecho (barato), luego se verifica la contraseña (BCrypt es lento a propósito), recién después se toca el disco. La reautenticación (*step-up*, D-8) protege la sesión que quedó abierta en el mostrador. La matriz de autoridad sobre cuentas vive en el dominio (`Usuario.PuedeGestionarCuentasDeRol`) y previene la escalada dentro de la app (D-4). Pruebas: `CrearBackupManualAsync_ActorQueNoEsGerente_LanzaOperacionNoAutorizadaSinCopiar` y `RestaurarDesdeHistorialAsync_ActorQueNoEsGerente_LanzaOperacionNoAutorizadaSinRestaurar`. Salvedad: los servicios operativos (POS, caja) todavía no verifican el rol; para respaldo y restauración sí. Ver [capítulo 08](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/08-seguridad-y-permisos.md) (2).

---

### Pregunta 6.8: *"La cadena de conexión tiene `TrustServerCertificate=True`. ¿No es inseguro?"*

* **🎯 Qué busca evaluar el docente:** Entender qué protege cada opción y cuándo deja de ser aceptable.
* **🗣️ Respuesta corta:** "El cifrado se mantiene; se omite la verificación de quién es el servidor. Esa verificación protege del hombre en el medio en una red, y acá cliente y servidor están en la misma PC. Dejaría de ser aceptable al pasar a varios puestos por red."
* **📖 Respuesta desarrollada:** El certificado de una instancia local es autofirmado y el controlador cifra por defecto. `Trusted_Connection=True` evita guardar usuario y contraseña de SQL. Para multipuesto habría que instalar un certificado real, usar `Encrypt=True` sin `TrustServerCertificate` y revisar autenticación y R-14 (la invariante del último Gerente no protege dos degradaciones simultáneas). Ver [capítulo 08](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/08-seguridad-y-permisos.md) (6).

---

## 🧪 Bloque 7: Testing

### Pregunta 7.1: *"¿Por qué no usaron mocks para probar `BACKUP` y `RESTORE`? Sería más rápido."*

* **🎯 Qué busca evaluar el docente:** Criterio sobre qué se simula y qué no.
* **🗣️ Respuesta corta:** "Mockeamos la orquestación, no el motor. Que el motor escriba un archivo legible, que `RESTORE ... WITH MOVE` no choque o que `sp_getapplock` se suelte al morir la sesión son comportamientos de SQL Server y Windows. Un mock los reemplazaría por nuestra opinión (R-8)."
* **📖 Respuesta desarrollada:** Pirámide: dominio (49 casos), servicios con `NSubstitute` (99 casos; repositorios en memoria que evalúan los predicados reales y `Received.InOrder` para el orden), integración contra SQL Server Express real y humo de interfaz en hilo STA. Los de integración reciben su servidor por `RETAIL_TEST_SQLSERVER` y crean carpetas accesibles para la cuenta del servicio (con `%TEMP%` el motor da error 5). El CI instala SQL Express antes de las pruebas. Ver [capítulo 09](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/09-como-se-prueba.md) (1 a 3 y 7).

---

### Pregunta 7.2: *"¿Cómo demuestran que la autorreparación de copias huérfanas funciona de verdad?"*

* **🎯 Qué busca evaluar el docente:** Probar la propiedad en la que se apoya un diseño, no solo el código.
* **🗣️ Respuesta corta:** "Un test toma el bloqueo compartido como una copia, busca en `sys.dm_tran_locks` qué sesión lo sostiene, la mata con `KILL` y comprueba que el exclusivo pasa a estar disponible. Eso prueba la propiedad que B-0 necesita: el motor suelta el bloqueo de un proceso muerto."
* **📖 Respuesta desarrollada:** `IntentarAdquirirExclusivoAsync_ElProcesoDeLaCopiaMuere_ElMotorSueltaElCompartidoYSeObtiene`. Detalle de ingeniería: el motor muestra solo los primeros 32 caracteres del recurso en esa vista, así que el nombre de la base de la prueba pone lo distintivo al principio. Complementan: una copia **viva** nunca se cierra como huérfana (`CrearBackupManualAsync_OtraCopiaViva_NoCierraNiTocaSuRegistro`, con una compuerta de dos `TaskCompletionSource`, sin `Task.Delay`). Salvedad: la caída se simula con `KILL`, no cortando la luz. Ver [capítulo 09](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/09-como-se-prueba.md) (4.1 y 4.5.1).

---

### Pregunta 7.3: *"Un test que pasa, ¿prueba algo?"* 🔥 (pregunta capciosa)

* **🎯 Qué busca evaluar el docente:** Pensamiento crítico sobre la calidad de las pruebas.
* **🗣️ Respuesta corta:** "No necesariamente. Nos pasó: un test pasaba en falso porque armaba la 'carpeta externa' con `File.Move`, que conserva la ACL, y el motor sí podía leerla. Lo descubrió un control negativo: antes de actuar, el test intenta un `RESTORE VERIFYONLY` directo y exige error 5."
* **📖 Respuesta desarrollada:** Hallazgo 15 del registro. La funcionalidad probada (traer el `.bak` al directorio de respaldos porque el motor no puede leer otra carpeta) no era necesaria para que el test pasara, así que no estaba probada. Al agregar el control negativo, la precondición falló y se vio. Se corrigió armando la carpeta con copia y borrado, para que el archivo nuevo herede los permisos de la carpeta externa. La regla: un test es confiable cuando uno puede hacerlo fallar a propósito; si la situación que dice proteger no se puede provocar, no protege nada. El mismo criterio está en el test de ACL, que verifica la **precondición** de que la carpeta hija hereda el permiso amplio. Ver [capítulo 09](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/09-como-se-prueba.md) (5.1 y 4.3).

---

### Pregunta 7.4: *"Una de sus pruebas falló una vez y no volvió a fallar. ¿La arreglaron?"* 🔥 (pregunta capciosa)

* **🎯 Qué busca evaluar el docente:** Honestidad intelectual ante una falla no reproducible.
* **🗣️ Respuesta corta:** "No lo podemos afirmar. Falló una vez en cinco corridas, no se reprodujo y no se capturó el error. Aislamos la clase en una colección sin paralelismo como medida preventiva, con la hipótesis de que competía por la instancia con las pruebas de restauración. Quedó documentado como hipótesis."
* **📖 Respuesta desarrollada:** Las pruebas de permisos suplantan cuentas y ejecutan `BACKUP`/`VERIFYONLY` mientras las de restauración hacen `RESTORE`, `DROP`, `SINGLE_USER` y `ClearAllPools` sobre la misma instancia. `PermisosTareaProgramadaColeccion` usa `DisableParallelization = true`. Aislarla elimina la competencia sin tocar lo que las pruebas verifican, pero no demuestra la causa. Si reaparece, hay que capturar el error con `dotnet test --logger trx`. Decir "se arregló" sin evidencia sería peor que reconocer la incertidumbre. Ver [capítulo 07](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/07-escenarios-de-error-y-corrupcion.md) (D-07) y [capítulo 09](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/09-como-se-prueba.md) (5.2).

---

### Pregunta 7.5: *"No pueden ejecutar un test como `SYSTEM`. ¿Cómo prueban sus permisos?"*

* **🎯 Qué busca evaluar el docente:** Ingenio para probar lo que no se puede ejecutar directamente, y claridad sobre los límites.
* **🗣️ Respuesta corta:** "Probamos exactamente lo que el script concede: la fixture lee las sentencias de `$Permisos` del propio script, crea un login de prueba con esos permisos y lo suplanta con `EXECUTE AS LOGIN`. Probamos lo permitido y lo denegado. La tarea real como `SYSTEM` se probó a mano."
* **📖 Respuesta desarrollada:** Denegados con *denied*: leer `VENTAS`, modificar `USUARIOS`, `DELETE` del historial, `CREATE TABLE`, `SELECT password_hash`. Permitidos: leer migraciones, el ciclo de vida del historial, `BACKUP` y `VERIFYONLY`, el bloqueo sobre `master`. Otro test fija la lista exacta de cuatro sentencias para que un permiso nuevo obligue a editar la prueba a propósito. Dos límites: el login no es el `SYSTEM` real, y los permisos de carpeta no se prueban ahí (los cubren los tests de ACL con `powershell.exe` real). Según el registro, la primera prueba manual como `SYSTEM` real fue exitosa el 2026-10-14 y falta repetirla con el script corregido. Ver [capítulo 09](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/09-como-se-prueba.md) (4.2 y 6.1).

---

## 🚧 Bloque 8: Límites y Trabajo Futuro

### Pregunta 8.1: *"¿Qué casos no están cubiertos por tests automáticos?"*

* **🎯 Qué busca evaluar el docente:** Conocimiento exacto del propio sistema.
* **🗣️ Respuesta corta:** "La rama donde también falla la reversión del intercambio, la muerte del proceso entre los dos renombrados, los mensajes y la espera de `Retail.Cli backup` (no tiene proyecto de pruebas), la cancelación real del `BACKUP` y la tarea real como `SYSTEM`. Distinguimos tres grados de certeza: probado, verificado a mano, deducido."
* **📖 Respuesta desarrollada:** Lista completa en [capítulo 09](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/09-como-se-prueba.md) (sección 6, tomada de la sección 7 del [capítulo 07](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/07-escenarios-de-error-y-corrupcion.md)). Incluye también la restauración real de una copia "del futuro" o con migraciones pendientes (solo hay pruebas de servicio y de detección), el bloqueo del cierre del diálogo y `PrepararRutaNueva`. El reinicio real de la app tras restaurar no se probó en la app real.

---

### Pregunta 8.2: *"Si el único Gerente olvida su contraseña, ¿qué pasa?"*

* **🎯 Qué busca evaluar el docente:** Consecuencias de una decisión de seguridad.
* **🗣️ Respuesta corta:** "Hoy solo puede blanquearlo otro Gerente desde la app; con un único Gerente hace falta intervención técnica sobre la base. Es el costo aceptado de eliminar `blanquear-gerente` (R-16). La recomendación operativa es dar de alta al menos dos Gerentes; la solución definitiva es la recuperación por correo, que es futura."
* **📖 Respuesta desarrollada:** El sistema protege al último Gerente (`UltimoGerenteException`) y bloquea el arranque si no hay ninguno activo (`inicializar` lo resuelve solo en ese caso). R-14: la verificación del último Gerente no resiste dos degradaciones simultáneas desde terminales distintas; con un puesto único no ocurre. Ver [capítulo 08](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/08-seguridad-y-permisos.md) (2.5).

---

### Pregunta 8.3: *"¿Esto escala a varias cajas en red?"*

* **🎯 Qué busca evaluar el docente:** Reconocer el alcance de las decisiones.
* **🗣️ Respuesta corta:** "El diseño de las capas sí, pero varias decisiones de seguridad y de concurrencia asumen un solo puesto: `TrustServerCertificate`, R-14, el reinicio de la app y la identidad compartida de Windows. Habría que revisarlas."
* **📖 Respuesta desarrollada:** El bloqueo `sp_getapplock` ya funciona entre procesos y máquinas que comparten el servidor, y el resto de la lógica no depende de la ubicación (puertos). Pero en multipuesto: certificado real y `Encrypt=True`; cuentas de dominio o logins con mínimo privilegio (R-17 se vuelve crítico); las otras terminales pierden la sesión al restaurar; y la tarea programada necesitaría correr en una máquina concreta. Ver [capítulo 08](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/08-seguridad-y-permisos.md) (6 y 7.4).

---

### Pregunta 8.4: *"Si tuvieran más tiempo, ¿qué mejorarían primero?"*

* **🎯 Qué busca evaluar el docente:** Priorización por riesgo.
* **🗣️ Respuesta corta:** "Por riesgo: (1) cerrar R-17 con un servicio privilegiado intermediario o permisos mínimos reales; (2) cifrar copias antes de cualquier destino remoto; (3) recuperación de contraseña por correo; (4) un proyecto de pruebas para `Retail.Cli`; (5) firmar las copias para tener autenticidad."
* **📖 Respuesta desarrollada:** Cada una es una decisión registrada como futura o riesgo aceptado: *broker* (capítulo 08, 7.4), cifrado con `ZipArchive` y `Aes` de .NET sin dependencias (D-16, R-5), correo (D-13, D-16), límite de intentos en consola (R-19) y el instalador real del Track 6.2. No se hicieron ya porque cada una agrega un componente y su superficie de ataque, y el criterio fue YAGNI frente al alcance del Track 6.3.

---

## ⚖️ Matriz de Trade-offs: Decisión → Beneficio → Costo → Alternativa Descartada

| Decisión | Beneficio | Costo aceptado | Alternativa descartada |
| :--- | :--- | :--- | :--- |
| **Base temporal + intercambio** (D-17) | Todo lo que puede fallar ocurre en una base descartable; la vigente no se toca hasta el final; indisponibilidad de segundos | El doble de espacio en disco durante el restore (se controla antes, D-23) | `RESTORE ... WITH REPLACE` sobre la vigente |
| **Bloqueo `sp_getapplock` de lectores y escritor** | Exclusión entre procesos distintos (app, consola, tarea); detección de huérfanas sin heurísticas de tiempo (B-0) | Una conexión propia a `master` por bloqueo; las copias esperan hasta 10 s al exclusivo | Un archivo de bloqueo o un *timeout* ("30 minutos sin avance") |
| **Registro `EnCurso` antes de copiar + índice único filtrado** | Evidencia ante cortes; una sola copia a la vez garantizada por la base | Registros huérfanos que hay que reparar (B-0) | Registrar solo al terminar |
| **Tres controles de integridad** (`CHECKSUM`, `VERIFYONLY`, SHA-256) | Cubren problemas distintos: contenido, legibilidad, cambio posterior del archivo | Tiempo de lectura adicional del archivo | Confiar solo en el éxito de `BACKUP` |
| **Credencial de Gerente verificada dentro de la copia** (D-18) | Restaurar funciona en una PC nueva o con la base corrupta, sin códigos de recuperación | Un restore por intento (R-19); copia fabricada con su propio Gerente pasa (R-20) | Sin autenticación; contra la vigente; procedimiento manual con SSMS (D-19) |
| **Eliminar `blanquear-gerente`** (D-13 revisada) | Ningún comando de la CLI da acceso a una cuenta existente | Recuperación de contraseña dependiente de otro Gerente (R-16) | Mantener el comando; códigos de recuperación |
| **Solo tarea programada, sin servicio en la app** (D-28) | Simplicidad; corre sin app ni sesión; sin reglas de "¿corresponde copiar?" | Sin copia si la PC no se enciende en el día; sin reintentos | Servicio híbrido en la app (D-14); SQL Agent (no incluido en Express) |
| **`SYSTEM` con cuatro permisos** (D-29) | Sin sesión ni contraseñas guardadas; mínimo privilegio verificado por test | `CREATE ANY DATABASE` es de servidor; el login de prueba no es el `SYSTEM` real | Usuario de Windows con contraseña guardada |
| **CLI de `Program Files` + script que se niega a registrar si es modificable** | Actualizar la app actualiza lo que ejecuta la tarea; sin escalada por reemplazo del `.exe` | Instalar como administrador; falla si el árbol de carpetas es escribible | Copia de la CLI a otra carpeta (se desactualizaba) |
| **Logs en `%LOCALAPPDATA%` de cada cuenta** | `SYSTEM` nunca escribe en una carpeta controlada por un usuario | Logs por cuenta, sin centralizar | `%ProgramData%` (*folder squatting*); `logs` junto al ejecutable |
| **Soft delete del historial (`ArchivoPurgado`)** | Constancia de auditoría permanente; la tarea no necesita `DELETE` | Filas que se acumulan | `DELETE` físico de registros |
| **Retención propia de 30 días para `PreRestauracion`** (D-24) | La foto de los datos reales sobrevive a un restore detectado tarde | Más disco ocupado | Retención común de 14 copias |
| **Autorización en Application + reautenticación** (D-8, D-9) | Una UI alterada no concede permisos; una sesión abierta no alcanza | Un BCrypt extra por operación sensible | Controlar permisos solo en el ViewModel |
| **Pruebas de integración contra SQL Express real** | Prueban lo que solo el motor sabe (permisos, bloqueos, `KILL`) | Más lentas; el CI debe instalar el motor (3 a 8 min) | Mocks para `BACKUP`/`RESTORE` |
| **Riesgo R-17 aceptado** | No se agrega un componente intermediario (YAGNI) | Un usuario con la identidad del motor puede saltear la app | Servicio privilegiado intermediario (futuro) |

---

## 🎤 Guion de 5 Minutos para Presentar el Módulo

> Sugerencia: practicar con un cronómetro. Cada tramo tiene el mensaje que tiene que quedar y el material de apoyo.

**Minuto 0:00 a 0:45 — El problema y el requisito.**  
"Una librería guarda todo en una sola base: ventas, caja, clientes, comprobantes. Si se pierde, pierde su historia contable y fiscal. `RNF-08` pide respaldo diario verificado; `RF-22`, copias manuales y restauración controlada; `RF-23`, administrar la base por comandos. El diseño tiene una idea rectora: **un respaldo se mide en su peor día**, así que gran parte del código está en lo que pasa cuando algo falla." *(Apoyo: mapa general del README, tabla de fallas del capítulo 01.)*

**Minuto 0:45 a 1:45 — Hacer una copia en la que se pueda confiar.**  
"Una copia pasa por estados: `EnCurso`, `Exitoso`, `Verificado`; solo `Verificado` es restaurable. El motor la escribe con `CHECKSUM`, la releemos con `VERIFYONLY`, calculamos un SHA-256 y escribimos un manifiesto `.json`. El registro se guarda `EnCurso` *antes* de copiar, y un bloqueo de lectores y escritor de `sp_getapplock` impide que una copia y una restauración se pisen. Si se corta la luz, la siguiente operación consigue el bloqueo exclusivo y eso **garantiza** que lo que quedó `EnCurso` es huérfano, sin mirar el reloj." *(Apoyo: diagrama de estados del capítulo 07; pregunta 2.3.)*

**Minuto 1:45 a 3:00 — Restaurar sin jugársela.**  
"Restaurar es lo más peligroso, así que no lo hacemos sobre la base en uso. Restauramos en una base temporal, validamos huella, espacio, esquema y credencial de un Gerente dentro de la copia, tomamos una copia previa, y solo entonces intercambiamos con dos renombrados de segundos. La base reemplazada queda intacta como `RetailDb_Anterior_<fecha>`. Todo lo que puede fallar ocurre *antes* del punto de no retorno, y un test con `Received.InOrder` lo prueba. Con rigor: antes del intercambio solo cambia el historial de la vigente, no los datos de negocio." *(Apoyo: diagrama Antes/Durante/Después del capítulo 04; preguntas 3.1 a 3.4.)*

**Minuto 3:00 a 3:45 — Automatización y seguridad.**  
"La copia diaria es una tarea programada a las 21:00 como `SYSTEM`, sin sesión ni contraseñas guardadas, con cuatro permisos en SQL y ninguno sobre `USUARIOS`. Como `SYSTEM` es poderosa, blindamos lo que ejecuta (la CLI instalada en `Program Files`, validada por el script) y lo que escribe (logs en el perfil de cada cuenta). Eliminamos `blanquear-gerente` porque con una sesión de Windows compartida le daba el control al cajero." *(Apoyo: preguntas 4.1, 5.2, 6.4.)*

**Minuto 3:45 a 4:30 — Cómo lo probamos.**  
"Pirámide de tests: dominio, servicios con dobles, e integración contra SQL Express real, porque `BACKUP`, `RESTORE` y los bloqueos son comportamientos del motor. Probamos la autorreparación matando una sesión con `KILL`, los permisos con `EXECUTE AS LOGIN` leyendo el propio script, y la ACL con un `powershell.exe` real. Un control negativo nos mostró un test que pasaba por la razón equivocada." *(Apoyo: capítulo 09; preguntas 7.2 y 7.3.)*

**Minuto 4:30 a 5:00 — Lo que no hacemos y lo que encontramos.**  
"No protegemos contra quien use la identidad de Windows del motor con SSMS (R-17): requiere cuentas separadas. La integridad del manifiesto no es autenticidad (R-20). Y al documentar encontramos que la retención borraba archivos sin validar la ruta; está corregido en una rama aparte. Cada hueco está listado, con su grado de certeza." *(Apoyo: preguntas 6.1, 6.2, 6.6 y 8.1.)*

---

## 📚 Ver También

| Documento | Para qué |
| :--- | :--- |
| [Índice de la guía de Respaldo y Restauración](../05-casos-de-uso-y-flujos/respaldo-y-restauracion/README.md) | Glosario y orden de lectura de los nueve capítulos |
| [Preguntas Trampa de Profesores](preguntas-trampa-de-profesores.md) | Preguntas generales de arquitectura, persistencia y resiliencia |
| [Matriz de Trade-offs de Arquitectura](matriz-tradeoffs-arquitectura.md) | Decisiones tecnológicas del proyecto en general |
| [`etapa-6-track-6.3-backup-restore.md`](file:///c:/Users/lucas/Proyectos/retail/docs/roadmap/etapa-6-track-6.3-backup-restore.md) | Registro de decisiones D-1 a D-29 y riesgos R-1 a R-20 |
