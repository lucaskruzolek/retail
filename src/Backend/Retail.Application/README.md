# Retail.Application (Capa de Aplicación y Casos de Uso)

`Retail.Application` orquesta los **casos de uso del sistema**, implementa los servicios de negocio de la aplicación, define las interfaces de persistencia e infraestructura (cumpliendo el **Principio de Inversión de Dependencias - DIP**) y valida exhaustivamente las entradas mediante **FluentValidation**.

---

## 🎯 Objetivos de la Capa

1. **Orquestar Casos de Uso y Flujos de Negocio:**
   Coordinar la interacción entre las entidades de `Retail.Domain`, las interfaces de persistencia (`IUnitOfWork`, `IRetailDbContext`) y los servicios externos (`IArcaClient`).
2. **Garantizar la Integridad Transaccional de Negocio:**
   Asegurar que operaciones complejas (como cobrar una venta multimedio y descontar stock atómicamente, o registrar una compra y actualizar costos de catálogo) se ejecuten bajo transacciones consistentes.
3. **Validar Entradas Desacopladamente con FluentValidation:**
   Interceptar y validar los DTOs recibidos desde la capa de presentación antes de que lleguen a la lógica del dominio, asegurando reglas de formato, rangos y obligatoriedad.
4. **Desacoplar la Infraestructura mediante Abstracciones:**
   Declarar contratos (`interfaces`) para la persistencia, el microservicio fiscal y los componentes de seguridad, permitiendo que la capa de infraestructura provea las implementaciones concretas sin acoplar la aplicación a detalles técnicos.

---

## 📁 Estructura del Directorio

```text
src/Backend/Retail.Application/
├── Interfaces/                          # Abstracciones e inversión de dependencias (DIP)
│   ├── Persistence/                     # Contratos de acceso a datos
│   │   ├── IRetailDbContext.cs          # DbSet abstraction para consultas
│   │   ├── IUnitOfWork.cs               # Manejo explícito de transacciones y SaveChanges
│   │   └── IRepository.cs               # Repositorio genérico / específico
│   ├── Services/                        # Contratos de servicios de aplicación
│   │   ├── IAuthService.cs
│   │   ├── IVentaService.cs
│   │   ├── IPresupuestoService.cs
│   │   ├── IInventarioService.cs
│   │   ├── ICajaService.cs
│   │   ├── ICompraService.cs
│   │   └── IFiscalService.cs
│   └── Infrastructure/                  # Abstracciones de componentes técnicos
│       ├── IArcaClient.cs               # Cliente hacia el microservicio arcasdk
│       ├── IPasswordHasher.cs           # PBKDF2 / BCrypt hashing
│       └── IJwtTokenGenerator.cs        # Generación y firma de tokens JWT
├── Services/                            # Implementación de Casos de Uso
│   ├── AuthService.cs                   # Login, Validación de Token, Kick-out y Heartbeat
│   ├── VentaService.cs                  # Descuento atómico de stock, Cobro multimedio, Devoluciones
│   ├── PresupuestoService.cs            # Creación sin reserva (15 días) y conversión a venta
│   ├── InventarioService.cs             # CRUD Artículos, cálculo de Markup % y stock mínimo
│   ├── CajaService.cs                   # Apertura, Arqueo Ciego, balance teórico vs declarado
│   ├── CompraService.cs                 # Ingreso de facturas, aumento de stock y costo de reposición
│   └── FiscalService.cs                 # Despacho asíncrono a la cola FIFO y reintentos en lote
└── Validators/                          # Reglas de validación con FluentValidation
    ├── CrearVentaValidator.cs           # Validación de ítems > 0, medios de pago y montos
    ├── AperturaTurnoValidator.cs        # Validación de saldo inicial >= 0
    ├── ArqueoCiegoValidator.cs          # Validación de valores declarados
    └── CrearArticuloValidator.cs        # Código de barras, descripción, margen y stock mínimo
```

---

## ⚙️ Funcionamiento de los Servicios de Aplicación

### 1. `AuthService.cs` (`RF-01`, `RF-02`, `RF-03`)
* **Login y Kick-Out:** Valida el usuario mediante `IPasswordHasher`. Consulta en `SESIONES_ACTIVAS`.
  * Si no hay sesión previa: Inserta el registro, genera el token JWT con `IJwtTokenGenerator` y retorna `LoginResponseDto`.
  * Si existe una sesión activa en otra terminal física: Retorna advertencia con `NombreTerminal` y `FechaInicio`. Al recibir `KickOutRequestDto`, realiza un *Upsert* en la tabla de sesiones, invalidando inmediatamente la terminal anterior.
* **Heartbeat:** Actualiza la marca temporal `FechaUltimoHeartbeat` cada 60 segundos. Si una terminal pierde conectividad durante > 3 minutos, la sesión se considera susceptible de desalojo.

### 2. `VentaService.cs` (`RF-09`, `RF-10`, `RF-13`, `RNF-01`)
* **Cobro en Mostrador y Descuento Atómico:**
  1. Valida que el cajero tenga un turno de caja en estado `ABIERTO` (`ICajaService`).
  2. Valida la suma de `PagosVenta` contra el `Total` de la venta.
  3. Inicia una transacción atómica mediante `IUnitOfWork`.
  4. Para cada ítem físico vendido, ejecuta `articulo.DescontarStock(cantidad)` validando disponibilidad.
  5. Inserta la venta en estado inicial `EMITIDO` o `PENDIENTE_FISCAL`.
  6. Confirma la transacción en la base de datos local (< 150 ms para liberar al cliente WPF).
  7. Despacha la solicitud de facturación a la cola secuencial FIFO (`IFiscalService`).

```csharp
public async Task<VentaResponseDto> RegistrarVentaAsync(CrearVentaDto dto, int idUsuario, CancellationToken ct)
{
    var turno = await _cajaRepository.GetTurnoActivoPorUsuarioAsync(idUsuario, ct)
        ?? throw new CajaCerradaException("No se puede registrar una venta sin un turno de caja abierto.");

    await using var transaction = await _unitOfWork.BeginTransactionAsync(ct);
    try
    {
        var venta = new Venta(turno.Id, idUsuario, dto.TipoOperacion);

        foreach (var item in dto.Detalles)
        {
            var articulo = await _articuloRepository.GetByIdAsync(item.IdArticulo, ct)
                ?? throw new DomainException($"El artículo con ID {item.IdArticulo} no existe.");

            articulo.DescontarStock(item.Cantidad);
            venta.AgregarDetalle(articulo.Id, item.Cantidad, item.PrecioUnitario);
        }

        foreach (var pago in dto.Pagos)
        {
            venta.AgregarPago(pago.MedioPago, pago.Monto, pago.Referencia);
        }

        await _ventaRepository.AddAsync(venta, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        // Despacho no bloqueante hacia la cola FIFO fiscal
        await _fiscalService.EncolarFacturacionAsync(venta.Id, ct);

        return _mapper.Map<VentaResponseDto>(venta);
    }
    catch
    {
        await transaction.RollbackAsync(ct);
        throw;
    }
}
```

### 3. `PresupuestoService.cs` (`RF-11`, `RF-12`)
* **Emisión de Presupuesto:** Registra una venta con `TipoOperacion = PRESUPUESTO` y fecha de validez de 15 días corridos. **No reserva ni descuenta stock físico**.
* **Conversión a Venta:** Al ser presentado en mostrador, el sistema recupera el presupuesto, valida que `DateTime.UtcNow <= FechaCreacion.AddDays(15)`, carga los ítems en el POS y ejecuta el flujo normal de `VentaService` descontando el stock al cobrar.

### 4. `CajaService.cs` (`RF-14`, `RF-15`, `RF-16`)
* **Arqueo Ciego:**
  1. El cajero envía `ArqueoCiegoDto` con el dinero en efectivo y cupones declarados.
  2. El servicio calcula el `SaldoFinalTeorico` a partir de:
     $$\text{SaldoInicial} + \sum \text{VentasEfectivo} + \sum \text{IngresosVarios} - \sum \text{EgresosVarios}$$
  3. Ejecuta `turno.CerrarTurno(saldoDeclarado, saldoTeorico)`.
  4. Persiste el cierre y retorna el acta con el desglose y la `Diferencia` (sobrante o faltante).

### 5. `CompraService.cs` (`RF-20`)
* **Ingreso de Mercadería:**
  1. Recibe la factura o remito del proveedor.
  2. En una transacción atómica, incrementa el `StockActual` de cada artículo físico y actualiza el `CostoReposicion` y `PrecioVenta` (aplicando el markup preconfigurado).
  3. Registra la cabecera y detalle de `COMPRAS`.

### 6. `FiscalService.cs` (`RF-17`, `RF-18`, `RF-19`)
* **Facturación Asíncrona y Contingencia:**
  - Envía la solicitud fiscal a la cola FIFO en memoria (`System.Threading.Channels`).
  - Si el enlace con `arcasdk` / ARCA se interrumpe, marca la venta con estado `ERROR_FISCAL_REINTENTABLE`.
  - Provee el método `ReintentarLoteFiscalAsync()` para que el Gerente reprocese las ventas en contingencia en estricto orden cronológico.

---

## 🛡️ Validadores con FluentValidation

Los validadores aseguran la calidad de los datos antes de entrar a la capa de dominio:

```csharp
public class CrearVentaValidator : AbstractValidator<CrearVentaDto>
{
    public CrearVentaValidator()
    {
        RuleFor(x => x.Detalles)
            .NotEmpty().WithMessage("La venta debe contener al menos un artículo.");

        RuleForEach(x => x.Detalles).ChildRules(detalle =>
        {
            detalle.RuleFor(d => d.IdArticulo).GreaterThan(0);
            detalle.RuleFor(d => d.Cantidad).GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");
            detalle.RuleFor(d => d.PrecioUnitario).GreaterThan(0).WithMessage("El precio debe ser positivo.");
        });

        RuleFor(x => x.Pagos)
            .NotEmpty().WithMessage("Debe registrar al menos una forma de pago.");

        RuleFor(x => x)
            .Must(x => x.Pagos.Sum(p => p.Monto) == x.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario))
            .WithMessage("La suma de los pagos debe coincidir exactamente con el total de la venta.");
    }
}
```

---

## 🚫 Reglas de Dependencia

- **Dependencias Permitidas:** `Retail.Application` solo depende de `Retail.Domain` y `Retail.Shared`.
- **Inversión de Control (DIP):** No debe tener referencias directas a `Microsoft.EntityFrameworkCore.SqlServer`, `System.Data.SqlClient`, ni clases de controladores o vistas WPF.
- **Pruebas Unitarias Aisladas:** Al estar completamente desacoplada de la infraestructura, todos los servicios de `Retail.Application` pueden probarse unitariamente de forma instantánea mediante mocks/stubs de `IRepository` y `IUnitOfWork`.
