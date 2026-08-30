# Retail.Shared (Capa Compartida / Shared Kernel)

`Retail.Shared` es una biblioteca de clases de **.NET 8** completamente agnóstica de infraestructura, persistencia o frameworks visuales. Representa el **contrato de comunicación común** y tipado entre el Backend Servidor (`Retail.Server.Api` / `Retail.Application`) y el Cliente de Escritorio (`Retail.Client.Wpf`).

---

## 🎯 Objetivos de la Capa

1. **Definir el Contrato de Comunicación (Single Source of Truth):**
   Garantizar que tanto la Web API como el cliente WPF compartan exactamente los mismos modelos de datos (DTOs), enumeraciones y rutas de API, evitando desalineaciones tipadas o discrepancias en la serialización JSON.
2. **Desacoplamiento Estricto de Persistencia y UI:**
   Aislar las entidades de dominio y el modelo relacional de base de datos (`Microsoft SQL Server` / EF Core) de la red, asegurando que la información sensible (como `password_hash` o datos de auditoría interna) nunca se exponga en los payloads de red.
3. **Mantenibilidad y Estandarización de Rutas y Seguridad:**
   Centralizar constantes del sistema (rutas de endpoints REST, nombres de claims, identificadores de roles) para evitar cadenas mágicas (*magic strings*) en controladores y clientes HTTP.

---

## 📁 Estructura del Directorio

```text
src/Shared/Retail.Shared/
├── DTOs/                                # Objetos de Transferencia de Datos serializables en JSON
│   ├── Auth/                            # Autenticación, Kick-out y Heartbeat
│   │   ├── LoginRequestDto.cs
│   │   ├── LoginResponseDto.cs
│   │   ├── KickOutRequestDto.cs
│   │   └── HeartbeatRequestDto.cs
│   ├── Ventas/                          # Ventas de mostrador y cobros multimedio
│   │   ├── CrearVentaDto.cs
│   │   ├── DetalleVentaDto.cs
│   │   ├── PagoVentaDto.cs
│   │   ├── VentaResponseDto.cs
│   │   └── DevolucionVentaDto.cs
│   ├── Presupuestos/                    # Cotizaciones temporales sin reserva de stock
│   │   ├── CrearPresupuestoDto.cs
│   │   └── PresupuestoResponseDto.cs
│   ├── Articulos/                       # ABM Artículos, catálogos y markup
│   │   ├── ArticuloDto.cs
│   │   ├── CrearArticuloDto.cs
│   │   ├── ActualizarArticuloDto.cs
│   │   └── CatalogoProveedorDto.cs
│   ├── Proveedores/                     # Proveedores e importaciones de planillas
│   │   ├── ProveedorDto.cs
│   │   └── ImportacionCatalogoRequestDto.cs
│   ├── Compras/                         # Ingreso de facturas y remitos de distribuidores
│   │   ├── CrearCompraDto.cs
│   │   └── DetalleCompraDto.cs
│   ├── Caja/                            # Apertura, movimientos y arqueo ciego
│   │   ├── AperturaTurnoDto.cs
│   │   ├── MovimientoCajaDto.cs
│   │   ├── ArqueoCiegoDto.cs
│   │   └── CierreTurnoResponseDto.cs
│   ├── Fiscal/                          # Respuestas de CAE y reintentos fiscales
│   │   ├── ComprobanteFiscalDto.cs
│   │   └── ReintentoFiscalDto.cs
│   └── Usuarios/                        # Administración de cuentas y sesiones
│       ├── UsuarioDto.cs
│       ├── CrearUsuarioDto.cs
│       └── SesionActivaDto.cs
├── Enums/                               # Enumeraciones del Dominio compartidas
│   ├── RolUsuarioEnum.cs                # Cajero, Encargado, Gerente
│   ├── EstadoTurnoEnum.cs               # ABIERTO, CERRADO
│   ├── TipoMovimientoCajaEnum.cs        # INGRESO, EGRESO
│   ├── TipoOperacionVentaEnum.cs        # VENTA_DIRECTA, PRESUPUESTO
│   ├── EstadoFiscalEnum.cs              # EMITIDO, ERROR_FISCAL_REINTENTABLE, NO_APLICA
│   ├── TipoComprobanteFiscalEnum.cs     # FACTURA_A, FACTURA_B, NOTA_CREDITO_A, NOTA_CREDITO_B
│   └── MedioPagoEnum.cs                 # EFECTIVO, TARJETA_DEBITO, TARJETA_CREDITO, TRANSFERENCIA_QR
└── Constants/                           # Constantes globales del sistema
    ├── ApiRoutes.cs                     # Rutas estandarizadas de endpoints
    └── SecurityConstants.cs             # Nombres de Roles, Claims y Políticas
```

---

## ⚙️ Funcionamiento Detallado por Componente

### 1. DTOs (Data Transfer Objects)
Los DTOs son clases `record` o `class` puras con propiedades autogeneradas (`get; set;` o `init;`), diseñadas para serializarse a JSON en la comunicación HTTP/HTTPS (`IS-01`, `IC-01`).

* **Módulo de Autenticación y Sesiones (`RF-01`, `RF-02`):**
  * `LoginRequestDto`: Envía `NombreUsuario` y `Password` plano sobre HTTPS/LAN.
  * `LoginResponseDto`: Devuelve el `TokenJwt`, `NombreUsuario`, `Rol`, `FechaExpiracion` y si se requirió confirmación de sesión previa.
  * `KickOutRequestDto`: Transporta la confirmación del usuario para invalidar la terminal anterior y tomar control de la cuenta.
  * `HeartbeatRequestDto`: Envía periódicamente el identificador de terminal y token hash para mantener activa la sesión (`SESIONES_ACTIVAS`).

* **Módulo de Ventas y Cobros (`RF-09`, `RF-10`, `RF-13`):**
  * `CrearVentaDto`: Contiene la lista de ítems (`DetalleVentaDto`) y la lista de formas de pago fraccionadas (`PagoVentaDto`).
  * `PagoVentaDto`: Registra el `MedioPago` (Efectivo, Débito, Crédito, QR), monto y referencias de cupón o transacción.
  * `DevolucionVentaDto`: Permite especificar el `IdVentaOriginal`, ítems devueltos y motivo para la emisión de Nota de Crédito.

* **Módulo de Presupuestos (`RF-11`, `RF-12`):**
  * `CrearPresupuestoDto`: Datos del presupuesto con validez temporal de 15 días, sin alterar stock.
  * `PresupuestoResponseDto`: Incluye la leyenda legal *"Documento No Válido como Factura"*.

* **Módulo de Caja y Arqueo Ciego (`RF-14`, `RF-15`, `RF-16`):**
  * `AperturaTurnoDto`: Saldo inicial declarado por el cajero.
  * `ArqueoCiegoDto`: Declaración física de dinero en efectivo y cupones recaudados **sin conocer el saldo teórico del sistema**.
  * `CierreTurnoResponseDto`: Generado por el backend tras el arqueo ciego, conteniendo el balance teórico, lo declarado y la diferencia (*sobrante/faltante*).

* **Módulo de Facturación ARCA (`RF-17`, `RF-18`, `RF-19`):**
  * `ComprobanteFiscalDto`: Contiene CAE, fecha de vencimiento de CAE, número de comprobante, punto de venta y resultado ARCA.
  * `ReintentoFiscalDto`: Solicitud para reprocesar comprobantes en estado `ERROR_FISCAL_REINTENTABLE`.

### 2. Enums (Tipos Enumerados Fuertes)
Garantizan la consistencia de estados entre el motor relacional y la interfaz de usuario:

```csharp
namespace Retail.Shared.Enums;

public enum RolUsuarioEnum
{
    Cajero = 1,
    Encargado = 2,
    Gerente = 3
}

public enum EstadoFiscalEnum
{
    EMITIDO,
    ERROR_FISCAL_REINTENTABLE,
    NO_APLICA
}

public enum MedioPagoEnum
{
    EFECTIVO,
    TARJETA_DEBITO,
    TARJETA_CREDITO,
    TRANSFERENCIA_QR
}
```

### 3. Constants (Rutas y Seguridad)
Estandarizan las URIs y reglas de acceso en ambos extremos de la aplicación:

```csharp
namespace Retail.Shared.Constants;

public static class ApiRoutes
{
    public const string Base = "api";

    public static class Auth
    {
        public const string Login = $"{Base}/auth/login";
        public const string KickOut = $"{Base}/auth/kick-out";
        public const string Heartbeat = $"{Base}/auth/heartbeat";
        public const string Logout = $"{Base}/auth/logout";
    }

    public static class Ventas
    {
        public const string Root = $"{Base}/ventas";
        public const string Devolucion = $"{Base}/ventas/devolucion";
    }

    public static class Fiscal
    {
        public const string Reintentos = $"{Base}/fiscal/reintentos";
    }
}
```

---

## 🚫 Reglas de Dependencia y Restricciones

- **CERO dependencias hacia otras capas:** `Retail.Shared` no debe hacer referencia a `Retail.Domain`, `Retail.Application`, `Retail.Infrastructure`, `Retail.Server.Api` ni `Retail.Client.Wpf`.
- **CERO dependencias pesadas de terceros:** Solo se permiten paquetes estándar de serialización (ej. `System.Text.Json`). Queda prohibida la inclusión de `EntityFrameworkCore`, paquetes de UI de WPF o drivers de base de datos.
- **Inmutabilidad recomendada:** Utilizar `record` o propiedades `init` cuando los DTOs representen mensajes de solo lectura para evitar mutaciones de estado colaterales.
