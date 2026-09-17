# Sistema de Diseño XAML y Guía de Estilos de UI (Retail POS)

### Proyecto: Retail (Sistema ERP & Punto de Venta para Librería)
**Plataforma:** .NET 8 LTS | WPF (Windows) | C# 12 | WPF-UI 4.0.0 (Fluent Design)  
**Estilo Base:** Windows 11 Modern Fluent Design adaptado a la velocidad y ergonomía de mostrador comercial.

---

## 🎯 Directivas Inviolables de Maquetación XAML

Todo agente o desarrollador que cree o modifique interfaces de usuario (`Views/Pages/`, `Views/Dialogs/` o `MainWindow.xaml`) debe cumplir estrictamente las siguientes cuatro reglas:

### 1. Prohibición de Colores y Pinceles Ad-Hoc
* **Queda estrictamente prohibido** hardcodear códigos hexadecimales de color (`#...`) o nombres de colores estándar de WPF (`Red`, `Blue`, `White`, etc.) directamente en las propiedades de los controles XAML (`Background`, `Foreground`, `BorderBrush`, etc.).
* Toda superficie, texto, borde, foco o estado interactivo debe consumir exclusivamente los tokens semánticos definidos en [`src/Retail.App/Styles/Colors.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Styles/Colors.xaml) utilizando `{DynamicResource NombreBrush}`.
* Esto garantiza soporte inmediato para cambios de tema, adaptación de contrastes y consistencia visual en toda la suite.

### 2. Contenedor Obligatorio (`ui:FluentWindow` + `ui:TitleBar`)
* Toda ventana principal o secundaria (incluyendo ventanas modales independientes) debe heredar de `ui:FluentWindow` en lugar de la clase base clásica `System.Windows.Window`.
* Debe incluir la directiva de extensión en la barra de título: `ExtendsContentIntoTitleBar="True"` y `WindowBackdropType="Mica"`.
* Debe incrustar en su primera fila (`Grid.Row="0"`) el control **`<ui:TitleBar>`**:
  * **Ventana Principal (Shell):** `Height="32"`, `MinHeight="32"`, provee botones de Minimizar, Maximizar (con soporte para **Snap Layouts** de Windows 11) y Cerrar.
  * **Ventanas Modales y Diálogos:** Deben configurar su `<ui:TitleBar>` con `Height="32"`, `Padding="8,0"`, `ShowMaximize="False"` y `CanMaximize="False"`, conservando el botón de cierre nativo y el template visual del framework.
* **Salvaguarda de Pantalla:** Para evitar que la barra de título quede oculta fuera de los límites de monitores de 768p o pantallas con escalado DPI al 125%/150%, las dimensiones deben ser seguras ($Width \le 1180$, $Height \le 680$, $MinHeight \le 560$) y se debe fijar en el constructor:
  ```csharp
  MaxHeight = SystemParameters.WorkArea.Height;
  MaxWidth = SystemParameters.WorkArea.Width;
  ```

### 3. Tipografía Dual de Alta Precisión
* **Interfaz General y Formularios:** Utilizar la fuente nativa de Windows 11 `Segoe UI Variable Text` / `Segoe UI Variable Display` mediante los estilos de [`src/Retail.App/Styles/Typography.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Styles/Typography.xaml) (`TextBlockHeader1`, `TextBlockHeader2`, `TextBlockBody`, etc.).
* **Mostrador, Moneda y Códigos Numéricos:** Es **obligatorio** utilizar tipografía monoespaciada `Cascadia Code` (`FontFamily="{StaticResource FontFamilyMonospace}"`) en:
  * El total gigante de venta de mostrador (`TextBlockDisplayCurrency`).
  * Todas las celdas de precios unitarios y subtotales en `DataGrid` (`TextBlockCurrencyCell`).
  * Cantidades de stock y números de comprobantes fiscales.
  * Etiquetas de teclas de atajo de teclado (`TextBlockKeycap`).
  * Códigos de barras de artículos.  
  *Justificación:* Cada dígito ocupa exactamente los mismos píxeles de ancho horizontal, garantizando que los decimales y comas se alineen verticalmente con rigor contable.

### 4. Componentes y Botones de Mostrador
* Las acciones de mostrador deben utilizar prioritariamente la botonera con teclas de función (`F1` a `F12`):
  * Botones estándar de función: `Style="{StaticResource KeycapButtonStyle}"` conteniendo una pastilla `<Border Style="{StaticResource KeycapBadgeStyle}">`.
  * Botones de conmutación y filtro de mostrador (`ToggleButton`): `Style="{StaticResource KeycapToggleButtonStyle}"` para filtros con estado activo/inactivo persistente (ej. `Solo Stock Crítico [F3]`).
  * Botón principal de cobro / confirmación: `Style="{StaticResource PrimaryActionButtonStyle}"` conteniendo la pastilla carmín `<Border Style="{StaticResource KeycapPrimaryBadgeStyle}">`.
* Las tablas deben implementar obligatoriamente `Style="{StaticResource DataGridRetailStyle}"`, sus filas `DataGridRowRetailStyle` y sus celdas `DataGridCellRetailStyle`, garantizando selección suave horizontal sin bordes verticales divisorios.
* Los estados de negocio (stock bajo, éxito fiscal, errores) deben representarse con los badges semáforo: `BadgeWarningStyle`, `BadgeDangerStyle` y `BadgeSuccessStyle`.

### 5. Diálogos Modales y Formularios
* **Barra de Título Estandarizada:** Todo diálogo modal debe configurar en su `<ui:TitleBar>`: `Height="32"`, `Padding="8,0"`, `ShowMaximize="False"` y `CanMaximize="False"`, asegurando presencia del botón de cierre y preservación del `ControlTemplate`.
* **Dimensiones Normalizadas de Diálogos Modales:**
  * **Modal Compacto (Auth / Cambios de Clave):** `Width="460"`, `Height="390"`.
  * **Modal Estándar (ABM Clientes / Usuarios / Cobranzas):** `Width="520-540"`, `Height="620-640"`.
  * **Modal Amplio (Catálogo Artículos / Fórmulas):** `Width="580"`, `Height="660"`.
* **Estilos Globales de Formularios:**
  * **Selectores (`ComboBox` / `ComboBoxItem`):** Estilos implícitos globales con hover suave (`SurfaceHoverBrush`), selección activa en `PrimaryLightBrush` con texto en `PrimaryPressedBrush` (`#6B0720`) y tipografía semi-negrita.
  * **Casillas de Verificación (`CheckBox`):** Tipografía de interfaz (`FontFamilyText`), `FontSize="14"`, cursor de mano y texto `TextPrimaryBrush`.
  * **Entradas de Texto (`TextBox` / `PasswordBox`):** Sin alterar márgenes, paddings ni dimensiones. Se define el color de marca Carmín (`#9D0F33`) exclusivamente en la línea inferior activa al recibir foco (`TextControlFocusedBorderBrush`), cursor (`CaretBrush`) y resaltado de selección (`SelectionBrush`).

---

## 🎨 Catálogo de Tokens Semánticos de Color

Definidos en [`src/Retail.App/Styles/Colors.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Styles/Colors.xaml):

### 1. Paleta de Marca (Carmín / Borravino)
| Token XAML | Valor Hex | Uso Semántico |
| :--- | :--- | :--- |
| `PrimaryBrush` | `#9D0F33` | Acción principal de mostrador (Cobro), bordes de foco, títulos de acento. |
| `PrimaryHoverBrush` | `#850C2A` | Estado sobrevolado (*hover*) de botones primarios. |
| `PrimaryPressedBrush` | `#6B0720` | Estado presionado (*pressed*) y texto de selección activa en grillas. |
| `PrimaryLightBrush` | `#FDF2F4` | Fondo suave de filas seleccionadas en `DataGrid` y *hover* de botones de mostrador. |
| `PrimaryForegroundBrush`| `#FFFFFF` | Texto legible sobre superficies carmín. |

### 2. Superficies y Fondos (Zinc Neutral)
| Token XAML | Valor Hex | Uso Semántico |
| :--- | :--- | :--- |
| `SurfaceBackgroundBrush` | `#FAFAFA` | Fondo de ventana principal y páginas de contenido. |
| `SurfaceCardBrush` | `#FFFFFF` | Fondo de tarjetas contenedoras y filas de datos. |
| `SurfaceBorderBrush` | `#E4E4E7` | Líneas divisorias, bordes de inputs y separadores de grillas. |
| `SurfaceBorderFocusedBrush`| `#9D0F33` | Borde de input con foco activo de teclado. |
| `SurfaceHoverBrush` | `#FDF2F4` | Fondo de fila sobrevolada en `DataGrid`. |
| `HeaderBackgroundBrush` | `#18181B` | Fondos oscuros de contraste o barras institucionales. |

### 3. Textos y Jerarquía de Lectura
| Token XAML | Valor Hex | Uso Semántico |
| :--- | :--- | :--- |
| `TextPrimaryBrush` | `#18181B` | Títulos, descripciones de artículos, subtotales principales. |
| `TextSecondaryBrush` | `#52525B` | Etiquetas de formulario, nombres de columnas de grilla, metadatos. |
| `TextMutedBrush` | `#A1A1AA` | Textos deshabilitados, marcas de agua (*placeholders*), versiones. |
| `TextOnDarkBrush` | `#FAFAFA` | Texto legible sobre fondos oscuros o barras de cabecera. |

### 4. Semáforos de Negocio (Librería POS)
| Token Semáforo | Pincel Fondo | Pincel Texto | Pincel Borde | Caso de Uso en Negocio |
| :--- | :--- | :--- | :--- | :--- |
| **Alerta (Warning)** | `WarningBackgroundBrush` (`#FEF3C7`) | `WarningForegroundBrush` (`#92400E`) | `WarningBorderBrush` (`#FCD34D`) | Stock $\le$ Stock Mínimo (`RF-08`), advertencias de turno de caja. |
| **Peligro (Danger)** | `DangerBackgroundBrush` (`#FEE2E2`) | `DangerForegroundBrush` (`#991B1B`) | `DangerBorderBrush` (`#FCA5A5`) | Error fiscal ARCA (`RF-16`), límite de crédito excedido (`RF-02`), anulación de línea. |
| **Éxito (Success)** | `SuccessBackgroundBrush` (`#D1FAE5`) | `SuccessForegroundBrush` (`#065F46`) | `SuccessBorderBrush` (`#6EE7B7`) | Cobro confirmado, turno de caja abierto, sincronización fiscal correcta. |

### 5. Pastillas de Teclado Rápido (Keycaps F1 a F12)
| Token XAML | Valor Hex | Uso Semántico |
| :--- | :--- | :--- |
| `KeycapBackgroundBrush` | `#27272A` | Fondo oscuro de la tecla rápida en botones estándar. |
| `KeycapBorderBrush` | `#3F3F46` | Borde sutil de la tecla rápida en botones estándar. |
| `KeycapPrimaryBackgroundBrush` | `#6B0720` | Fondo carmín oscuro de la tecla rápida en botones primarios (`[F12]`). |
| `KeycapPrimaryBorderBrush` | `#850C2A` | Borde carmín de la tecla rápida en botones primarios. |

---

## 🔤 Jerarquía Tipográfica y Estilos de Texto

Definidos en [`src/Retail.App/Styles/Typography.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Styles/Typography.xaml):

| Estilo XAML | Fuente | Tamaño | Peso | Uso Principal |
| :--- | :--- | :--- | :--- | :--- |
| `TextBlockHeader1` | Segoe UI Variable | 22 px | SemiBold | Título de módulo o ventana principal. |
| `TextBlockHeader2` | Segoe UI Variable | 16 px | SemiBold | Títulos de secciones o tarjetas secundarias. |
| `TextBlockBody` | Segoe UI Variable | 14 px | Regular | Textos descriptivos, campos de formulario. |
| `TextBlockCaption` | Segoe UI Variable | 12 px | Regular | Notas al pie, marcas de versión, instrucciones. |
| `TextBlockDisplayCurrency` | Cascadia Code | 32 px | Bold | **Total gigante de la venta actual (mostrador).** |
| `TextBlockCurrencyCell` | Cascadia Code | 14 px | SemiBold | Precios unitarios y subtotales en columnas de grillas. |
| `TextBlockKeycap` | Cascadia Code | 11 px | Bold | Letras de teclas de atajo (`F1`, `F4`, `F12`, `ESC`). |

---

## 📐 Iconografía Oficial (MahApps.Metro.IconPacks.BoxIcons)

La aplicación estandariza su iconografía mediante el paquete modular **`MahApps.Metro.IconPacks.BoxIcons`**, complementado con geometrías nativas `StreamGeometry` en [`src/Retail.App/Styles/Icons.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Styles/Icons.xaml).

### 1. Espacio de Nombres en Vistas XAML
```xml
xmlns:iconPacks="http://metro.mahapps.com/winfx/xaml/iconpacks"
```

### 2. Convención Semántica de Glifos (Regular vs Solid)
* **`Solid...` (Relleno):** Utilizado para el ítem activo de navegación en Sidebar, botón principal de cobro, cabeceras de diálogo y badges semáforo.
  * Punto de Venta (POS): `Kind="SolidReceipt"`
  * Catálogo de Artículos: `Kind="SolidBook"`
  * Clientes y Ctas. Ctes.: `Kind="SolidUser"`
  * Caja y Cobros: `Kind="SolidDollarCircle"`
  * Compras a Distribuidores: `Kind="SolidCart"`
  * Proveedores: `Kind="SolidTruck"`
  * Operadores y Roles: `Kind="SolidUserIdCard"`
  * Consola Fiscal ARCA: `Kind="SolidBadgeCheck"`
  * Alertas de Incidencia / Stock: `Kind="SolidAlertTriangle"`
* **`Regular...` (Línea):** Utilizado para acciones secundarias, botones de grillas de datos y búsquedas.
  * Búsqueda en inputs (`ui:TextBox`): `<ui:ImageIcon Source="{iconPacks:BoxIconsImage Kind=RegularSearch, Brush={StaticResource TextMutedBrush}}" Width="16" Height="16" />`
  * Refrescar datos: `Kind="RegularRefreshCw"`
  * Modificar / Editar fila: `Kind="RegularEdit"`
  * Restablecer Clave: `Kind="RegularKey"`
  * Baja Lógica (Soft Delete): `Kind="RegularTrash"`
  * Confirmación / Guardado: `Kind="RegularCheck"`

### 3. Geometrías Nativas `StreamGeometry` (Acciones Críticas de Mostrador)
Definidos en [`src/Retail.App/Styles/Icons.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Styles/Icons.xaml) para consumo por hardware directo en atajos de teclado de mostrador:
* `GeometryBarcode`: Escáner de código de barras.
* `GeometrySearch`: Lupa para búsqueda predictiva en catálogo.
* `GeometryReceipt`: Facturas y comprobantes fiscales ARCA.
* `GeometryCash`: Efectivo y apertura/cierre de gaveta de caja.
* `GeometryCard`: Pagos con tarjetas de crédito/débito.
* `GeometryBox`: Control de stock e inventario de librería.
* `GeometryWarning`: Triángulo de advertencia para stock mínimo.
* `GeometryLock` / `GeometryUser`: Seguridad de usuarios, PIN de cajero y permisos.

---

## 🧩 Snippets y Patrones XAML Canónicos

### 1. Botón de Mostrador con Atajo de Teclado (F1 a F12)
```xml
<Button Style="{StaticResource KeycapButtonStyle}" Margin="0,0,8,0">
    <StackPanel Orientation="Horizontal">
        <Path Data="{StaticResource GeometrySearch}"
              Fill="{DynamicResource PrimaryBrush}"
              Width="16" Height="16" Stretch="Uniform" Margin="0,0,8,0" />
        <TextBlock Text="Buscar Artículo" VerticalAlignment="Center" />
        <Border Style="{StaticResource KeycapBadgeStyle}">
            <TextBlock Text="F1" Style="{StaticResource TextBlockKeycap}" />
        </Border>
    </StackPanel>
</Button>
```

### 2. Botón Primario de Cobro (`[F12]`)
```xml
<Button Style="{StaticResource PrimaryActionButtonStyle}" Margin="0,0,8,0">
    <StackPanel Orientation="Horizontal">
        <Path Data="{StaticResource GeometryCash}"
              Fill="White" Width="16" Height="16" Stretch="Uniform" Margin="0,0,8,0" />
        <TextBlock Text="Cobrar Venta" Foreground="White" FontWeight="Bold" VerticalAlignment="Center" />
        <Border Style="{StaticResource KeycapPrimaryBadgeStyle}">
            <TextBlock Text="F12" Style="{StaticResource TextBlockKeycap}" Foreground="White" />
        </Border>
    </StackPanel>
</Button>
```

### 3. Columna de Precio en `DataGrid` (Monospace Contable Alineado a la Derecha)
```xml
<DataGridTextColumn Header="Precio Unit."
                    Binding="{Binding PrecioVentaFormateado}"
                    ElementStyle="{StaticResource TextBlockCurrencyCell}"
                    Width="140" />
```

### 4. Columna de Texto Estándar en `DataGrid` (Centrado Vertical)
```xml
<DataGridTextColumn Header="Usuario"
                    Binding="{Binding NombreUsuario}"
                    ElementStyle="{StaticResource TextBlockTextCell}"
                    Width="140" />
```

### 5. Badge Semáforo de Stock Bajo
```xml
<Border Style="{StaticResource BadgeWarningStyle}"
        Visibility="{Binding StockBajo, Converter={StaticResource BoolToVisibilityConverter}}">
    <TextBlock Text="Stock Bajo"
               FontSize="11"
               FontWeight="Bold"
               Foreground="{DynamicResource WarningForegroundBrush}" />
</Border>
```

---

## 🔍 Referencia Viva (Living Styleguide)

Para comprobar el renderizado interactivo, contrastes, fuentes y comportamiento dinámico de todos los controles de este sistema de diseño, consulte el código y ejecute la galería interactiva:
* **Vista XAML:** [`src/Retail.App/Views/Dev/StyleGalleryView.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Dev/StyleGalleryView.xaml)
* **Acceso en tiempo de ejecución:** Ejecutar la aplicación (`dotnet run --project src/Retail.App`) y pulsar el botón **"Ver Galería de Controles (Etapa 0.6)"** en la barra superior.
