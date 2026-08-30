# Retail.Server.Api (Capa de Presentación Backend / ASP.NET Core Web API)

`Retail.Server.Api` es el **punto de entrada HTTP/REST centralizado** del sistema en la Red de Área Local (LAN). Se ejecuta en el servidor de la tienda y expone los servicios de negocio a las terminales de mostrador y administración mediante una API segura con tokens JWT, middlewares de validación de sesión y documentación interactiva vía Swagger/OpenAPI.

---

## 🎯 Objetivos de la Capa

1. **Exponer los Endpoints RESTful en la LAN (`IS-01`, `IC-01`):**
   Proveer una interfaz de comunicación HTTP rápida (< 150 ms para ventas en mostrador) y fuertemente tipada para los clientes WPF.
2. **Gestionar la Autenticación y Autorización RBAC (`RF-01`, `RNF-06`):**
   Validar tokens JWT y restringir el acceso a endpoints según el rol del usuario (`[Authorize(Roles = "Cajero,Encargado,Gerente")]`).
3. **Controlar la Sesión Única y el Desalojo Inmediato (*Kick-Out*) (`RF-02`):**
   Interceptar las peticiones mediante un middleware que verifica la validez del token contra la tabla `SESIONES_ACTIVAS`, bloqueando de inmediato cualquier terminal desalojada.
4. **Manejo Centralizado de Errores con Formato ProblemDetails (RFC 7807):**
   Traducir excepciones de dominio y validaciones a respuestas HTTP estándar (400 Bad Request, 401 Unauthorized, 403 Forbidden, 409 Conflict, 500 Internal Server Error) sin exponer detalles de la infraestructura.

---

## 📁 Estructura del Directorio

```text
src/Backend/Retail.Server.Api/
├── Controllers/                         # Controladores REST organizados por módulo
│   ├── AuthController.cs                # /api/auth (login, kick-out, heartbeat, logout)
│   ├── VentasController.cs              # /api/ventas, /api/ventas/devolucion
│   ├── PresupuestosController.cs        # /api/presupuestos
│   ├── ArticulosController.cs            # /api/articulos, /api/articulos/stock-minimo
│   ├── ProveedoresController.cs         # /api/proveedores, /api/proveedores/importar
│   ├── CajaController.cs                # /api/caja/apertura, /api/caja/movimientos, /api/caja/cierre
│   ├── ComprasController.cs             # /api/compras
│   ├── FiscalController.cs              # /api/fiscal/reintentos (Consola Gerencial)
│   └── UsuariosController.cs            # /api/usuarios, /api/usuarios/sesiones
├── Middlewares/                         # Pipeline de procesamiento de solicitudes HTTP
│   ├── ExceptionHandlingMiddleware.cs   # Captura global de excepciones -> ProblemDetails
│   └── SessionValidationMiddleware.cs   # Validación de sesión activa y desalojo (Kick-Out)
├── Program.cs                           # Configuración de servicios, DI, JWT, Swagger y Pipeline
└── appsettings.json                     # Conexión SQL Server (localhost), JWT Secret, arcasdk URL
```

---

## ⚙️ Controladores y Endpoints Principales

| Controlador | Ruta Base | Rol Permitido | Descripción y Requisitos |
| :--- | :--- | :--- | :--- |
| `AuthController` | `/api/auth` | Anónimo / Todos | Login (`RF-01`), Desalojo de terminal previa (`RF-02`), Emisión periódica de Heartbeat. |
| `VentasController` | `/api/ventas` | `Cajero, Encargado, Gerente` | Cobro rápido multimedio y descuento atómico (`RF-09`, `RF-10`), Devoluciones (`RF-13`). |
| `PresupuestosController` | `/api/presupuestos` | `Cajero, Encargado, Gerente` | Creación de presupuestos con 15 días de validez sin reserva de stock (`RF-11`, `RF-12`). |
| `ArticulosController` | `/api/articulos` | `Encargado, Gerente` | ABM Artículos, cálculo de Markup % (`RF-04`, `RF-05`), Alertas de stock mínimo (`RF-08`). |
| `ProveedoresController` | `/api/proveedores` | `Encargado, Gerente` | Gestión de proveedores y subida multipart de planillas Excel/CSV (`RF-07`). |
| `CajaController` | `/api/caja` | `Cajero, Encargado, Gerente` | Apertura de turno (`RF-14`), Movimientos varios (`RF-15`), Arqueo Ciego y cierre (`RF-16`). |
| `ComprasController` | `/api/compras` | `Encargado, Gerente` | Registro de facturas/remitos, incremento de stock físico y costos (`RF-20`). |
| `FiscalController` | `/api/fiscal` | `Gerente` | Consola de reintentos de facturación ARCA en lote para ventas en contingencia (`RF-17`, `RF-18`, `RF-19`). |
| `UsuariosController` | `/api/usuarios` | `Gerente` | ABM de cuentas de usuario, reseteo de claves y supervisión de sesiones activas (`RF-03`). |

---

## 🛡️ Middlewares y Seguridad

### 1. `SessionValidationMiddleware.cs` (Garantía de Sesión Única)
Intercepta cada petición que porte un token JWT válido y extrae el claim `id_usuario`. Compara el hash del token recibido contra el `token_hash` persistido en la tabla `SESIONES_ACTIVAS`:
- **Si coincide:** Permite continuar la petición.
- **Si no coincide (porque el usuario inició sesión en otra terminal física con Kick-Out):** Retorna inmediatamente **HTTP 401 Unauthorized** con el mensaje `"Sesión cerrada: Has iniciado sesión en otra terminal"`.

```csharp
public class SessionValidationMiddleware
{
    private readonly RequestDelegate _next;

    public SessionValidationMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IRetailDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var tokenHashClaim = context.User.FindFirst("TokenHash")?.Value;

            if (int.TryParse(userIdClaim, out var userId))
            {
                var sesionActiva = await dbContext.SesionesActivas.FindAsync(userId);
                if (sesionActiva == null || sesionActiva.TokenHash != tokenHashClaim)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new { message = "Sesión invalidada por inicio en otra terminal." });
                    return;
                }
            }
        }

        await _next(context);
    }
}
```

### 2. `ExceptionHandlingMiddleware.cs` (ProblemDetails RFC 7807)
Traduce de forma determinística las excepciones de la aplicación a respuestas estandarizadas:

| Tipo de Excepción | Código HTTP | Mensaje / Detalle |
| :--- | :--- | :--- |
| `ValidationException` (FluentValidation) | `400 Bad Request` | Diccionario con lista de errores por campo. |
| `StockInsuficienteException` | `409 Conflict` | Detalle del artículo y stock disponible vs. solicitado. |
| `CajaCerradaException` | `400 Bad Request` | Indicación de que se requiere apertura de turno previa. |
| `SesionActivaException` | `409 Conflict` | Terminal previa y hora de inicio para cuadro de diálogo modal. |
| `DomainException` | `400 Bad Request` | Mensaje de la regla de negocio violada. |
| `Exception` no controlada | `500 Internal Error` | `"Ha ocurrido un error inesperado en el servidor."` (logueando el stack trace internamente). |

---

## ⚙️ Configuración en `Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);

// Inyección de capas desacopladas
builder.Services.AddShared();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Configuración de JWT Bearer
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { /* Configuración de Issuer, Audience y SecretKey */ });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseMiddleware<SessionValidationMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();
```

---

## 🚫 Reglas de Dependencia

- **Dependencias Permitidas:** `Retail.Server.Api` depende de `Retail.Application`, `Retail.Infrastructure` (para la configuración de DI) y `Retail.Shared`.
- **Sin Lógica de Negocio en Controladores:** Los controladores deben ser delgados (*thin controllers*); su única función es recibir el DTO, invocar al servicio de aplicación correspondiente y retornar el código de estado HTTP adecuado.
