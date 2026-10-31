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
* **No mergea diccionarios propios:** `ui:ThemesDictionary`, `ui:ControlsDictionary` y los diccionarios de `Styles/` se cargan una sola vez en [`App.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/App.xaml) y toda ventana los hereda. `<ui:FluentWindow.Resources>` queda solo para las claves propias de la ventana (convertidores, constantes). Una copia local de `ui:ThemesDictionary` tapa el acento carmín que aplica `ApplicationAccentColorManager` en `App.xaml.cs` y repone en esa ventana el azul de Windows. `scripts/audit-xaml.ps1` lo rechaza.
* Debe incrustar en su primera fila (`Grid.Row="0"`) el control **`<ui:TitleBar>`**:
  * **Ventana Principal (Shell):** `Height="32"`, `MinHeight="32"`, `Background="{DynamicResource SurfaceBackgroundBrush}"`, título alineado a la izquierda estilizado con `FontFamily="{StaticResource FontFamilyText}"`, `FontSize="12"`, `FontWeight="SemiBold"` y `Foreground="{DynamicResource TextSecondaryBrush}"`. Provee botones de Minimizar, Maximizar (con soporte para **Snap Layouts** de Windows 11) y Cerrar.
  * **Ventanas Modales y Diálogos:** Deben configurar su `<ui:TitleBar>` con `Height="32"`, `Padding="8,0"`, `Background="{DynamicResource SurfaceBackgroundBrush}"`, `ShowMaximize="False"` y `CanMaximize="False"`, sin ícono, y con el título centrado mediante `<ui:TitleBar.CenterContent>` (`FontFamily="{StaticResource FontFamilyText}"`, `FontSize="12"`, `FontWeight="SemiBold"`), conservando el botón de cierre nativo y el template visual del framework.
* **Salvaguarda de Pantalla:** Para evitar que la barra de título quede oculta fuera de los límites de monitores de 768p o pantallas con escalado DPI al 125%/150%, las dimensiones deben ser seguras ($Width \le 1180$, $Height \le 680$, $MinHeight \le 560$) y se debe fijar en el constructor:
  ```csharp
  MaxHeight = SystemParameters.WorkArea.Height;
  MaxWidth = SystemParameters.WorkArea.Width;
  ```

### 3. Tipografía Dual de Alta Precisión
* **Interfaz General y Formularios:** Utilizar la fuente nativa de Windows 11 `Segoe UI Variable Text` / `Segoe UI Variable Display` mediante los estilos de [`src/Retail.App/Styles/Typography.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Styles/Typography.xaml) (`TextBlockHeader1`, `TextBlockHeader2`, `TextBlockBody`, etc.).
* **Importes, Cantidades y Stock (D-23):** Es **obligatorio** utilizar `FontFamily="{StaticResource FontFamilyNumeric}"` (Segoe UI Variable Display) **junto con** `Typography.NumeralAlignment="Tabular"`, o los estilos que ya los traen:
  * El total gigante de venta de mostrador (`TextBlockDisplayCurrency`) y los importes destacados de modales (`TextBlockCurrencyLarge`).
  * Las celdas de precios unitarios y subtotales en `DataGrid` (`TextBlockCurrencyCell`) y las celdas numéricas (`TextBlockNumericCell`: número de fila, cantidades, stock).
  * Los campos de edición de montos, porcentajes y cantidades.  
  *Justificación:* con números **tabulares**, cada dígito ocupa el mismo ancho y los decimales se alinean verticalmente con rigor contable, igual que con una fuente monoespaciada (verificado: `111111` y `000000` miden lo mismo). Se reemplazó Cascadia Code en los importes porque es una fuente de programación: alineaba bien pero se leía como código, y su punto y su coma ocupan el ancho de un dígito.
* **Códigos y Textos Técnicos:** `Cascadia Code` (`FontFamily="{StaticResource FontFamilyMonospace}"`) se reserva para lo que **es** un código: códigos de barras, CUIT/DNI, códigos de proveedor, números de comprobante, teclas de atajo (`TextBlockKeycap`), códigos de color y trazas técnicas (`TextBlockMonospaceCell` en grillas).
* **Dependencia de la PC:** ninguna fuente viene incluida en la aplicación. Segoe UI Variable viene con Windows 11 (en Windows 10 cae a Segoe UI); si falta Cascadia Code, WPF usa Consolas.

### 4. Componentes y Botones de Mostrador
* Las acciones de mostrador deben utilizar prioritariamente la botonera con teclas de función (`F1` a `F12`):
  * Botones estándar de función: `Style="{StaticResource KeycapButtonStyle}"` conteniendo una pastilla `<Border Style="{StaticResource KeycapBadgeStyle}">`.
  * Botones de conmutación y filtro de mostrador (`ToggleButton`): `Style="{StaticResource KeycapToggleButtonStyle}"` para filtros con estado activo/inactivo persistente (ej. `Solo con Deuda`). Los filtros **no llevan atajo de teclado** ni pastilla de tecla.
  * Botón principal de cobro / confirmación: `Style="{StaticResource PrimaryActionButtonStyle}"` conteniendo la pastilla carmín `<Border Style="{StaticResource KeycapPrimaryBadgeStyle}">`.
  * **Excepción, tarjeta de la venta del POS:** "Cobrar venta" (`PrimaryActionButtonStyle`, solo texto) y "Cancelar" (`KeycapButtonStyle` con `RegularX`) van dentro de la tarjeta del total, bajo una línea interna, con `Height="42"` y **sin pastilla**: el atajo (`[F12]`, `[F8]`) va en el `ToolTip`. Así el total sigue siendo lo más destacado de la columna.
* Las tablas deben implementar obligatoriamente `Style="{StaticResource DataGridRetailStyle}"`, sus filas `DataGridRowRetailStyle` y sus celdas `DataGridCellRetailStyle`, garantizando selección suave horizontal sin bordes verticales divisorios. El estilo desplaza **por píxeles** (`VirtualizingPanel.ScrollUnit="Pixel"`) sin perder la virtualización: `ScrollViewer.CanContentScroll` queda en `True`, porque con `False` la grilla dibuja todas las filas (RNF-03). La barra horizontal es automática y solo aparece si las columnas no entran: por eso **toda columna `Width="*"` declara su `MinWidth`** (240 para una descripción), y no se usa `MinColumnWidth` global, que ensancharía las columnas de íconos.
* **Encabezados alineados con su celda:** el título de una columna sigue la alineación de sus datos. Las columnas de importes y números (`TextBlockCurrencyCell`, `CeldaNumericaDerecha`) declaran `HeaderStyle="{StaticResource DataGridColumnHeaderRightStyle}"`, para que el título quede sobre los dígitos; las que centran controles (cantidad con `-` / `+`, acciones) usan `DataGridColumnHeaderCenterStyle`. Las grillas compactas del Tablero usan `EncabezadoMuyCompactoDerecha` / `EncabezadoCompactoDerecha`, que conservan su relleno reducido. `scripts/audit-xaml.ps1` rechaza una `DataGridTextColumn` con celda a la derecha y sin `HeaderStyle`; no alcanza a las `DataGridTemplateColumn`, donde se declara a mano.
* **Contador de cantidad del POS:** `-` y `+` son `KeycapButtonStyle` de 24×24 con `RegularMinus` / `RegularPlus`, de la misma altura que el cuadro de la cantidad. Los tres miden 24 px porque es el alto útil de la celda (fila de 42 px menos sus bordes y el relleno de 8 px): un control más alto se recorta. Tras el clic el foco vuelve al buscador (`OnAjusteCantidadClick`), igual que con Enter o Esc: el botón no queda marcado y el cajero sigue escaneando.
* **Controles editables dentro de celdas de `DataGrid`** (`ComboBox`, `TextBox`, `CheckBox` en un `CellTemplate`): su binding declara siempre `UpdateSourceTrigger=PropertyChanged`. La grilla agrupa los bindings de cada fila en un `BindingGroup` y, sin el trigger explícito, retiene el valor hasta que la fila confirma la edición, cosa que nunca pasa con `DataGridRetailStyle` (`IsReadOnly=True`): la pantalla muestra lo elegido pero el ViewModel no lo recibe. Lo verifica [`EdicionEnCeldasDeGrillaTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.App.UnitTests/EdicionEnCeldasDeGrillaTests.cs).
* **Indicador de pasos** (diálogos con recorrido, p. ej. la incorporación con sugerencias): círculos numerados unidos por una línea, carmín sólido el paso actual, carmín suave con tilde los completados y gris los pendientes; sin borde de pastilla ni cursor de mano, porque informa y no navega. El indicador va en la **misma fila** que el título de la tarjeta y su interruptor, y los pasos no repiten título propio. La navegación va en el **pie, como en un asistente**: `Cancelar · ← Anterior · Siguiente: <destino> →` en los pasos previos y `Cancelar · ← Anterior · Confirmar` en el último (M-70); "Confirmar" no aparece antes del último paso.
* Los estados de negocio (stock bajo, éxito fiscal, errores) deben representarse con los badges semáforo: `BadgeWarningStyle`, `BadgeDangerStyle` y `BadgeSuccessStyle`.
* **Desplegables de resultados de búsqueda** (buscador del POS): contenedor flotante con borde `SurfaceBorderBrush` y sombra (`DropShadowEffect` con `ElevationShadowColor`) para despegarlo de la página; filas con `ItemContainerStyle="{StaticResource SearchResultListBoxItemStyle}"` (fila completa, cursor de mano, carmín sólido al seleccionarse) y textos con `SearchResultTitleTextStyle`, `SearchResultAmountTextStyle` y `SearchResultMetaTextStyle`, que pasan a `PrimaryForegroundBrush` / `PrimaryLightBrush` sobre el carmín. El hover selecciona la fila: mouse y teclado comparten un único estado de selección. Ver la sección 6.1 de [`StyleGalleryView.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Dev/StyleGalleryView.xaml).
* **Botones de ícono:** para eliminar una fila de grilla o lista, `IconDangerButtonStyle` (28×28, sin borde ni fondo, ícono gris `TextMutedBrush` que pasa a `DangerForegroundBrush` sobre `DangerBackgroundBrush` en hover); para una acción secundaria de solo ícono junto a un botón primario, `IconPrimarySquareButtonStyle` (56×56, `PrimaryLightBrush` con borde e ícono carmín; en hover, el carmín sólido del botón primario; deshabilitado con opacidad 0,45 y cursor de prohibido, igual que `PrimaryActionButtonStyle`). El ícono **no** fija `Foreground`: lo hereda del botón. Siempre con `ToolTip` que nombre la acción y su atajo. Ver la sección 6.2 de la galería.
* **Enums en pantalla:** nunca se muestra el nombre de un enum del Dominio (`TarjetaDebito`). Los `ComboBox` usan `ItemTemplate="{StaticResource EnumTextoItemTemplate}"` y los `TextBlock` `Converter={StaticResource EnumATextoConverter}`, ambos en `Controls.xaml`. Los textos se definen en [`EnumATextoConverter.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Converters/EnumATextoConverter.cs) (presentación, no Dominio).
* **Visibilidad invertida:** el `BooleanToVisibilityConverter` de WPF **ignora** `ConverterParameter`. Para ocultar con `true` se usa `InverseBooleanToVisibilityConverter` (declarado en los recursos de la vista, como `CajaView` y `PosView`), y un `string` nunca se enlaza a un conversor booleano: se expone una propiedad `bool` (`TieneError`).

### 5. Diálogos Modales y Formularios
* **Barra de Título Estandarizada:** Todo diálogo modal debe configurar en su `<ui:TitleBar>`: `Height="32"`, `Padding="8,0"`, `Background="{DynamicResource SurfaceBackgroundBrush}"`, `ShowMaximize="False"` y `CanMaximize="False"`, sin ícono, y con el título centrado mediante `<ui:TitleBar.CenterContent>` asegurando presencia del botón de cierre y preservación del `ControlTemplate`.
* **Dimensiones Normalizadas de Diálogos Modales:**
  * **Modal Compacto (Auth / Cambios de Clave):** `Width="460"`, `Height="390"`.
  * **Modal Estándar (ABM Clientes / Usuarios / Cobranzas):** `Width="520-540"`, `Height="620-640"`.
  * **Modal Amplio (Fórmulas / Importar planilla):** `Width="580"`, `Height="660"`.
  * **Modal de Ficha en dos columnas (Artículo de catálogo):** `Width="960"`, `SizeToContent="Height"` y `MinHeight="0"` (como `ClasificacionFormDialog`): la ventana mide lo que su contenido, sin espacio vacío bajo las tarjetas, y el tope lo pone `MaxHeight = WorkArea.Height`. El valor de solo lectura junto a un cuadro editable (el costo fijado por la principal) es un campo gris con candado de la misma altura que el cuadro vecino (`Height="{Binding ActualHeight, ElementName=…}"`), no texto suelto. A la izquierda, qué es el artículo (Identificación y Stock); a la derecha, cuánto cuesta (Fuentes y Precio), en el orden en que se calcula. La lista de fuentes scrollea sola (`MaxHeight="168"`, unos 3 renglones), de modo que el precio de venta queda siempre a la vista. Se conserva el `ScrollViewer` externo como red de seguridad en pantallas de 768p con escalado.
* **Encabezado de tarjeta en formularios (`CardHeaderIconStyle` + `CardHeaderTextStyle`, en `Controls.xaml`):** ícono BoxIcons de 18 y título de 15 SemiBold, ambos en `TextSubtleBrush`, para que se lean como grabados en la tarjeta y no compitan con los datos. El tamaño se calibra en esos dos estilos y lo toman todas las tarjetas. Las etiquetas de campo no llevan "(Opcional)": solo los obligatorios se marcan con `*`.
* **Elegir entre opciones excluyentes:** dos estilos de `RadioButton`, según cuántas opciones haya. No reemplazan al `ui:ToggleSwitch`, que activa o desactiva un **modo**: "Servicio encendido" no significa nada.
  * **Selector segmentado (`SegmentedControlBorderStyle` + `SegmentedRadioButtonStyle`):** dos o tres opciones fijas, como "Producto · Servicio". Riel `SurfaceBorderBrush` con las opciones en un `UniformGrid`; la elegida flota en `SurfaceCardBrush`, sin carmín, y el foco de teclado marca su borde en `PrimaryBrush`. Cada opción lleva ícono + texto y `AutomationProperties.Name`.
  * **Píldoras (`PillRadioButtonStyle`):** muchas opciones que pueden pasar de línea, como las pestañas de reporte del Tablero. Selección de marca (`PrimaryLightBrush`, borde `PrimaryBrush`, D-28).
  * **Enlace:** todos los botones del grupo enlazan **una sola** propiedad con `IgualAParametroConverter` y su valor en `ConverterParameter` (un enum, o `sys:Boolean` para un sí/no). Desmarcarse no escribe nada. No se enlaza cada botón a una propiedad booleana inversa (`EsProducto` / `EsServicio`): el formulario de artículo lo hacía así y abría sin ninguna opción marcada.
* **Entrada numérica (`helpers:EntradaNumericaHelper`):** ayuda de entrada que filtra lo tipeado y lo pegado y bloquea el espacio; la validación sigue en el ViewModel.
  * `SoloDigitos="True"` para cantidades enteras: stock actual y stock mínimo.
  * `SoloDecimal="True"` para importes y porcentajes: costo de reposición y margen. Admite un único separador decimal, el de la cultura con la que el binding interpreta el número (el `Language` del cuadro), porque aceptar el otro haría que "6,5" se leyera como 65. Tipear "." o "," inserta el separador correcto.
* **Importe resultado en un formulario** (p. ej. el precio de venta): recuadro neutro (`SurfaceBackgroundBrush` con borde `SurfaceBorderBrush`) e importe en `TextBlockCurrencyLarge`. El carmín queda para la acción principal: un recuadro carmín suave con borde se confunde con un aviso.
* **Fila destacada en una lista de formulario** (la fuente principal): fondo `PrimaryLightBrush` por `DataTrigger`, con la lista recortada por `helpers:EsquinasHelper.RadioDelRecorte` para que el fondo no tape las esquinas redondeadas del borde.
* **Dato con historial (stock actual en edición):** se muestra como texto con un botón "Ajustar" de la misma altura que el cuadro vecino (`Height="{Binding ActualHeight, ElementName=…}"`) que habilita el campo y deja ver la diferencia ("Ajuste: 0 → 12 (+12)") con "Aceptar" y "Descartar". En el alta el campo se edita directo como "Stock inicial", porque cargar la existencia inicial no es un ajuste. Cuando exista el registro de ajustes con motivo, el mismo botón abrirá su diálogo.
  * **Modal de Trabajo (Incorporar artículos con sugerencias):** `Width="1040"`, `Height="680"`, redimensionable. Reservado a diálogos donde se revisa una grilla editable fila por fila (Track 6.6, P6-9); el contenido se agrupa en tarjetas por tema (p. ej. "Ganancia global" y "Clasificación").
* **Interruptor de modo (`ui:ToggleSwitch`):** para activar o desactivar un modo de trabajo dentro de un diálogo (p. ej. "Sugerir clasificación"), en el encabezado de la tarjeta que controla, y también para filtros de lista sí/no (p. ej. "Solo ambiguos"). Encendido toma el carmín exacto de marca, porque [`Colors.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Styles/Colors.xaml) redefine `ToggleSwitchFillOn*` / `ToggleSwitchStrokeOn*` con los colores `SystemAccentColor*`; el resto de los acentos de WPF-UI (`AccentFillColor*Brush`) los calcula `ApplicationAccentColorManager` a partir del carmín.
* **Opciones "Sin …" en selectores:** cuando "ninguno" es una elección válida, va como opción del combo (`OpcionDiccionario` con `Id` nulo), no como casilla aparte. El combo **nunca se ve vacío**: empieza en `OpcionDiccionario.SinClasificar` ("Sin clasificar", nadie decidió). Solo se ofrece una opción "Sin …" confirmada si el dato la distingue de "sin clasificar": "Sin marca" sí (M-7, D-18); "Sin categoría" no, porque se guardaría igual (M-70).
* **Acción de texto en una fila** (`GhostPrimaryButtonStyle`): ícono de 12 y texto carmín de 12, sin borde ni fondo, fondo carmín suave en hover. Para acciones secundarias que conviven con los controles de la fila (p. ej. "Restablecer" bajo el estado de una fila corregida). No reemplaza a `IconDangerButtonStyle` para eliminar.
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
| `TextSubtleBrush` | `#71717A` | Títulos e íconos de encabezado de tarjeta en formularios ("grabados"). Da 4,8:1 sobre blanco y cumple AA; `TextMutedBrush` (2,6:1) no alcanza para un título. |
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

### 6. Elevación
| Token XAML | Valor Hex | Uso Semántico |
| :--- | :--- | :--- |
| `ElevationShadowColor` | `#18181B` | `Color` (no pincel) para `DropShadowEffect` de superficies flotantes: desplegables y popups. Opacidad recomendada 0.18. |

### 7. Sidebar (Superficie Oscura)
| Token XAML | Valor Hex | Contraste | Uso Semántico |
| :--- | :--- | :--- | :--- |
| `SidebarBackgroundBrush` | `#18181B` | — | Fondo del menú lateral. |
| `SidebarBorderBrush` / `SidebarHoverBrush` | `#27272A` | — | Borde derecho, separadores y fondo de hover. |
| `SidebarPressedBrush` | `#3F3F46` | — | Fondo al presionar y pulgar del scroll. |
| `SidebarTextBrush` | `#D4D4D8` | 11,99:1 | Texto e íconos de los ítems en reposo. |
| `SidebarTextStrongBrush` | `#FAFAFA` | 16,97:1 | Texto en hover y nombre del operador. |
| `SidebarTextMutedBrush` | `#A1A1AA` | 6,91:1 | Títulos de sección. |
| `SidebarAccentBrush` | `#F36F8E` | 6,31:1 | Carmín claro para íconos o texto de acento. |
| `SidebarActiveBackgroundBrush` / `SidebarActiveForegroundBrush` | `#9D0F33` / `#FFFFFF` | 8,24:1 | Píldora del ítem activo. |

* **El carmín de marca `#9D0F33` solo da 2,15:1 sobre el fondo oscuro:** sirve de relleno (la píldora activa) pero jamás como color de texto o ícono; para eso existe `SidebarAccentBrush`.
* **Hover y pressed de `SidebarItemButtonStyle` viven en `Style.Triggers`**, no en el template, para que el trigger del ítem activo declarado en la vista prevalezca.
* **Botón hamburguesa:** anclado a la izquierda, sobre el eje de los íconos de los ítems (no se desplaza al animar el ancho). El ícono indica la acción: `RegularMenu` (expandir) cuando está colapsado y `RegularDockLeftArrow` (contraer) cuando está expandido; el ToolTip dice "Expandir menú lateral" o "Contraer menú lateral" más `Ctrl+B`.
* **Scroll con autoocultado (`SidebarScrollViewerStyle`):** la barra vertical flota sobre el contenido (no quita ancho a los ítems) y solo aparece mientras el mouse está sobre el menú.

### 8. Espaciado Estructural (Grilla de 4 px)
Definidos en [`Styles/Spacing.xaml`](../src/Retail.App/Styles/Spacing.xaml) como `StaticResource` (no cambian en ejecución). Regla de proximidad: lo relacionado va más junto que lo no relacionado.

| Token XAML | Valor | Uso Semántico |
| :--- | :---: | :--- |
| `PageMargin` | 20, arriba 12 | Margen del `Grid` raíz de **toda** página (`Views/Pages`), hasta el borde de la ventana y el Sidebar. Arriba es 12 porque la barra de título ya separa el contenido del borde de la ventana. |
| `SectionSpacing` | 16 (inferior) | Entre secciones apiladas: encabezado, filtros y cuerpo. |
| `CardSpacing` | 8 (inferior) | Entre tarjetas relacionadas de un mismo grupo (cliente y totales del POS). |
| `ColumnSpacing` / `ColumnGap` | 16 | Entre dos columnas de contenido: como margen derecho de la primera, o como columna espaciadora (`GridLength`). |

* **Alcance:** solo se tokeniza el espaciado entre bloques de una página. Los micro-márgenes internos de un control (4 a 8 px) se escriben a mano.
* **Excepción documentada:** el Dashboard conserva su separación propia de 12 entre bloques (más denso por las tarjetas KPI), pero usa `PageMargin`.
* **Auditoría:** `scripts/audit-xaml.ps1` rechaza una página cuyo `Grid` raíz declare un margen numérico literal.

---

## 📋 Páginas de Listado (Buscador + Filtros + Grilla)

Patrón de las páginas de catálogo y padrón (Artículos como piloto; Clientes, Proveedores, Usuarios, Importador y Pedidos adoptan el encabezado y la grilla). Los estilos viven en [`Styles/Listados.xaml`](../src/Retail.App/Styles/Listados.xaml) y los componentes en [`Views/Components/`](../src/Retail.App/Views/Components/).

```text
PageHeader (sin tarjeta)  ·················· título · acciones a la derecha
┌─ Tarjeta ──────────────────────────────────────────────┐
│ ListToolbar                                            │  búsqueda · limpiar filtros · N resultados
│              (con filas marcadas: búsqueda · [N seleccionados ✕] ···· acciones)
│ ┌ FilterBand ──────────────────────────────────────┐   │
│ │ Rubro   Categoría   Marca   Distribuidor   Stock │   │  etiqueta arriba, una sola fila
│ └──────────────────────────────────────────────────┘   │
│ DataGrid (DataGridListadoStyle)                        │  ocupa el alto disponible, hasta la paginación
│ PaginationBar                                          │
└────────────────────────────────────────────────────────┘
```

| Pieza | Qué es | Regla de uso |
| :--- | :--- | :--- |
| `PageHeader` | Control con contenido (`Titulo`, `Subtitulo`; el contenido son las acciones). Sin insignias junto al título | Reemplaza a la tarjeta de encabezado. Las páginas de listado **no llevan subtítulo** (el título alcanza y la grilla gana alto); sin subtítulo, su línea se colapsa y el título se centra con las acciones. Un control con contenido, y no un `UserControl` con una propiedad `object`, para que los botones hereden el `DataContext` de la página. Debajo lleva `Margin="{StaticResource SectionSpacing}"` |
| `ListToolbar` | Buscador de 390 × 40, "Limpiar filtros" (fantasma, se deshabilita sola), indicador de carga y "N resultados" | Se enlaza por propiedades de dependencia (`Texto`, `ComandoLimpiar`, `TotalResultados`, `EstaCargando`). Con `HaySeleccion`, su `AccionesDeSeleccion` (la `BulkActionBar` de la página) reemplaza a "Limpiar filtros", la carga y el contador; el buscador sigue a la vista. La barra hereda el `DataContext` de la página por el árbol visual |
| `BulkActionBar` | Acciones masivas **sin recuadro**: a la izquierda, el badge "N seleccionados ✕" (fondo `PrimaryLightBrush`, el rosa de las filas marcadas; texto `PrimaryBrush`; la "✕", con `BadgeCancelButtonStyle`, cancela la selección); a la derecha, las acciones como botones sueltos | Va **en la barra del buscador** (`ListToolbar.AccionesDeSeleccion`), en el lugar de "Limpiar filtros". La acción con lógica va con `KeycapButtonStyle` (el único acento carmín de la vista es "Nuevo …"); las demás, con `GhostNeutralButtonStyle`; "Más ▾" va último, porque su menú abre alineado a la derecha, con una flecha liviana dibujada (`Path` de 8 × 4, trazo 1,2) y los ítems en peso normal. No dice "de M": la selección es de la página visible (filtrar o paginar la descarta). No es un panel flotante porque taparía las últimas filas |
| `FilterBandStyle` + `FilterFieldStyle` + `ComboBoxFiltroStyle` | Banda hundida con los filtros en un `Grid` de columnas `*`, etiqueta de 12 px arriba y combos de 36 px | Una sola fila; los textos largos no se cortan porque el combo ocupa toda su columna |
| `DataGridListadoStyle` | Grilla de listado: encabezado en peso normal, fila de una línea de 44 px, selección en tinte carmín suave **sin borde carmín**, hover gris (los triggers de la fila están en `Style.Triggers` para que un estilo derivado los redefina: en una grilla **con casillas**, el tinte sigue a la casilla y un clic en una fila no cambia su aspecto: el gris es solo del hover). Ocupa **todo el alto** de su fila, así el borde llega hasta la paginación aunque haya pocas filas. **Esquinas redondeadas** (vienen de la plantilla de `DataGridRetailStyle`, así que valen para todas las grillas) | Encabezados numéricos: `DataGridColumnHeaderRightListadoStyle`. La grilla compacta (POS, modales) sigue siendo `DataGridRetailStyle` |
| `RowMenuButtonStyle` + `MenuDesplegableHelper` | Botón "⋮" de 32 px (ícono de 14), sin contorno, que abre un `ContextMenu` con las acciones de la fila | El menú se despliega **debajo del botón con los bordes derechos alineados** y un segundo clic lo cierra: se abre con `StaysOpen=true` y el helper lo cierra ante un clic ajeno, la rueda, perder el foco o mover la ventana. El `Click` decide por el estado del menú **al presionar** el botón, no al soltarlo: en una grilla el menú se cierra solo durante el clic (la celda toma el foco), y decidir al soltar lo reabría según la duración del clic. La columna no lleva título y mide **40 px**, con el botón de 32 pegado a la izquierda: la columna anterior (Stock, numérica) termina con 8 de relleno, así el "⋮" tiene 8 px de aire a cada lado. El doble clic en la fila abre la edición. El menú no está en el árbol visual de la página: sus comandos se enlazan por `PlacementTarget.Tag` (el ViewModel) y `PlacementTarget.DataContext` (la fila) |
| `CheckBoxCompactStyle` | Casilla de 16 × 16 para grillas (la de WPF-UI mide 20 y desproporciona la fila). Marcada o intermedia se rellena de carmín | Encabezado y celdas de selección de la grilla |
| `GhostNeutralButtonStyle` | Acción de texto gris sin borde | El carmín se reserva para la marca, la acción principal y la selección |

* **Importes y números:** celdas con `TextBlockCurrencyCell`, `TextBlockPriceCell` o `TextBlockNumericCell`, que fijan `Language="es-AR"`. Formato de moneda `{0:C2}` (da `$ 4.000,00`) y de porcentaje `{0:0.##}%`. Sin unidades en el título de columna.
* **Stock:** punto de color (verde normal) + cantidad; bajo el mínimo, el punto se reemplaza por "⚠" ámbar, para que el estado no dependa solo del color (el mínimo no se muestra en la grilla). Un servicio muestra "—" con tooltip. Alineado a la derecha, como toda columna numérica, con 8 de relleno derecho (ver el botón "⋮"), y de 64 px: lo justo para su título; el resto lo absorbe la descripción.
* **Distribuidor:** insignia neutra con ícono de enlace y, si hay fuentes adicionales, un chip "+n" **solo informativo** (no es un botón; el doble clic en la fila abre el artículo).
* **Nombres de artículo:** se muestran en mayúsculas con `AMayusculasConverter` (es-AR). Es solo presentación: el dato guardado no cambia.
* **Esquinas de las grillas:** `helpers:EsquinasHelper.Esquinas` (8 por defecto). Una grilla pegada a los bordes de su tarjeta (Compras) lo pone en `0`. La plantilla recorta el contenido con `RadioDelRecorte`, porque sin recorte el fondo del encabezado se sale de la esquina.
* **Anchos de columna:** las columnas de datos son fijas y se ajustan a su contenido; la principal (el nombre) es `*` y absorbe el resto.
* **Combo de filas por página:** el `ComboBox` de WPF-UI dibuja su borde visible según el texto más el relleno vertical (no `Height`) y reserva ~32 px para la flecha además del relleno derecho. En `PaginationBar` se resuelve con `Padding="10,5.7,0,5.7"` y `Width="64"`: igual de alto que los botones y sin recortar el número.
* **Paginación:** `PaginationBar` tiene `MostrarResumen` (por defecto `True`). Artículos lo pone en `False` y muestra en su lugar "N resultados": oculta el contador de la barra del buscador (`ListToolbar.MostrarContador="False"`) y enlaza un texto del pie a su `TextoContador`, a la izquierda y en línea con el paginador.
* **Acciones masivas sin lógica todavía:** se muestran **deshabilitadas con el tooltip** "Disponible próximamente": "Eliminar seleccionados" (`RegularTrash`) como botón a la vista, y Cambiar rubro (`RegularBookmarks`) y Cambiar categoría (`RegularTag`) en "Más ▾". No se cablean bucles sobre acciones de a una.

---

## 🔤 Jerarquía Tipográfica y Estilos de Texto

Definidos en [`src/Retail.App/Styles/Typography.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Styles/Typography.xaml):

| Estilo XAML | Fuente | Tamaño | Peso | Uso Principal |
| :--- | :--- | :--- | :--- | :--- |
| `TextBlockHeader1` | Segoe UI Variable | 22 px | SemiBold | Título de módulo o ventana principal. |
| `TextBlockHeader2` | Segoe UI Variable | 16 px | SemiBold | Títulos de secciones o tarjetas secundarias. |
| `TextBlockBody` | Segoe UI Variable | 14 px | Regular | Textos descriptivos, campos de formulario. |
| `TextBlockCaption` | Segoe UI Variable | 12 px | Regular | Notas al pie, marcas de versión, instrucciones. |
| `TextBlockDisplayCurrency` | Segoe UI Variable Display, tabular | 32 px | SemiBold | **Total gigante de la venta actual (mostrador).** |
| `TextBlockCurrencyLarge` | Segoe UI Variable Display, tabular | 24 px | SemiBold | Importes destacados en modales (saldo deudor, cobranzas). |
| `TextBlockCurrencyCell` | Segoe UI Variable Display, tabular | 14 px | SemiBold | Precios unitarios y subtotales en columnas de grillas. |
| `TextBlockNumericCell` | Segoe UI Variable Display, tabular | 13 px | SemiBold | Números de fila, cantidades y stock en grillas. |
| `TextBlockMonospaceCell` | Cascadia Code | 13 px | SemiBold | Códigos de barras, documentos e identificadores en grillas. |
| `TextBlockKeycap` | Cascadia Code | 11 px | Bold | Letras de teclas de atajo (`F1`, `F4`, `F12`, `ESC`). |

---

## 📐 Iconografía Oficial (MahApps.Metro.IconPacks.BoxIcons)

La aplicación estandariza su iconografía mediante el paquete modular **`MahApps.Metro.IconPacks.BoxIcons`**, complementado con geometrías nativas `StreamGeometry` en [`src/Retail.App/Styles/Icons.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Styles/Icons.xaml).

### 1. Espacio de Nombres en Vistas XAML
```xml
xmlns:iconPacks="http://metro.mahapps.com/winfx/xaml/iconpacks"
```

### 2. Convención Semántica de Glifos (Regular vs Solid)
* **`Solid...` (Relleno):** Utilizado para el botón principal de cobro, cabeceras de diálogo, badges semáforo y el avatar del operador en el Sidebar.
  * Alertas de Incidencia / Stock: `Kind="SolidAlertTriangle"`
  * Avatar del operador (Sidebar, glifo oscuro sobre círculo blanco): `Kind="SolidUser"`
* **`Regular...` (Línea):** Utilizado para los ítems de navegación del Sidebar, acciones secundarias, botones de grillas de datos y búsquedas. En el Sidebar todos los módulos usan la variante de línea, para que el ítem activo se distinga por la píldora carmín y no por el grosor del glifo.
  * Punto de Venta (POS): `Kind="RegularStoreAlt2"`
  * Catálogo de Artículos: `Kind="RegularBook"`
  * Clientes y Ctas. Ctes.: `Kind="RegularUser"`
  * Caja y Cobros: `Kind="RegularDollarCircle"`
  * Pedidos a Proveedores (y toda acción "Pedir"): `Kind="RegularCart"`
  * Compras a Distribuidores (el pedido ya recibido): `Kind="RegularPackage"` (encabezado de la página: `SolidPackage`)
  * Proveedores: `Kind="RegularTruck"`
  * Operadores y Roles: `Kind="RegularUserIdCard"`
  * Consola Fiscal ARCA: `Kind="RegularBadgeCheck"`
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
