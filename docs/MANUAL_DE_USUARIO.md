# Retail POS — Manual de Usuario y Guía de Operación

### Sistema ERP & Punto de Venta para Librería y Mostrador Comercial
**Plataforma:** .NET 8 LTS | Windows 11 Fluent Design  
**Versión del Sistema:** v1.0.0  
**Audiencia:** Cajeros, Repositores, Encargados, Gerentes y Administradores de Sistema.

---

## 📑 Tabla de Contenidos

1. [Capítulo 1: Introducción, Ergonomía de Mostrador y Atajos Globales](#capítulo-1-introducción-ergonomía-de-mostrador-y-atajos-globales)
2. [Capítulo 2: Ventana de Autenticación (Login)](#capítulo-2-ventana-de-autenticación-login)
3. [Capítulo 3: Ventana Principal (Shell, Sidebar y Navegación)](#capítulo-3-ventana-principal-shell-sidebar-y-navegación)
4. [Capítulo 4: Punto de Venta de Alta Velocidad (POS)](#capítulo-4-punto-de-venta-de-alta-velocidad-pos)
5. [Capítulo 5: Diálogo de Cobro Multimedio y Facturación Fiscal](#capítulo-5-diálogo-de-cobro-multimedio-y-facturación-fiscal)
6. [Capítulo 6: Selector y Búsqueda Rápida de Clientes](#capítulo-6-selector-y-búsqueda-rápida-de-clientes)
7. [Capítulo 7: Padrón y Gestión de Clientes](#capítulo-7-padrón-y-gestión-de-clientes)
8. [Capítulo 8: Formulario de Alta y Edición de Clientes](#capítulo-8-formulario-de-alta-y-edición-de-clientes)
9. [Capítulo 9: Cobranza de Cuenta Corriente](#capítulo-9-cobranza-de-cuenta-corriente)
10. [Capítulo 10: Control de Caja y Gestión de Turnos](#capítulo-10-control-de-caja-y-gestión-de-turnos)
11. [Capítulo 11: Apertura y Movimientos Manuales de Caja](#capítulo-11-apertura-y-movimientos-manuales-de-caja)
12. [Capítulo 12: Arqueo Ciego y Cierre de Turno](#capítulo-12-arqueo-ciego-y-cierre-de-turno)
13. [Capítulo 13: Catálogo Maestro de Artículos e Inventario](#capítulo-13-catálogo-maestro-de-artículos-e-inventario)
14. [Capítulo 14: Ficha Integral de Artículo (Alta y Modificación)](#capítulo-14-ficha-integral-de-artículo-alta-y-modificación)
15. [Capítulo 15: Presentaciones Derivadas y Fraccionamiento](#capítulo-15-presentaciones-derivadas-y-fraccionamiento)
16. [Capítulo 16: Clasificación y Taxonomía (Rubros, Categorías y Marcas)](#capítulo-16-clasificación-y-taxonomía-rubros-categorías-y-marcas)
17. [Capítulo 17: Padrón y Formulario de Proveedores Mayoristas](#capítulo-17-padrón-y-formulario-de-proveedores-mayoristas)
18. [Capítulo 18: Equivalencias de Proveedor](#capítulo-18-equivalencias-de-proveedor)
19. [Capítulo 19: Importador de Catálogos y Planillas Excel/CSV](#capítulo-19-importador-de-catálogos-y-planillas-excelcsv)
20. [Capítulo 20: Asistente de Incorporación Asistida al Catálogo](#capítulo-20-asistente-de-incorporación-asistida-al-catálogo)
21. [Capítulo 21: Pedidos de Abastecimiento a Proveedores](#capítulo-21-pedidos-de-abastecimiento-a-proveedores)
22. [Capítulo 22: Armado y Emisión de Pedidos de Compra](#capítulo-22-armado-y-emisión-de-pedidos-de-compra)
23. [Capítulo 23: Ingreso y Recepción de Compras (Facturas y Remitos)](#capítulo-23-ingreso-y-recepción-de-compras-facturas-y-remitos)
24. [Capítulo 24: Gestión de Usuarios y Roles de Seguridad](#capítulo-24-gestión-de-usuarios-y-roles-de-seguridad)
25. [Capítulo 25: Formulario de Usuario y Blanqueo de Contraseña](#capítulo-25-formulario-de-usuario-y-blanqueo-de-contraseña)
26. [Capítulo 26: Copias de Seguridad y Respaldo de Base de Datos](#capítulo-26-copias-de-seguridad-y-respaldo-de-base-de-datos)
27. [Capítulo 27: Restauración Segura y Confirmación con Credencial](#capítulo-27-restauración-segura-y-confirmación-con-credencial)
28. [Capítulo 28: Tablero Gerencial e Indicadores de Rendimiento](#capítulo-28-tablero-gerencial-e-indicadores-de-rendimiento)
29. [Capítulo 29: Reportes Analíticos y Exportación a Excel/PDF](#capítulo-29-reportes-analíticos-y-exportación-a-excelpdf)
30. [Capítulo 30: Ajustes del Sistema y Preferencias de Tema](#capítulo-30-ajustes-del-sistema-y-preferencias-de-tema)
31. [Capítulo 31: Centro de Ayuda y Consulta Rápida](#capítulo-31-centro-de-ayuda-y-consulta-rápida)

---

## Capítulo 1: Introducción, Ergonomía de Mostrador y Atajos Globales

Retail POS ha sido diseñado priorizando la **velocidad de atención comercial**. En mostrador, retirar las manos del teclado para usar el ratón retrasa el despacho de clientes y genera fatiga operativa. Por ello, el 100% de las operaciones críticas cuenta con soporte directo de teclas de función (`F1` a `F12`) y atajos universales.

### Tabla General de Atajos de Teclado del Sistema

| Tecla / Combinación | Acción Global | Módulo Asociado |
| :--- | :--- | :--- |
| **`[F1]`** | Abrir Punto de Venta (Terminal POS) | Ventas y Mostrador |
| **`[F2]`** | Abrir Catálogo de Artículos y Stock | Inventario |
| **`[F4]`** | Buscar y Seleccionar Cliente / Abrir Padrón | Clientes |
| **`[F5]`** | Consultar Caja y Estado de Turno | Caja |
| **`[F6]`** | Presupuestos y Cotizaciones | Ventas |
| **`[F7]`** | Abrir Tablero Gerencial (Gerente) | Reportes |
| **`[F10]`** | Administrar Usuarios y Operadores | Seguridad |
| **`[F11]`** | Copias de Seguridad y Respaldos | Base de Datos |
| **`[F12]`** | Cobrar Venta (en POS) / Registrar Compra (en Compras) | Transaccional |
| **`[Ctrl + B]`** | Expandir / Contraer Menú Lateral (Sidebar) | Shell General |
| **`[Ctrl + H]`** | Abrir Centro de Ayuda y Atajos Rápidos | Soporte |
| **`[Enter]`** | Confirmar acción primaria en diálogos (`IsDefault`) | Formularios y Modales |
| **`[Esc]`** | Cerrar diálogo actual / Cancelar búsqueda activa | Universal |

### Reglas de Entrada de Datos Numéricos y Decimales
* **Cantidades Enteras (Stock, Unidades):** El sistema bloquea el ingreso de caracteres no numéricos y espacios. Solo admite dígitos (`0` al `9`).
* **Importes y Porcentajes Moneda:** Se admite tanto el punto (`.`) como la coma (`,`); el sistema normaliza automáticamente el separador decimal según la configuración regional argentina (`es-AR`).
* **Números Tabulares:** Todos los montos se alinean visualmente a la derecha para que los centavos coincidan en la misma vertical.

---

## Capítulo 2: Ventana de Autenticación (Login)

La ventana de inicio de sesión (`LoginWindow`) es la puerta de entrada al sistema y garantiza el control de acceso basado en roles (RBAC).

```
┌────────────────────────────────────────────────────────┐
│  Retail POS - Inicio de Sesión                         │
├────────────────────────────────────────────────────────┤
│                                                        │
│  Usuario o Correo Electrónico:                         │
│  [ admin@libreria.com.ar                             ] │
│                                                        │
│  Contraseña:                                           │
│  [ ••••••••••••                                      ] │
│                                                        │
│  [ Iniciar Sesión ]  (Enter)                           │
│                                                        │
└────────────────────────────────────────────────────────┘
```

### Campos y Carga de Datos
1. **Usuario o Correo Electrónico (`*` Obligatorio):**
   * Puede ingresarse el nombre de usuario (ej. `juan.perez`) o la casilla de correo (ej. `juan@libreria.com`).
   * No distingue mayúsculas de minúsculas.
2. **Contraseña (`*` Obligatorio):**
   * Caracteres enmascarados de seguridad.
   * Si la cuenta fue recién creada o blanqueada, el sistema exigirá el cambio de credencial en el primer acceso.

### Operatoria y Validación
* Presione **`[Enter]`** para enviar el formulario o haga clic en **Iniciar Sesión**.
* Si las credenciales son incorrectas o la cuenta está inhabilitada, se mostrará un mensaje descriptivo en color carmín.
* Si el operador supera la validación, la ventana de login se cierra y el Shell principal adapta sus menús según el rol asignado (Cajero, Encargado, Gerente o Administrador).

---

## Capítulo 3: Ventana Principal (Shell, Sidebar y Navegación)

El Shell (`MainWindow`) gobierna el entorno de trabajo continuo de mostrador.

### Zonas de la Interfaz
1. **Sidebar Lateral Izquierdo:**
   * **Botón Hamburguesa (`[Ctrl + B]`):** Alterna el ancho del menú entre modo compacto (58 px, solo íconos con tooltips flotantes) y modo expandido (200 px, ícono + texto descriptivo).
   * **Botonera de Módulos:** Accesos directos a Tablero, Punto de Venta, Clientes, Caja, Inventario, Compras y Administración. Los módulos no permitidos para el rol activo permanecen ocultos automáticamente.
   * **Pie Fijo del Menú:**
     * **Botón Ayuda (`[Ctrl + H]`):** Despliega el Centro de Ayuda modal.
     * **Botón Ajustes:** Conduce a la personalización de tema claro/oscuro.
     * **Ficha del Operador:** Muestra el nombre y rol del usuario activo.
     * **Botón "Cambiar Operador":** Permite cambiar de turno o usuario en menos de 15 ms sin cerrar el programa.
2. **Barra de Título Superior:**
   * Integra controles estándar de ventana (Minimizar, Maximizar con Snap Layouts de Windows 11 y Cerrar).
   * Muestra avisos destacados no bloqueantes (por ejemplo, advertencia de restauración reciente de base de datos para Gerentes).
3. **Área Central de Contenido (`RootFrame`):**
   * Contenedor dinámico donde se presentan las páginas operativas sin recargas destructivas de memoria.

---

## Capítulo 4: Punto de Venta de Alta Velocidad (POS)

El Punto de Venta (`PosView`, tecla rápida **`[F1]`**) es el centro neurálgico del mostrador comercial. Permite escanear artículos, modificar cantidades y cobrar en segundos.

```
┌───────────────────────────────────────────────────┬────────────────────────┐
│ Buscador: [ 7791234567890                        ]│ TOTAL A COBRAR         │
├───────────────────────────────────────────────────┤                        │
│ Cant │ Código        │ Descripción  │ Unit  │ Sub │ $ 15.420,00            │
├──────┼───────────────┼──────────────┼───────┼─────┤                        │
│  2   │ 7791234567890 │ Cuaderno A4  │ 4.500 │9.000│ Cliente: Consumidor    │
│  1   │ 9789501234567 │ Libro Novela │ 6.420 │6.420│                        │
│      │               │              │       │     │ [ Cobrar Venta - F12 ] │
│      │               │              │       │     │ [ Cancelar Ticket - F8]│
└───────────────────────────────────────────────────┴────────────────────────┘
```

### Proceso de Carga y Escaneo
1. **Búsqueda por Lector de Código de Barras (Modo Escáner):**
   * El cursor se ubica por defecto en el cuadro de búsqueda al ingresar a la pantalla.
   * Al disparar el lector sobre el código de barras del producto, el escáner emite la secuencia numérica seguida de un retorno de carro (`Enter`).
   * El artículo se incorpora inmediatamente a la grilla con cantidad `1`. Si ya existía en el ticket, se incrementa su cantidad automáticamente.
2. **Búsqueda Manual por Texto (Predictivo):**
   * Escriba parte del título, autor o código (ej. `"cuaderno rivadavia"` o `"lapicera"`).
   * Se abrirá un menú desplegable con coincidencias en tiempo real mostrando título, precio y stock disponible.
   * Con las flechas de dirección **`[↓]`** / **`[↑]`** seleccione el ítem y presione **`[Enter]`** para incorporarlo.
3. **Modificación Rápida de Cantidad:**
   * **Botones `-` y `+`:** Modifican de a 1 unidad directamente sobre la fila. El foco regresa de inmediato al buscador para seguir escaneando.
   * **Edición Directa:** Haga clic o tabule a la columna "Cant", escriba la cantidad deseada (ej. `10`) y presione **`[Enter]`**.
4. **Eliminación de un Ítem:**
   * Haga clic sobre el botón de tacho rojo (`IconDangerButtonStyle`) en el extremo derecho de la fila del ticket.
5. **Acciones de Mostrador:**
   * **`[F4]`:** Abre el diálogo para asignar un cliente registrado en lugar del Consumidor Final predeterminado.
   * **`[F8]`:** Cancela el ticket completo previa confirmación rápida, limpiando la grilla.
   * **`[F12]`:** Dispara el diálogo de cobro final.

---

## Capítulo 5: Diálogo de Cobro Multimedio y Facturación Fiscal

El diálogo de cobro (`CobroModalDialog`, tecla **`[F12]`** desde el POS) permite registrar los medios de pago del cliente, calcular el vuelto con exactitud y emitir el comprobante correspondiente.

### Carga de Pagos Multimedio
El sistema admite combinar diferentes formas de pago en un mismo comprobante:

1. **Efectivo:**
   * Ingrese el monto en el campo Efectivo o utilice los **Botones de Billetes Rápidos** (`+$1.000`, `+$2.000`, `+$5.000`, `+$10.000`, `+$20.000`).
   * El sistema calcula de inmediato el **Vuelto** en números grandes y destacados.
2. **Tarjeta de Débito / Crédito:**
   * Ingrese el importe cobrado en la terminal POS física (posnet/lapos).
   * Opcionalmente, ingrese los últimos 4 dígitos o número de cupón de autorización.
3. **Transferencia / QR (MercadoPago / Modo):**
   * Ingrese el importe transferido una vez corroborada la acreditación en la terminal o teléfono de caja.
4. **Cuenta Corriente:**
   * Habilitado únicamente si se asignó un cliente con cuenta corriente activa (`[F4]`).
   * Si el monto imputado supera el límite de crédito disponible del cliente, el sistema mostrará una alerta en rojo impidiendo la confirmación hasta ajustar el monto.

### Selección de Comprobante
* **Ticket Factura B / C:** Seleccionado por defecto para Consumidores Finales y Monotributistas.
* **Factura A:** Disponible automáticamente cuando el cliente asignado es Responsable Inscripto con CUIT válido.
* Presione **`[Enter]`** o haga clic en **"Confirmar Cobro"**:
  * Se registra la venta de forma atómica en base de datos.
  * Se descuenta el stock de los artículos vendidos.
  * Se envía la solicitud fiscal a ARCA/AFIP (o simulador de CAE en modo local).
  * Se envía la orden de impresión del ticket fiscal al servicio térmico.
  * El POS se reinicia limpio para la siguiente venta.

---

## Capítulo 6: Selector y Búsqueda Rápida de Clientes

Invocado con **`[F4]`** desde el Punto de Venta o el formulario de presupuestos (`SeleccionarClienteModalDialog`).

### Procedimiento de Uso
1. Ingrese en el cuadro de búsqueda el DNI, CUIT, Razón Social o Teléfono del cliente.
2. La lista filtra los resultados en tiempo real mostrando:
   * Nombre o Razón Social.
   * Documento e identificación tributaria.
   * Estado de cuenta corriente y saldo deudor si correspondiera.
3. Seleccione el cliente con las flechas del teclado y presione **`[Enter]`** (o doble clic).
4. El cliente queda vinculado al ticket actual y se actualiza el panel derecho del POS. Para volver al consumidor final genérico, presione **`[Esc]`**.

---

## Capítulo 7: Padrón y Gestión de Clientes

Accesible desde el menú lateral en **Ventas $\rightarrow$ Clientes** (tecla **`[F4]`** desde el Shell).

### Componentes de la Pantalla
* **Barra Superior de Filtros (`ListToolbar`):**
  * Cuadro de búsqueda: filtra por nombre, apellido, DNI, CUIT, email o localidad.
  * Botón de alternancia **"Solo con Deuda"**: filtra instantáneamente aquellos clientes que presentan saldo pendiente en cuenta corriente.
  * Botón **"Limpiar filtros"**: restablece la vista al padrón completo.
* **Grilla Principal de Clientes:**
  * Columnas: Documento, Razón Social / Nombre, Condición IVA, Teléfono, Saldo Cta. Cte., Estado.
  * Badges de estado: `Activo` (verde), `Inactivo` (gris), `Con Saldo Deudor` (amarillo de advertencia).
* **Botonera de Acciones:**
  * **"Nuevo Cliente":** Abre el formulario de alta.
  * **"Cobranza Rápida":** Abre directamente el módulo de cobro para el cliente seleccionado.
* **Barra de Paginación (`PaginationBar`):** Permite navegar entre páginas de resultados y ajustar la cantidad de filas visibles (15, 25, 50).

---

## Capítulo 8: Formulario de Alta y Edición de Clientes

Modal `ClienteFormDialog` utilizado para registrar un nuevo cliente o actualizar sus datos de contacto y comerciales.

### Campos y Reglas de Carga

| Campo | Obligatorio | Regla de Validación y Formato |
| :--- | :---: | :--- |
| **Tipo de Documento** | Sí | Selección en desplegable: `DNI`, `CUIT`, `CUIL`, `Pasaporte`. |
| **Número de Documento** | Sí | Solo números sin puntos ni guiones (DNI: 7-8 dígitos; CUIT/CUIL: 11 dígitos con validación de dígito verificador). |
| **Razón Social / Nombre Completo** | Sí | Mínimo 3 caracteres, máximo 150 caracteres. En mayúsculas y minúsculas legibles. |
| **Condición frente al IVA** | Sí | Desplegable: `Consumidor Final`, `Responsable Inscripto`, `Monotributo`, `Exento`. |
| **Correo Electrónico** | No | Formato de email estándar (`usuario@dominio.com`). |
| **Teléfono / Celular** | No | Solo dígitos, espacios y prefijos telefónicos. |
| **Domicilio Fiscal / Comercial** | No | Calle, número, piso/depto y localidad. |
| **Habilitar Cuenta Corriente** | No | Interruptor `ToggleSwitch` (Sí / No). |
| **Límite de Crédito ($)** | Condicional | Obligatorio si Cta. Cte. está habilitada. Monto numérico mayor a cero. |
| **Días de Gracia / Vencimiento** | Condicional | Días pactados para la cancelación del saldo (ej. 30 días). |

Presione **`[Enter]`** para guardar o **`[Esc]`** para cancelar sin guardar cambios.

---

## Capítulo 9: Cobranza de Cuenta Corriente

Modal `CobranzaModalDialog` para asentar pagos totales o parciales sobre el saldo deudor de un cliente.

### Operatoria
1. La cabecera muestra el **Saldo Actual Deudor** del cliente en tipografía tabular destacada.
2. **Monto a Imputar:**
   * Escriba el monto específico que el cliente abona.
   * O haga clic en **"Pagar Totalidad"** para completar automáticamente el total del saldo pendiente.
3. **Medio de Cobro:** Seleccione Efectivo, Débito, Transferencia bancaria o Cheque.
4. Si paga en efectivo con un billete de mayor valor, el visor calcula el vuelto en tiempo real.
5. Presione **"Confirmar Cobranza"**: se genera el recibo de cobranza, se descuenta la deuda en la ficha del cliente y se imputa el ingreso en la caja activa del cajero.

---

## Capítulo 10: Control de Caja y Gestión de Turnos

Accesible desde **Ventas $\rightarrow$ Caja y turnos** (tecla **`[F5]`**). Gobierna la responsabilidad patrimonial del dinero en mostrador.

### Estados de la Caja
* **Caja Cerrada:** No se pueden facturar ventas en el POS ni registrar cobranzas hasta realizar la apertura.
* **Caja Abierta:** Muestra el turno vigente con:
  * Operador responsable y fecha/hora de apertura.
  * Saldo inicial de cambio.
  * Total recaudado por sistema desglosado por: Efectivo, Tarjetas de Débito, Tarjetas de Crédito y Pagos QR/Transferencias.
  * Total de egresos/ingresos manuales.

---

## Capítulo 11: Apertura y Movimientos Manuales de Caja

### 1. Apertura de Turno (`AperturaTurnoDialog`)
Al iniciar la jornada de trabajo o el turno de mostrador:
* **Fondo Inicial de Caja ($) (`*` Obligatorio):** Ingrese el dinero en efectivo disponible en el cajón para dar cambio (ej. `$25.000,00`). Si la caja abre sin cambio, ingrese `0`.
* Presione **"Abrir Caja"**: el estado cambia a Activo y habilita la facturación en el POS.

### 2. Ingresos y Egresos Manuales (`MovimientoCajaDialog`)
Para movimientos de efectivo no derivados de ventas:
* **Tipo de Movimiento:** Seleccione `Ingreso de Dinero` (ej. reposición de cambio) o `Retiro / Egreso de Dinero` (ej. pago a cadetería, gastos de limpieza, retiro de seguridad).
* **Importe ($) (`*` Obligatorio):** Monto exacto que entra o sale del cajón.
* **Motivo / Concepto (`*` Obligatorio):** Detalle explicativo de la operación (mínimo 5 caracteres).
* Presione **"Registrar Movimiento"**.

---

## Capítulo 12: Arqueo Ciego y Cierre de Turno

Retail POS implementa el protocolo de **Arqueo Ciego** (`ArqueoCiegoDialog`): para prevenir irregularidades, el cajero debe contar físicamente el dinero y declararlo **sin que la pantalla le revele cuánto efectivo espera el sistema**.

```
┌────────────────────────────────────────────────────────┐
│ Arqueo y Cierre de Turno de Caja                      │
├────────────────────────────────────────────────────────┤
│                                                        │
│  Efectivo Recontado en Cajón:                          │
│  [ $ 84.500,00                                       ] │
│                                                        │
│  Observaciones del Cierre (Opcional):                  │
│  [ Se retiraron $50.000 a sobre de tesorería         ] │
│                                                        │
│  [ Cerrar Turno Definitivamente ]                      │
│                                                        │
└────────────────────────────────────────────────────────┘
```

### Pasos para el Cierre
1. El cajero realiza el recuento físico de todos los billetes y monedas del cajón.
2. Ingresa el total en el campo **Efectivo Recontado**.
3. Opcionalmente agrega notas u observaciones sobre anomalías del turno.
4. Presione **"Cerrar Turno Definitivamente"**.
5. El sistema consolida la información, calcula las diferencias (Sobrante / Faltante / Exacto) y emite el reporte de cierre para auditoría gerencial.

---

## Capítulo 13: Catálogo Maestro de Artículos e Inventario

Accesible desde **Inventario $\rightarrow$ Artículos** (tecla **`[F2]`**).

### Visualización y Controles
* **Grilla de Artículos:**
  * Muestra Código, Descripción, Rubro, Categoría, Marca, Stock Actual, Stock Mínimo, Costo Principal, Markup % y Precio de Venta.
  * Badges de Alerta:
    * `Verde`: Stock normal.
    * `Amarillo (Warning)`: Stock actual menor o igual al stock mínimo de reposición.
    * `Rojo (Danger)`: Stock en cero o negativo.
* **Buscador Universal:** Búsqueda en servidor traducible a SQL por código de barras, descripción o marca.
* **Filtros por Taxonomía:** Desplegables de Rubro (acota las categorías hijas) y Marca.
* **Acciones:**
  * **Nuevo Artículo:** Abre la ficha en blanco.
  * **Doble clic sobre una fila:** Abre la ficha de edición del artículo.

---

## Capítulo 14: Ficha Integral de Artículo (Alta y Modificación)

Modal `ArticuloFormDialog`: estructurado en dos columnas de alta precisión ergonómica.

### Columna Izquierda: Identificación y Stock
1. **Código de Barras:** Código EAN-13, EAN-8 o código alfanumérico interno. Puede dispararse con la pistola lectora directamente sobre el campo.
2. **Descripción (`*` Obligatorio):** Nombre comercial claro (ej. *"Cuaderno Rivadavia Tapa Dura 100 Hojas Rayado"*).
3. **Tipo de Ítem:** Selector segmentado `Producto` (maneja inventario físico) o `Servicio` (fotocopias, anillados, impresiones; no descuenta stock).
4. **Clasificación:** Selectores de Rubro, Categoría y Marca.
5. **Stock Actual:** En alta se ingresa la existencia inicial; en edición se realiza mediante el botón asistido de ajuste de stock con motivo.
6. **Stock Mínimo:** Cantidad umbral para disparar alertas automáticas de compra.

### Columna Derecha: Fuentes de Proveedor y Formación de Precios
1. **Fuentes de Compra:** Permite vincular los códigos y costos de diferentes distribuidores mayoristas para un mismo artículo. Se selecciona una como **Fuente Principal**.
2. **Costo de Reposición ($) (`*` Obligatorio):** Tomado de la fuente principal o cargado manualmente.
3. **Margen de Ganancia (Markup %):** Porcentaje de beneficio deseado sobre el costo (ej. `40%`).
4. **Alícuota IVA:** Desplegable (`21%`, `10.5%`, `0%`).
5. **Precio de Venta Final ($):** Calculado automáticamente en tiempo real mediante la fórmula:
   $$\text{Precio Final} = \text{Costo} \times \left(1 + \frac{\text{Markup}}{100}\right) \times (1 + \text{IVA})$$
   El recuadro muestra el importe en números grandes y claros.

Presione **`[Enter]`** para guardar el artículo.

---

## Capítulo 15: Presentaciones Derivadas y Fraccionamiento

### 1. Presentaciones Derivadas (`PresentacionFormDialog`)
Permite comercializar un producto en diferentes formatos a partir de un artículo padre (ej. *Paquete de 10 Resmas* o *Caja de 50 Lapiceras*):
* Se indica el factor de conversión (cantidad de unidades contenidas).
* El costo se recalcula dinámicamente en base al producto origen.

### 2. Fraccionar Stock (`FraccionarDialog`)
Permite desarmar una presentación mayor para abastecer la venta individual:
* Ingrese la cantidad de bultos a abrir.
* La pantalla previsualiza el stock resultante: se restan los bultos mayores y se incrementan automáticamente las unidades sueltas correspondientes.

---

## Capítulo 16: Clasificación y Taxonomía (Rubros, Categorías y Marcas)

Modal `AdministrarClasificacionDialog`: organiza el árbol de productos de la librería.

### Secciones
1. **Rubros y Categorías:** Maestro-detalle. Al seleccionar un Rubro (ej. *Papelería*), se listan a la derecha sus Categorías asociadas (ej. *Carpetas*, *Repuestos*, *Hojas*).
2. **Marcas y Editoriales:** Padrón de fabricantes con gestión de alias comerciales y prefijos EAN (los primeros dígitos del código de barras que identifican a la marca).
3. **Términos y Normalización:** Mapeo de términos de distribuidores hacia categorías internas para facilitar importaciones masivas.

---

## Capítulo 17: Padrón y Formulario de Proveedores Mayoristas

Accesible desde **Compras $\rightarrow$ Proveedores**.

### Carga del Proveedor (`ProveedorFormDialog`)
1. **CUIT (`*` Obligatorio):** 11 dígitos numéricos. El sistema aplica validación algorítmica de CUIT argentino (Módulo 11) impidiendo registrar números erróneos o inexistentes.
2. **Razón Social (`*` Obligatorio):** Nombre legal del distribuidor o editorial.
3. **Nombre de Fantasía:** Nombre comercial o de catálogo.
4. **Condición Tributaria:** Seleccione Responsable Inscripto, etc.
5. **Datos de Contacto:** Email de pedidos, teléfono de despacho y domicilio comercial.

---

## Capítulo 18: Equivalencias de Proveedor

Diálogo `EquivalenciasProveedorDialog`: resuelve la discrepancia de nombres entre la planilla del distribuidor y la base de datos interna.
* Permite homologar cómo llama el proveedor a una marca o categoría (ej. *"BIC ARGENTINA"* $\rightarrow$ *"Bic"*).
* Evita tener que clasificar manualmente los artículos en cada ingreso de mercadería.

---

## Capítulo 19: Importador de Catálogos y Planillas Excel/CSV

Accesible desde **Inventario $\rightarrow$ Catálogos mayoristas** o el botón "Importar Planilla" (`ImportarPlanillaDialog`).

### Procedimiento de Ingesta Masiva
1. **Selección del Archivo:** Haga clic en "Examinar" y elija el archivo `.xlsx`, `.xls` o `.csv` provisto por el mayorista.
2. **Mapeo de Columnas:** Asocie las columnas de la planilla con los campos del sistema:
   * Columna de Código / EAN.
   * Columna de Descripción / Título.
   * Columna de Precio de Costo.
   * Columna de Marca / Editorial.
3. **Streaming MiniExcel:** El procesamiento se ejecuta en streaming de bajo consumo de memoria RAM ($< 100\text{ MB}$ para más de 30.000 filas).
4. El catálogo queda cargado en la tabla de trabajo para su revisión antes de ingresar al inventario de la tienda.

---

## Capítulo 20: Asistente de Incorporación Asistida al Catálogo

Modal `IncorporarArticulosModalDialog`: asistente de 3 pasos para sumar artículos nuevos desde la lista mayorista hacia el catálogo oficial.

* **Paso 1 (Asociar Valores):** Resuelve marcas o categorías de la planilla que aún no tengan equivalencia en el sistema.
* **Paso 2 (Términos Nuevos):** Sugiere categorías automáticas en base a palabras clave de la descripción.
* **Paso 3 (Revisión de Margen y Precios):**
  * Permite definir un **Margen de Ganancia Global** para todo el lote (ej. `45%`).
  * Permite ajustar márgenes individuales en la grilla para artículos destacados.
  * Muestra el precio de venta sugerido resultante.
* Haga clic en **"Confirmar Incorporación"**: los artículos se dan de alta en el catálogo oficial de forma definitiva.

---

## Capítulo 21: Pedidos de Abastecimiento a Proveedores

Accesible desde **Compras $\rightarrow$ Pedidos**. Permite planificar las órdenes de compra evitando quiebres de stock.

### Estados del Pedido
* **Borrador:** Pedido en confección que puede modificarse o eliminarse.
* **Emitido:** Orden cerrada enviada al proveedor pendiente de entrega.
* **Recibido:** Mercadería entregada e ingresada al stock.
* **Cancelado:** Pedido desestimado con registro obligatorio de motivo.

---

## Capítulo 22: Armado y Emisión de Pedidos de Compra

Modal `PedirArticulosDialog`:
1. Agrupa automáticamente los ítems sugeridos según su **Fuente de Proveedor Principal**.
2. Muestra el stock actual, el stock mínimo y la cantidad recomendada a pedir para alcanzar el nivel óptimo.
3. Permite editar libremente las cantidades de cada renglón.
4. Presione **"Emitir Pedido"**: el pedido pasa a estado Emitido y queda listo para asociarse al remito o factura cuando el camión entregue los paquetes.

---

## Capítulo 23: Ingreso y Recepción de Compras (Facturas y Remitos)

Pantalla `ComprasView` (tecla **`[F12]`** para confirmar recepción).

### Carga del Comprobante
1. **Proveedor (`*` Obligatorio):** Seleccione el distribuidor del menú desplegable.
2. **Vincular Pedido (Opcional):** Haga clic en "Recibir pedido emitido..." para precargar automáticamente la grilla con los ítems y cantidades esperadas.
3. **Tipo de Comprobante:** Selector segmentado `Factura` o `Remito`.
4. **Punto de Venta y Número:** Formato `XXXXX - XXXXXXXX`.
5. **Fecha de Emisión:** Selección mediante calendario desplegable (`dd/MM/yyyy`).
6. **Grilla de Artículos Recibidos:**
   * Ingrese el código o busque el artículo en el cuadro predictivo.
   * Verifique la cantidad recibida.
   * Ingrese el **Costo Real Facturado**: si el costo subió o bajó respecto al costo histórico, la fila se resaltará en color y calculará el nuevo precio de venta sugerido para mantener el margen comercial.
7. Presione **`[F12]` ("Registrar Compra")**:
   * Se actualiza el stock físico de todos los artículos de la lista.
   * Se actualizan los costos de reposición de la fuente principal.
   * Se actualizan los precios de mostrador si correspondiera.
   * El pedido asociado pasa automáticamente a estado *Recibido*.

---

## Capítulo 24: Gestión de Usuarios y Roles de Seguridad

Accesible para Gerentes y Administradores desde **Administración $\rightarrow$ Usuarios** (tecla **`[F10]`**).

### Matriz de Roles (RBAC)
* **Cajero:** Opera exclusivamente el Punto de Venta (POS), clientes y su turno de caja. No puede ver compras ni alterar configuraciones de costo.
* **Encargado:** Gestiona ventas, stock, pedidos a proveedores y recepción de mercadería.
* **Gerente:** Acceso pleno al negocio, reportes de rentabilidad, modificación de precios y módulo de respaldos de base de datos.
* **Administrador:** Soporte técnico y de infraestructura. Gestiona usuarios y respaldos; no opera la venta diaria de mostrador.

---

## Capítulo 25: Formulario de Usuario y Blanqueo de Contraseña

### 1. Formulario de Operador (`UsuarioFormDialog`)
* **Nombre de Usuario (`*` Obligatorio):** Identificador único sin espacios (ej. `mperez`).
* **Nombre y Apellido (`*` Obligatorio):** Nombre completo del empleado.
* **Correo Electrónico (`*` Obligatorio):** Casilla institucional o personal.
* **Rol Asignado (`*` Obligatorio):** Cajero, Encargado, Gerente o Administrador.
* **Contraseña Inicial:** Mínimo 8 caracteres, al menos una mayúscula y un número.

### 2. Blanqueo de Contraseña (`CambiarPasswordDialog`)
Utilizado cuando un operador olvida su clave:
* El Gerente o Administrador ingresa la nueva contraseña provisoria y la reitera en el campo de confirmación.
* Al iniciar sesión por primera vez con esa clave, el sistema solicitará al operador elegir una contraseña personal privada.

---

## Capítulo 26: Copias de Seguridad y Respaldo de Base de Datos

Accesible desde **Administración $\rightarrow$ Copias** (tecla **`[F11]`**).

### Generación de Copia de Seguridad
1. Haga clic en **"Crear Copia de Seguridad Ahora"**.
2. El sistema ejecuta una copia nativa de SQL Server (`.bak`) en el directorio seguro del sistema.
3. El registro se incorpora automáticamente a la tabla histórica indicando:
   * Nombre del archivo con timestamp (ej. `retail_backup_20261107_120000.bak`).
   * Tamaño en megabytes legibles.
   * Operador que disparó la copia.

---

## Capítulo 27: Restauración Segura y Confirmación con Credencial

La restauración de base de datos (`ConfirmarCredencialDialog`) es una operación destructiva crítica que sobreescribe la información actual.

```
┌────────────────────────────────────────────────────────┐
│ Confirmar Restauración Crítica                         │
├────────────────────────────────────────────────────────┤
│                                                        │
│  [!] ADVERTENCIA FISCAL ARCA:                          │
│  Restaurar una base anterior no anula comprobantes     │
│  ya emitidos ante el fisco. Se requerirá conciliación. │
│                                                        │
│  Para continuar, escriba la palabra RESTAURAR:         │
│  [ RESTAURAR                                         ] │
│                                                        │
│  Contraseña del Gerente Autorizante:                   │
│  [ ••••••••••••                                      ] │
│                                                        │
│  [ Iniciar Restauración ]  (Enter)                     │
│                                                        │
└────────────────────────────────────────────────────────┘
```

### Protocolo de Seguridad
1. Seleccione la copia deseada en la pestaña "Copias" y presione "Restaurar".
2. **Palabra de Confirmación:** Escriba exactamente la palabra `RESTAURAR` en mayúsculas.
3. **Credencial:** Ingrese la contraseña del Gerente.
4. Presione **"Iniciar Restauración"**:
   * El sistema genera una copia preventiva de seguridad de la base actual antes de tocar los datos.
   * Aplica la restauración del backup seleccionado.
   * Notifica el éxito y procede al reinicio seguro de la aplicación.
   * Durante los siguientes 7 días, el Shell mostrará un aviso de advertencia para garantizar que el evento sea auditado.

---

## Capítulo 28: Tablero Gerencial e Indicadores de Rendimiento

Accesible exclusivamente para el rol Gerente en **Tablero** (tecla **`[F7]`**).

### Indicadores Clave (KPIs en Tiempo Real)
1. **Facturación Total del Período:** Monto global vendido comparado con el período inmediatamente anterior.
2. **Margen Bruto de Ganancia (%):** Rentabilidad comercial sobre los costos de reposición.
3. **Ticket Promedio:** Gasto medio por cliente atendido.
4. **Cantidad de Artículos Despachados:** Volumen físico de mercadería vendida.

Cada indicador cuenta con flechas de variación porcentual en verde (crecimiento) o rojo (caída) respecto al ciclo comparable.

---

## Capítulo 29: Reportes Analíticos y Exportación a Excel/PDF

Pestañas de reporte disponibles en la parte inferior del Tablero:
1. **Ventas por Período y Medios de Pago:** Grilla con desglose de totales cobrados en Efectivo, Débito, Crédito y QR.
2. **Rentabilidad por Rubro y Categoría:** Análisis de qué secciones de la librería generan mayor margen neto.
3. **Ranking de Artículos Más Vendidos:** Top de productos por volumen y por facturación.
4. **Ranking de Clientes:** Principales compradores de mostrador y cuentas corrientes.
5. **Auditoría de Turnos y Arqueos:** Historial de cierres de caja con diferencias de efectivo declaradas.

### Exportación
* Haga clic en **"Exportar a Excel (.xlsx)"** o **"Exportar a PDF (.pdf)"**.
* El sistema guarda el informe respetando el formato de moneda y tipografía tabular argentina.
* Si el archivo se encontrara abierto por otra aplicación (ej. Excel), el sistema ofrecerá la opción de reintentar sin perder los datos calculados.

---

## Capítulo 30: Ajustes del Sistema y Preferencias de Tema

Accesible desde el pie del menú lateral en el botón **Ajustes**.

### Opciones de Configuración
1. **Apariencia y Tema Visual:**
   * Selector segmentado: `Tema Claro` (fondo blanco/gris de alta luminosidad) o `Tema Oscuro` (superficies zinc/oscuras para baja fatiga visual nocturna).
   * El cambio se aplica en caliente en toda la interfaz sin necesidad de reiniciar la aplicación y se almacena de forma persistente para el operador conectado.
2. **Documentación y Ayuda:**
   * Acceso de un clic para abrir el Manual de Usuario en PDF en el visor nativo de Windows.
   * Botón para abrir el Centro de Ayuda interactivo.

---

## Capítulo 31: Centro de Ayuda y Consulta Rápida

Invocable en cualquier momento mediante la combinación de teclas **`[Ctrl + H]`** o desde el botón con ícono de interrogación (`RegularHelpCircle`) en el pie del sidebar.

```
┌────────────────────────────────────────────────────────┐
│ Ayuda y Soporte                             [ _ ] [ X ]│
├────────────────────────────────────────────────────────┤
│  Manual de Usuario en PDF                              │
│  Consulte la guía completa de operación de mostrador,  │
│  apertura y cierre de caja y facturación.              │
│  [📕 Abrir Manual de Usuario ]                         │
│                                                        │
│  Atajos Rápidos de Mostrador                           │
│   [F1]   Punto de Venta     [F5]   Caja y Turnos       │
│   [F2]   Artículos y Stock  [F10]  Usuarios            │
│   [F4]   Clientes           [F12]  Cobrar Venta        │
│   [Ctrl+B] Menú Lateral     [Ctrl+H] Ayuda             │
│   [Esc]  Cerrar / Cancelar                             │
│                                                        │
│  Retail POS v1.0.0 • Cátedra Universitaria  [Entendido]│
└────────────────────────────────────────────────────────┘
```

### Funcionalidades
1. **Botón "Abrir Manual de Usuario":** Lanza el documento PDF oficial en el lector predeterminado del sistema operativo (Microsoft Edge, Adobe Acrobat, etc.), permitiendo imprimir, buscar texto ($Ctrl+F$) o estudiar los procedimientos en una segunda pantalla.
2. **Chuleta de Atajos Rápidos:** Tarjeta de consulta visual instantánea para cajeros y personal de mostrador sin abandonar la tarea en curso.
3. Presione **`[Esc]`** o **`[Enter]`** para cerrar el diálogo y retomar la operación inmediatamente.

