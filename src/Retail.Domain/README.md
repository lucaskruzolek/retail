# Retail.Domain (Capa de Dominio / Core de Negocio)

`Retail.Domain` es el **núcleo puro de la lógica de negocio** del sistema Retail. Modela las entidades, enumeraciones, reglas comerciales, invariantes y excepciones de dominio basadas en el **Diagrama Entidad-Relación (DER)** y la **Especificación de Requisitos de Software (ERS v3.2)**.

---

## 📁 Estructura del Directorio

```text
src/Retail.Domain/
├── Entities/                            # Entidades de Negocio (Modelos del DER)
│   ├── Usuario.cs                       # Cuentas de usuario con hash de contraseña y rol
│   ├── Rol.cs                           # Cajero, Encargado, Gerente
│   ├── Cliente.cs                       # CUIT/DNI, Condición IVA y Cuenta Corriente
│   ├── CobranzaCliente.cs               # Cobro de deudas (efectivo, transferencia, tarjeta)
│   ├── TurnoCaja.cs                     # Apertura, cierre y balance de arqueo de efectivo
│   ├── MovimientoCaja.cs                # Ingresos extraordinarios y retiros de caja
│   ├── Categoria.cs                     # Clasificación de artículos
│   ├── Marca.cs                         # Fabricantes / Editoriales
│   ├── Articulo.cs                      # Productos físicos y artesanías (código nulable)
│   ├── Proveedor.cs                     # Distribuidores y datos fiscales (CUIT)
│   ├── CatalogoProveedor.cs             # Listas de costos importadas de distribuidores
│   ├── Venta.cs                         # Cabecera de venta cobrada
│   ├── DetalleVenta.cs                  # Ítems vendidos con subtotal
│   ├── PagoVenta.cs                     # Imputación de pagos (efectivo, tarjeta, QR, cta cte)
│   ├── Presupuesto.cs                   # Cotización temporal independiente (15 días de validez)
│   ├── DetallePresupuesto.cs            # Ítems cotizados con precio unitario congelado
│   ├── ComprobanteFiscal.cs             # Datos fiscales ARCA (CAE, PV, Número, motivo_error)
│   ├── Compra.cs                        # Facturas y remitos de distribuidores
│   └── DetalleCompra.cs                 # Ítems adquiridos y actualización de costos
├── Enums/                               # Enumeraciones del Negocio
│   ├── RolUsuarioEnum.cs                # Cajero, Encargado, Gerente
│   ├── EstadoTurnoEnum.cs               # ABIERTO, CERRADO
│   ├── TipoMovimientoCajaEnum.cs        # INGRESO, EGRESO
│   ├── EstadoPresupuestoEnum.cs         # PENDIENTE, CONVERTIDO, VENCIDO, CANCELADO
│   ├── EstadoFiscalEnum.cs              # EMITIDO, ERROR_FISCAL_REINTENTABLE, NO_APLICA
│   ├── TipoComprobanteFiscalEnum.cs     # FACTURA_A, FACTURA_B, NO_APLICA
│   ├── CondicionIvaEnum.cs              # RESPONSABLE_INSCRIPTO, MONOTRIBUTO, CONSUMIDOR_FINAL, EXENTO
│   ├── TipoDocumentoEnum.cs             # DNI, CUIT, CUIL, PASAPORTE
│   └── MedioPagoEnum.cs                 # EFECTIVO, TARJETA_DEBITO, TARJETA_CREDITO, TRANSFERENCIA_QR, CUENTA_CORRIENTE
├── Exceptions/                          # Excepciones que representan violaciones de reglas
│   ├── DomainException.cs               # Clase base abstracta de errores de dominio
│   ├── StockInsuficienteException.cs    # Intento de venta de stock negativo
│   ├── CajaCerradaException.cs          # Operación de cobro sin turno abierto
│   ├── PresupuestoVencidoException.cs   # Intento de cobrar presupuesto caducado (> 15 días)
│   └── LimiteCreditoExcedidoException.cs# Compra a cuenta corriente superior al límite
└── Common/                              # Abstracciones y tipos base de dominio
    ├── BaseEntity.cs                    # id, created_at, deleted_at (Soft Delete)
    └── IAggregateRoot.cs                # Marcador de raíz de agregado (DDD)
```

---

## 🧩 Invariantes Clave del Dominio

### 1. `Articulo.cs` (Artesanías y Recálculo Automático por Compras)
- **Código de Barras Nulable (`RF-04`):** Los productos artesanales y servicios tienen `CodigoBarras = null`. La unicidad solo aplica a códigos no nulos.
- **Recálculo Automático en Compras (`RF-19`):**  
  El método de dominio `ActualizarCostoYRecalcularPrecio(decimal nuevoCosto)` actualiza `CostoReposicion` y automáticamente recalcula `PrecioVenta` preservando el `PorcentajeGanancia`:
  ```csharp
  public void ActualizarCostoYRecalcularPrecio(decimal nuevoCosto)
  {
      CostoReposicion = nuevoCosto;
      if (PorcentajeGanancia > 0)
      {
          PrecioVenta = Math.Round(nuevoCosto * (1m + (PorcentajeGanancia / 100m)), 2);
      }
  }
  ```

### 2. `CobranzaCliente.cs` y `Cliente.cs` (Cobro de Deudas Multimedio)
- Al registrarse una cobranza (`CobranzaCliente`), el método `Cliente.AcreditarPago(decimal monto)` reduce el `SaldoCuentaCorriente`.
- Si el medio de pago es `MedioPagoEnum.EFECTIVO`, la entidad se asocia a `TurnoCaja` para sumar al dinero físico; si es electrónico (`TRANSFERENCIA_QR`, `TARJETA_DEBITO`), se imputa al total de canales digitales del turno.

### 3. `Presupuesto.cs` (Vigencia y Conversión con Validación de Stock)
- Valida que `DateTime.UtcNow <= FechaVencimiento` (15 días).
- Al convertirse, pasa a `EstadoPresupuestoEnum.CONVERTIDO`.
