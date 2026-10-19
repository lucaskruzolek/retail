# sp\_Login: análisis y propuesta

Oct 10, 2026 · @lucas

El `sp_Login` de la rama `feature/login-sp` no puede ejecutarse tal como está: falla al crearse, no puede verificar contraseñas BCrypt y la aplicación todavía no lo llama. Proponemos dejar la verificación de la contraseña en C# y llevar al SP el **registro del intento de login** (bloqueo por intentos fallidos y auditoría), con una **vista** para leer las credenciales, que es lo que pidió el docente.

## Qué trae hoy la rama

La rama tiene un solo commit (`b430a5e`) y agrega el script del SP, pero ninguna parte de la aplicación lo crea en la base ni lo llama.

| Archivo | Cambio | Observación |
| --- | --- | --- |
| `src/Retail.Infrastructure/SqlScripts/sp_Login.sql` | `CREATE PROCEDURE dbo.sp_Login` | Ninguna migración lo ejecuta, así que la base nunca lo tiene. |
| `docs/MAPA_DEL_PROYECTO.md` | Link al `.sql` en la fila RF-01 | El link apunta a `c:/Users/pablo/...`; el resto del mapa usa la ruta del repo. |
| `wiki/.obsidian/workspace.json` | Estado del editor Obsidian | Es configuración local del editor; conviene sacarlo del commit y agregarlo al `.gitignore`. |
| `wiki/2026-10-08.md` | Archivo vacío | Nota diaria que Obsidian crea solo; se puede borrar. |

`AuthService.LoginAsync` sigue buscando al usuario con `IRepository<Usuario>.FindAsync`, como antes. Para que el login use un SP hacen falta tres cosas que hoy no están: una **migración** que lo cree, un **servicio de Infraestructura** que lo ejecute y que `AuthService` llame a ese servicio.

## Errores del SP actual

El SP tiene dos errores que impiden usarlo (1 y 2), uno de seguridad (3) y dos que aparecen con datos reales (4 y 5). Esta es la parte central del script:

```sql
SELECT  @IdUsuario      = u.Id,
        @NombreCompleto = u.NombreCompleto,
        @Rol            = u.IdRol,
        @StoredHash     = u.PasswordHash,
        @Resultado      = CASE
                             WHEN u.IsDeleted = 1 THEN 2
                             WHEN @StoredHash <> @PasswordHash THEN 1
                             ELSE 0
                           END
FROM dbo.Usuarios u
WHERE u.NombreUsuario = @NombreUsuario;
```

| # | Error | Qué pasa | Gravedad |
| --- | --- | --- | --- |
| 1 | Usa nombres de propiedades de C# en lugar de nombres de columnas | El `CREATE PROCEDURE` falla con *Invalid column name* | Impide crearlo |
| 2 | Compara el hash de la contraseña por igualdad | Ninguna contraseña correcta pasaría nunca | Impide usarlo |
| 3 | Usa `@StoredHash` en el mismo `SELECT` que lo carga | Puede aceptar cualquier contraseña | Seguridad |
| 4 | No contempla varios usuarios con el mismo login | Puede responder "inactivo" a una cuenta activa | Alta |
| 5 | Devuelve `NombreCompleto` | Esa columna desaparece con `feat/normalizacion-datos-maestros` | Alta |

**1. Nombres de columnas.** EF Core traduce cada propiedad a una columna con otro nombre, definido en `UsuarioConfiguration.cs`. El SQL tiene que usar el nombre de la columna, no el de la propiedad:

| Propiedad en C# (lo que usa el SP) | Columna real en `USUARIOS` |
| --- | --- |
| `Id` | `id_usuario` |
| `NombreUsuario` | `nombre_usuario` |
| `PasswordHash` | `password_hash` |
| `IdRol` | `id_rol` |
| `IsDeleted` | No existe: se calcula en C# a partir de `deleted_at` |

SQL Server deja crear un SP que nombra una tabla que todavía no existe, pero no uno que nombra columnas inexistentes de una tabla que sí existe. Por eso el script falla al ejecutarse.

**2. El hash no se puede comparar por igualdad.** BCrypt agrega un *salt* aleatorio cada vez que calcula un hash, y lo guarda dentro del propio hash. La misma contraseña da un resultado distinto en cada cálculo (hashes de ejemplo):

```
Admin123!  ->  $2a$11$Qm3kZ1v8yJcN0pT5hL2eXe...
Admin123!  ->  $2a$11$7FbW4sRkD9qA1mVx6uYcOu...
```

Para verificar, la aplicación usa `BCrypt.Verify(contraseña, hashGuardado)`: lee el salt del hash guardado y repite el cálculo con ese salt. Un `@StoredHash <> @PasswordHash` nunca da igual, salvo que el hash que llega se haya calculado con el salt guardado, y para eso habría que leerlo antes de la base. Además, `password_hash` es `nvarchar(255)` y el parámetro es `VARBINARY(64)`.

**3. Orden de evaluación.** Cuando un mismo `SELECT` asigna varias variables, SQL Server no garantiza en qué orden lo hace. Si el `CASE` se evalúa antes de cargar `@StoredHash`, la variable sigue en `NULL`. `NULL <> valor` no da verdadero sino desconocido, así que el `CASE` cae en `ELSE 0`, que significa **OK**. La comparación tiene que hacerse contra la columna (`u.password_hash`), no contra una variable que se carga en la misma sentencia.

**4. Usuarios repetidos por baja lógica.** El índice único de `nombre_usuario` solo aplica a los usuarios activos. Si se da de baja a `lucas` y se crea otro `lucas`, quedan dos filas con el mismo login. Con dos filas, cada variable termina con el valor de una fila cualquiera, y el SP puede devolver el estado de la cuenta dada de baja. `AuthService` hoy cubre ese caso eligiendo primero la fila activa (test `LoginAsync_UsuarioRecreadoConMismoUsername_PriorizaUsuarioActivoYAutenticaExitosamente`).

**5. Cambio de esquema.** La rama de normalización separa `nombre_completo` en `nombre` y `apellido`, y guarda el login en minúsculas. Cualquier SQL que lea `USUARIOS` tiene que escribirse sobre ese esquema nuevo.

## Por qué el login completo no puede ser un SP

El paso central del login es verificar la contraseña con BCrypt, y SQL Server no tiene BCrypt. El resto del login depende de ese paso.

Para que el SP decidiera solo si la contraseña es correcta, habría dos caminos, y ninguno sirve:

- **Calcular el hash en C# y comparar en SQL.** Para calcularlo hace falta el salt, y el salt está dentro del hash guardado. La aplicación tendría que leer primero el hash de la base, y en ese caso ya puede verificar en C# con `BCrypt.Verify`: el SP no aporta nada.
- **Guardar un hash sin salt, o con un salt fijo,** para poder compararlo por igualdad. Eso incumple **RNF-04** ("hashing seguro con BCrypt o PBKDF2 con salt"): dos usuarios con la misma contraseña tendrían el mismo hash, y una tabla precalculada la revelaría.

Hay un segundo motivo. `SISTEMA_DE_PERSISTENCIA.md` (Ley 6) prohíbe poner reglas de negocio en SP o triggers, porque ahí no se pueden testear con xUnit ni versionar junto al resto del Dominio.

La opción contraria, un SP que solo hace un `SELECT` parametrizado y deja la verificación en C#, tampoco alcanza. El docente lo dijo al consultarle: un SP que solo lee se comporta como una vista, y no cumple la función de un SP.

| Opción | ¿Verifica la contraseña de forma segura? | ¿Es un SP de verdad? | ¿Respeta la Ley 6? |
| --- | --- | --- | --- |
| Login completo en el SP | No: obliga a debilitar el hash (RNF-04) | Sí | No |
| SP que solo lee las credenciales | Sí, en C# | No: es una vista | Sí |
| **Vista para leer + SP que registra el intento** | **Sí, en C#** | **Sí: transacción, escrituras y decisiones** | **Casi: la política llega como parámetro desde C#** |

La tercera fila es la propuesta de la sección siguiente: cada objeto de la base hace lo que le corresponde.

## Propuesta: una vista para leer y un SP para registrar el intento

El login queda en tres pasos: la aplicación lee las credenciales de una **vista**, verifica la contraseña con BCrypt en C#, y después un **SP** registra el resultado del intento.

&#91;embedded content: login propuesto · 3 pasos: vista, BCrypt en C#, SP\]

La base interviene dos veces: la vista lee las credenciales (paso 1) y el SP registra el intento (paso 3). La contraseña se verifica en C# (paso 2), porque SQL Server no tiene BCrypt.

**La vista `vw_CredencialesUsuario`.** Devuelve, para un login, el hash guardado, nombre, apellido, rol, si está dado de baja y hasta cuándo está bloqueado. Solo lee, así que es una vista y no un SP, como indicó el docente. La aplicación la consulta con `WHERE nombre_usuario = @login`.

**El SP `sp_RegistrarIntentoLogin`.** Recibe el resultado de `BCrypt.Verify` y, dentro de una transacción:

1. Bloquea la fila de estado de esa cuenta, para que otra caja no la modifique al mismo tiempo.
2. Si la cuenta está bloqueada, rechaza el intento aunque la contraseña sea correcta.
3. Si la contraseña es correcta, pone el contador de fallos en cero y guarda la hora del último acceso.
4. Si es incorrecta, suma 1 al contador; al llegar al máximo, bloquea la cuenta durante N minutos.
5. Registra el intento en la tabla de auditoría, sea cual sea el resultado.
6. Devuelve el resultado (aceptado, rechazado o bloqueado), los intentos que quedan y la hora de desbloqueo.

Esbozo:

```sql
CREATE OR ALTER PROCEDURE dbo.sp_RegistrarIntentoLogin
    @nombre_usuario       NVARCHAR(50),
    @id_usuario           INT = NULL,       -- NULL si el login no existe
    @credenciales_validas BIT,              -- resultado de BCrypt.Verify en C#
    @max_intentos         INT,              -- la política la decide la aplicación
    @minutos_bloqueo      INT,
    @equipo               NVARCHAR(100),
    @resultado            TINYINT   OUTPUT, -- 0 aceptado, 1 rechazado, 2 bloqueado
    @intentos_restantes   INT       OUTPUT,
    @bloqueado_hasta      DATETIME2 OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;   -- ante cualquier error, se deshace toda la transacción
    BEGIN TRANSACTION;
        -- 1. leer la fila de ACCESOS_USUARIOS con WITH (UPDLOCK, HOLDLOCK)
        -- 2 a 4. decidir y actualizar el contador y el bloqueo
        -- 5. INSERT INTO AUDITORIA_ACCESOS (...)
    COMMIT TRANSACTION;
END
```

**Dos tablas nuevas.**

| Tabla | Una fila por | Columnas | Quién escribe |
| --- | --- | --- | --- |
| `ACCESOS_USUARIOS` | Usuario | `id_usuario`, `intentos_fallidos`, `bloqueado_hasta`, `ultimo_acceso` | Solo el SP |
| `AUDITORIA_ACCESOS` | Intento de login | `fecha`, `nombre_usuario`, `id_usuario` (nulo si no existe), `resultado`, `equipo` | Solo el SP |

El estado de bloqueo va en una tabla aparte y no en `USUARIOS`. A `USUARIOS` la escribe el agregado `Usuario` desde EF Core; si el SP también escribiera esa fila, dos partes del sistema la modificarían y podrían pisarse. Con la tabla aparte, cada tabla tiene un solo responsable.

**Tres decisiones que conviene sostener en la defensa.**

- **La contraseña se sigue verificando en C#.** BCrypt no existe en SQL Server y RNF-04 exige un hash con salt.
- **El SP no decide la política:** la cantidad de intentos y los minutos de bloqueo le llegan como parámetros desde `appsettings.json`. Las reglas del negocio quedan en la aplicación; el SP aplica el cambio de estado de forma atómica.
- **Un login inexistente también se audita,** con `id_usuario` nulo. La pantalla muestra el mismo mensaje en los dos casos, para no revelar qué usuarios existen.&#32;

## Qué cambia en el código

La vista y el SP se crean con una migración de EF Core. Así existen en cualquier base que arranque la aplicación, igual que las tablas: `MigrateAsync` la aplica sola.

| Capa | Archivo | Qué hace |
| --- | --- | --- |
| Infraestructura | Migración `AgregarRegistroDeAccesos` | Crea las dos tablas, la vista y el SP con `migrationBuilder.Sql("CREATE OR ALTER ...")`; `Down()` los elimina. |
| Infraestructura | `Persistence/Services/CredencialesQueryService.cs` | Lee la vista con `SqlQuery<T>`, una consulta parametrizada y sin riesgo de inyección SQL. |
| Infraestructura | `Persistence/Services/RegistroAccesosService.cs` | Ejecuta el SP y lee sus parámetros de salida. |
| Application | `Interfaces/Persistence/ICredencialesQueryService.cs` e `IRegistroAccesos.cs` | Contratos que usa `AuthService`, sin saber que del otro lado hay una vista y un SP. Siguen el mismo patrón que `IClienteQueryService`. |
| Application | `AuthService.cs` | Lee, verifica con BCrypt, registra el intento y lanza `CuentaBloqueadaException` cuando corresponde. |
| Application | `PoliticaBloqueoOptions` | Intentos máximos y minutos de bloqueo, leídos de `appsettings.json`. |
| UI | `LoginViewModel.cs` | Muestra "Cuenta bloqueada hasta las HH:mm" o "Le quedan N intentos". |
| Tests | `AuthServiceTests` + test de integración del SP | Unitarios con mocks, y contra LocalDB: activo, dado de baja, bloqueo, desbloqueo por tiempo y dos conexiones simultáneas que no pierden incrementos. |
| Docs | ERS, `SISTEMA_DE_PERSISTENCIA.md`, `DER.mmd`, `MAPA_DEL_PROYECTO.md` | Requisito nuevo, excepción a la Ley 6 explicada, tablas nuevas y archivos nuevos. |

**Por qué el SQL va dentro de la migración y no en un `.sql` aparte.** Una migración no se modifica después de aplicada. Si leyera un `.sql` y alguien editara ese archivo más adelante, una base creada hoy y otra creada mañana tendrían SP distintos. Para cambiar el SP se agrega otra migración con un nuevo `CREATE OR ALTER`.

## Pasos y decisiones abiertas

La rama actual no se puede reutilizar: el SP hay que reescribirlo sobre el esquema nuevo. Conviene arrancar una rama nueva desde `main` cuando entre la normalización.

- [ ] Mergear `feat/normalizacion-datos-maestros` (cambia las columnas de `USUARIOS` que lee la vista).
- [ ] Confirmar la propuesta con el docente: "un SP transaccional que registra cada intento de login, bloquea la cuenta tras N fallos de forma atómica entre cajas y audita los accesos; las credenciales se leen de una vista".
- [ ] Agregar el requisito a la ERS (por ejemplo, RF-01b: bloqueo temporal por intentos fallidos y auditoría de accesos).
- [ ] Crear la rama nueva desde `main` e implementar según la tabla de la sección anterior.
- [ ] Cerrar `feature/login-sp` y agregar `wiki/.obsidian/workspace*.json` al `.gitignore`.

**Decisiones abiertas**

| Tema | Propuesta | Alternativa |
| --- | --- | --- |
| Política de bloqueo | 5 intentos fallidos, 15 minutos | Otros valores; quedan en `appsettings.json` |
| Desbloqueo antes de tiempo | Que el bloqueo venza solo | Que el Gerente pueda desbloquear desde Usuarios, con un segundo SP chico |
| Nombre del SP | `sp_RegistrarIntentoLogin`, que dice lo que hace | Mantener `sp_Login`, si el docente ya lo conoce con ese nombre |
