# Retail.Application (Capa de Aplicación / Casos de Uso)

`Retail.Application` orquesta los **casos de uso de negocio** del sistema Retail. Define los contratos de servicios, DTOs de entrada/salida, validadores mediante `FluentValidation` y las interfaces de persistencia e infraestructura.

---

## 📁 Estructura del Directorio

```text
src/Retail.Application/
├── Interfaces/
│   ├── Persistence/
│   │   ├── IRetailDbContext.cs          # Abstracción de DbSets y SaveChangesAsync
│   │   ├── IUnitOfWork.cs               # Control de transacciones atómicas explícitas
│   │   └── IRepository.cs               # Operaciones CRUD tipadas
│   ├── Services/
│   │   ├── IAuthService.cs              # Autenticación local y verificación de contraseñas
│   │   ├── IVentaService.cs             # Cobro de ventas y despacho fiscal
│   │   ├── IPresupuestoService.cs        # Emisión y conversión de presupuestos (con validación de stock y precios)
│   │   ├── IClienteService.cs            # ABM, control de cuentas corrientes y RegistrarCobranzaAsync
│   │   ├── IInventarioService.cs        # ABM de artículos, márgenes de ganancia y stock mínimo
│   │   ├── ICajaService.cs              # Apertura, movimientos y arqueo ciego de efectivo
│   │   ├── ICompraService.cs            # Ingreso de facturas y recálculo automático de precio
│   │   └── IFiscalService.cs            # Emisión de comprobantes ARCA y consola de reintentos
│   └── Infrastructure/
│       ├── IArcaClient.cs               # Comunicación con microservicio local arcasdk
│       ├── IExcelCatalogParser.cs       # Lectura de planillas de distribuidores (MiniExcel)
│       └── IPasswordHasher.cs           # Criptografía de contraseñas (BCrypt)
├── DTOs/                                # Contratos de entrada y salida de datos
│   ├── Ventas/                          # CrearVentaDto, DetalleVentaDto, PagoVentaDto, VentaResponseDto
│   ├── Presupuestos/                    # CrearPresupuestoDto, PresupuestoDto, DetallePresupuestoDto
│   ├── Clientes/                        # ClienteDto, CrearClienteDto, RegistrarCobranzaDto, CobranzaResponseDto
│   ├── Articulos/                       # ArticuloDto, CrearArticuloDto, ActualizarArticuloDto
│   ├── Proveedores/                     # ProveedorDto, MapeoColumnasCatalogoDto
│   ├── Compras/                         # CrearCompraDto, DetalleCompraDto
│   ├── Caja/                            # AperturaTurnoDto, MovimientoCajaDto, ArqueoCiegoEfectivoDto, CierreTurnoDto
│   └── Fiscal/                          # ComprobanteFiscalDto, ReintentoFiscalDto
├── Services/                            # Implementación concreta de casos de uso
│   ├── AuthService.cs
│   ├── VentaService.cs
│   ├── PresupuestoService.cs
│   ├── ClienteService.cs
│   ├── InventarioService.cs
│   ├── CajaService.cs
│   ├── CompraService.cs
│   └── FiscalService.cs
└── Validators/                          # Reglas FluentValidation
    ├── CrearVentaValidator.cs
    ├── CrearPresupuestoValidator.cs
    ├── CrearClienteValidator.cs
    ├── RegistrarCobranzaValidator.cs
    ├── AperturaTurnoValidator.cs
    └── ArqueoCiegoEfectivoValidator.cs
```

---

## ⚙️ Casos de Uso Destacados

### 1. `ClienteService.cs` (Circuito de Cobranzas Multimedio `RF-20`)
- Método `RegistrarCobranzaAsync(RegistrarCobranzaDto dto)`:
  - Valida que el monto a cancelar no supere el saldo deudor actual del cliente.
  - Inserta la entidad `CobranzaCliente`.
  - Descuenta el saldo deudor: `cliente.AcreditarPago(dto.Monto)`.
  - Si `dto.MedioPago == MedioPagoEnum.EFECTIVO`, suma a `turnoCaja.TotalIngresosEfectivo`.
  - Si es electrónico, suma a `turnoCaja.TotalVentasElectronicas`.
  - Confirma la transacción y retorna el DTO para impresión del recibo de cobranza.

### 2. `CompraService.cs` (Recálculo Automático de Precios `RF-19`)
- Al registrar la compra de mercadería, para cada ítem en `DETALLE_COMPRAS`, compara el costo unitario de compra con `articulo.CostoReposicion`.
- Si el costo aumentó o cambió, invoca `articulo.ActualizarCostoYRecalcularPrecio(item.CostoUnitario)`, actualizando de forma automática e inmediata el `PrecioVenta` según su *Markup %*.

### 3. `PresupuestoService.cs` (Conversión con Validación de Stock `RF-12`)
- Al solicitar la conversión de un presupuesto, audita que cada artículo cuente con unidades suficientes en góndola (`StockActual >= CantidadPresupuestada`).
- Si algún artículo no tiene stock, lanza `StockInsuficienteException` detallando las unidades faltantes para que el operador decida si ajustar la cantidad o esperar reposición.
