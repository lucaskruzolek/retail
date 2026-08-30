# Retail.Domain (Capa de Dominio / Core de Negocio)

`Retail.Domain` es el **núcleo puro de la lógica de negocio** del sistema. No tiene dependencias de bases de datos, frameworks web, librerías de infraestructura ni interfaces de usuario. Contiene las entidades, reglas de negocio, invariantes y excepciones de dominio basadas en el **Diagrama Entidad-Relación (DER)** y la **Especificación de Requisitos de Software (ERS)**.

---

## 🎯 Objetivos de la Capa

1. **Encapsular el Modelo de Negocio y sus Invariantes:**
   Modelar fielmente las operaciones del comercio minorista (cálculo de markup, validación de stock, control de turnos de caja, reglas de presupuestos y estados fiscales) asegurando que ninguna entidad pueda quedar en un estado inválido.
2. **Independencia Absoluta de la Tecnología y Persistencia:**
   El dominio no sabe si los datos se guardan en SQL Server, PostgreSQL o memoria; se enfoca exclusivamente en la validez y consistencia del negocio.
3. **Manejo Expresivo de Errores Mediante Excepciones de Dominio:**
   Proveer excepciones fuertemente tipadas que comuniquen de forma precisa las violaciones a las reglas comerciales (ej. `StockInsuficienteException`, `CajaCerradaException`).

---

## 📁 Estructura del Directorio

```text
src/Backend/Retail.Domain/
├── Entities/                            # Entidades de Negocio (Modelos del DER)
│   ├── Usuario.cs                       # Cuentas locales con hash y rol
│   ├── Rol.cs                           # Cajero, Encargado, Gerente
│   ├── SesionActiva.cs                  # Control de sesión única por terminal física
│   ├── TurnoCaja.cs                     # Apertura, cierre y balance de arqueo ciego
│   ├── MovimientoCaja.cs                # Ingresos extraordinarios y retiros
│   ├── Categoria.cs                     # Clasificación de artículos
│   ├── Marca.cs                         # Fabricantes / Editoriales
│   ├── Articulo.cs                      # Productos físicos y servicios (stock, markup, precio)
│   ├── Proveedor.cs                     # Distribuidores y datos fiscales (CUIT)
│   ├── CatalogoProveedor.cs             # Listas de costos importadas de distribuidores
│   ├── Venta.cs                         # Cabecera de venta o presupuesto
│   ├── DetalleVenta.cs                  # Ítems vendidos con subtotal
│   ├── PagoVenta.cs                     # Imputación de pagos multimedio
│   ├── ComprobanteFiscal.cs             # Datos fiscales ARCA (CAE, PV, Número)
│   ├── Compra.cs                        # Facturas y remitos de distribuidores
│   └── DetalleCompra.cs                 # Ítems adquiridos y actualización de costos
├── Exceptions/                          # Excepciones que representan violaciones de reglas
│   ├── DomainException.cs               # Clase base de errores de dominio
│   ├── StockInsuficienteException.cs    # Intento de venta de stock negativo
│   ├── SesionActivaException.cs         # Intento de acceso concurrente
│   ├── CajaCerradaException.cs          # Operación de cobro sin turno abierto
│   └── PresupuestoVencidoException.cs   # Intento de cobrar presupuesto > 15 días
└── Common/                              # Abstracciones y tipos base de dominio
    ├── BaseEntity.cs                    # id, created_at, deleted_at (Soft Delete)
    └── IAggregateRoot.cs                # Marcador de raíz de agregado (DDD)
```

---

## 🧩 Modelo de Dominio y Reglas de Negocio (Invariantes)

### 1. `Articulo.cs` (Gestión de Inventario y Precios)
- **Fórmulas de Precios (`RF-04`, `RF-05`):**
  $$\text{PrecioVenta} = \text{CostoReposicion} \times \left(1 + \frac{\text{PorcentajeGanancia}}{100}\right)$$
- **Servicios vs. Artículos Físicos:** Si `EsServicio == true` (fotocopias, anillados), el control de stock mínimo y descuentos atómicos no aplica.
- **Stock Mínimo (`RF-08`):** Un artículo físico genera advertencia si $\text{StockActual} \le \text{StockMinimo}$.
- **Descuento de Stock (`RF-10`):** El método `DescontarStock(int cantidad)` valida que `StockActual >= cantidad`, lanzando `StockInsuficienteException` en caso contrario.

```csharp
public class Articulo : BaseEntity
{
    public string CodigoBarras { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;
    public int IdCategoria { get; private set; }
    public int IdMarca { get; private set; }
    public int? IdCatalogoProveedor { get; private set; }
    public decimal CostoReposicion { get; private set; }
    public decimal PorcentajeGanancia { get; private set; }
    public decimal PrecioVenta { get; private set; }
    public int StockActual { get; private set; }
    public int StockMinimo { get; private set; }
    public bool EsServicio { get; private set; }

    public void ActualizarPrecioDesdeCatalogo(decimal nuevoCosto)
    {
        CostoReposicion = nuevoCosto;
        PrecioVenta = Math.Round(CostoReposicion * (1 + (PorcentajeGanancia / 100m)), 2);
    }

    public void DescontarStock(int cantidad)
    {
        if (EsServicio) return;
        if (StockActual < cantidad)
            throw new StockInsuficienteException($"Stock insuficiente para '{Descripcion}'. Actual: {StockActual}, Solicitado: {cantidad}");
        StockActual -= cantidad;
    }

    public void IncrementarStock(int cantidad, decimal nuevoCosto)
    {
        if (cantidad <= 0) throw new DomainException("La cantidad a incrementar debe ser positiva.");
        StockActual += cantidad;
        CostoReposicion = nuevoCosto;
        PrecioVenta = Math.Round(CostoReposicion * (1 + (PorcentajeGanancia / 100m)), 2);
    }
}
```

### 2. `TurnoCaja.cs` (Gestión de Caja y Arqueo Ciego)
- **Apertura de Turno (`RF-14`):** Requiere un `SaldoInicial >= 0`, vinculando el usuario y la terminal física.
- **Movimientos Varios (`RF-15`):** Ingresos y retiros de efectivo actualizan el saldo teórico acumulado.
- **Arqueo Ciego y Cierre (`RF-16`):**
  - El cajero declara `SaldoFinalDeclarado` a ciegas.
  - La entidad calcula:
    $$\text{Diferencia} = \text{SaldoFinalDeclarado} - \text{SaldoFinalTeorico}$$
    - Si $\text{Diferencia} > 0 \implies \text{Sobrante}$
    - Si $\text{Diferencia} < 0 \implies \text{Faltante}$
  - La caja pasa a estado `CERRADO`.

### 3. `Venta.cs` (Ventas, Presupuestos y Cobros Multimedio)
- **Venta Directa vs. Presupuesto (`RF-09`, `RF-11`, `RF-12`):**
  - En `TipoOperacionVentaEnum.VENTA_DIRECTA`: Se descuenta el stock de inmediato y se procesa el cobro.
  - En `TipoOperacionVentaEnum.PRESUPUESTO`: Se valida una vigencia máxima de **15 días corridos**. No descuenta stock al crearse.
- **Cobros Multimedio:** La suma de `PagosVenta.Sum(p => p.Monto)` debe ser exactamente igual al `Total` de la venta.
- **Estado Fiscal (`RF-17`, `RF-18`):**
  - Inicia en `EMITIDO` tras obtener CAE de ARCA.
  - Si falla el enlace fiscal externo, queda en `ERROR_FISCAL_REINTENTABLE` con ticket no fiscal emitido.

### 4. `SesionActiva.cs` (Control de Terminal Única y Heartbeat)
- Garantiza que `IdUsuario` sea clave primaria única.
- Almacena el `NombreTerminal`, `TokenHash` y la `FechaUltimoHeartbeat`.
- Si el usuario inicia sesión en una segunda terminal física, el dominio permite ejecutar la sustitución (*Upsert*) invalidando el token anterior (`RF-02`).

---

## 🚫 Reglas de Dependencia y Buenas Prácticas

- **Capa Base:** `Retail.Domain` solo puede referenciar a `Retail.Shared` (para Enums o tipos de valor compartidos). No puede referenciar ninguna otra capa del sistema.
- **No ORM en Dominio:** No se colocan atributos de Entity Framework Core (como `[Table]`, `[Key]`, `[ForeignKey]`) en las clases de dominio. La configuración se realiza exclusivamente mediante **Fluent API** en `Retail.Infrastructure`.
- **Encapsulamiento y Setters Privados:** Las propiedades deben tener `private set` o `protected set`. La modificación del estado de una entidad debe realizarse siempre a través de **métodos de negocio explícitos** que validen invariantes.
- **Soft Delete:** Todas las entidades que heredan de `BaseEntity` cuentan con `DeletedAt` (anulable) para admitir bajas lógicas sin destrucción física de datos históricos.
