# Auditoria Estatica de Recursos XAML (Retail POS)
# -Detalle: ademas del resumen de valores fuera de la grilla de 4 px, lista cada ocurrencia (archivo:linea).
param(
    [switch]$Detalle
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$appDir = Join-Path $repoRoot "src\Retail.App"
$stylesDir = Join-Path $appDir "Styles"

if (-not (Test-Path $stylesDir)) {
    Write-Error "No se encontro el directorio de estilos en: $stylesDir"
    exit 1
}

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " RETAIL POS - AUDITORIA ESTATICA DE RECURSOS XAML" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

# 1. Recolectar claves definidas en Styles/
$definedKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)

Get-ChildItem -Path $stylesDir -Filter "*.xaml" | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    $matches = [regex]::Matches($content, 'x:Key="([^"]+)"')
    foreach ($m in $matches) {
        [void]$definedKeys.Add($m.Groups[1].Value)
    }
}

Write-Host "Claves semanticas registradas en Styles: $($definedKeys.Count)" -ForegroundColor Gray

# 2. Escanear archivos XAML de Retail.App (excluyendo obj/ y bin/)
$xamlFiles = Get-ChildItem -Path $appDir -Filter "*.xaml" -Recurse | Where-Object {
    $_.FullName -notmatch "\\obj\\" -and $_.FullName -notmatch "\\bin\\"
}

$unresolvedStatic = @{}
$unresolvedDynamic = @{}
$localThemeMerges = [System.Collections.Generic.List[string]]::new()
$headersSinAlinear = [System.Collections.Generic.List[string]]::new()
$margenesLiterales = [System.Collections.Generic.List[string]]::new()

foreach ($file in $xamlFiles) {
    $content = Get-Content $file.FullName -Raw

    # El encabezado sigue la alineacion de su celda: una columna de texto con celda a la derecha declara su HeaderStyle
    foreach ($columna in [regex]::Matches($content, '<DataGridTextColumn\b[^>]*>')) {
        $etiqueta = $columna.Value
        if ($etiqueta -match 'ElementStyle="\{StaticResource (TextBlockCurrencyCell|CeldaNumericaDerecha|CeldaMonedaChica)\}"' -and
            $etiqueta -notmatch '\bHeaderStyle=') {
            $titulo = if ($etiqueta -match 'Header="([^"]*)"') { $Matches[1] } else { '(sin encabezado)' }
            $headersSinAlinear.Add("$($file.Name): $titulo")
        }
    }

    # El margen de pagina es un token (PageMargin): cambiarlo en Spacing.xaml lo cambia en todas las vistas
    if ($file.Name -like '*View.xaml' -and $file.DirectoryName -match '\\Views\\Pages$' -and
        $content -match '(?m)^    <Grid Margin="[0-9, ]+"') {
        $margenesLiterales.Add($file.Name)
    }

    # Los diccionarios de WPF-UI se cargan solo en App.xaml: una copia por ventana tapa el acento de la aplicacion
    if ($file.Name -ne "App.xaml" -and $content -match '<ui:(ThemesDictionary|ControlsDictionary)\b') {
        $localThemeMerges.Add($file.Name)
    }

    $localKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $locMatches = [regex]::Matches($content, 'x:Key="([^"]+)"')
    foreach ($lm in $locMatches) {
        [void]$localKeys.Add($lm.Groups[1].Value)
    }

    # StaticResources
    $staticMatches = [regex]::Matches($content, 'StaticResource\s+([a-zA-Z0-9_]+)')
    foreach ($sm in $staticMatches) {
        $key = $sm.Groups[1].Value
        if (-not $definedKeys.Contains($key) -and -not $localKeys.Contains($key)) {
            if (-not $unresolvedStatic.ContainsKey($key)) {
                $unresolvedStatic[$key] = [System.Collections.Generic.List[string]]::new()
            }
            if (-not $unresolvedStatic[$key].Contains($file.Name)) {
                $unresolvedStatic[$key].Add($file.Name)
            }
        }
    }

    # DynamicResources (excluyendo recursos nativos de WPF-UI)
    $dynamicMatches = [regex]::Matches($content, 'DynamicResource\s+([a-zA-Z0-9_]+)')
    foreach ($dm in $dynamicMatches) {
        $key = $dm.Groups[1].Value
        $isWpfUiResource = $key -match '^(TextFillColor|ControlFillColor|CardBackground|Accent|SystemControl|ApplicationPageBackground|ControlStrokeColor)'
        if (-not $definedKeys.Contains($key) -and -not $localKeys.Contains($key) -and -not $isWpfUiResource) {
            if (-not $unresolvedDynamic.ContainsKey($key)) {
                $unresolvedDynamic[$key] = [System.Collections.Generic.List[string]]::new()
            }
            if (-not $unresolvedDynamic[$key].Contains($file.Name)) {
                $unresolvedDynamic[$key].Add($file.Name)
            }
        }
    }
}

$hasErrors = $false

Write-Host "`n--- REVISION DE STATIC RESOURCES ---" -ForegroundColor Yellow
if ($unresolvedStatic.Count -eq 0) {
    Write-Host "[OK] Todos los StaticResources estan declarados y resueltos." -ForegroundColor Green
}
else {
    $hasErrors = $true
    Write-Host "[ERROR] Se detectaron StaticResources NO resueltos:" -ForegroundColor Red
    foreach ($entry in $unresolvedStatic.GetEnumerator()) {
        Write-Host "  * $($entry.Key)  -->  en archivo(s): $($entry.Value -join ', ')" -ForegroundColor Red
    }
}

Write-Host "`n--- REVISION DE DYNAMIC RESOURCES (CUSTOM) ---" -ForegroundColor Yellow
if ($unresolvedDynamic.Count -eq 0) {
    Write-Host "[OK] Todos los DynamicResources personalizados estan declarados y resueltos." -ForegroundColor Green
}
else {
    $hasErrors = $true
    Write-Host "[ERROR] Se detectaron DynamicResources NO resueltos:" -ForegroundColor Red
    foreach ($entry in $unresolvedDynamic.GetEnumerator()) {
        Write-Host "  * $($entry.Key)  -->  en archivo(s): $($entry.Value -join ', ')" -ForegroundColor Red
    }
}

Write-Host "`n--- REVISION DE DICCIONARIOS DE TEMA LOCALES ---" -ForegroundColor Yellow
if ($localThemeMerges.Count -eq 0) {
    Write-Host "[OK] Solo App.xaml carga ui:ThemesDictionary y ui:ControlsDictionary." -ForegroundColor Green
}
else {
    $hasErrors = $true
    Write-Host "[ERROR] Ventanas o vistas que vuelven a mergear los diccionarios de WPF-UI (heredan los de App.xaml):" -ForegroundColor Red
    foreach ($name in $localThemeMerges) {
        Write-Host "  * $name" -ForegroundColor Red
    }
}

Write-Host "`n--- REVISION DE ENCABEZADOS DE COLUMNAS A LA DERECHA ---" -ForegroundColor Yellow
if ($headersSinAlinear.Count -eq 0) {
    Write-Host "[OK] Todo encabezado sobre una celda a la derecha declara HeaderStyle." -ForegroundColor Green
}
else {
    $hasErrors = $true
    Write-Host "[ERROR] Columnas con celda a la derecha y encabezado a la izquierda (use DataGridColumnHeaderRightStyle):" -ForegroundColor Red
    foreach ($item in $headersSinAlinear) {
        Write-Host "  * $item" -ForegroundColor Red
    }
}

Write-Host "`n--- REVISION DEL MARGEN DE PAGINA ---" -ForegroundColor Yellow
if ($margenesLiterales.Count -eq 0) {
    Write-Host "[OK] Toda pagina usa {StaticResource PageMargin} en su Grid raiz." -ForegroundColor Green
}
else {
    $hasErrors = $true
    Write-Host "[ERROR] Paginas con margen raiz literal (use {StaticResource PageMargin}):" -ForegroundColor Red
    foreach ($name in $margenesLiterales) {
        Write-Host "  * $name" -ForegroundColor Red
    }
}

Write-Host "`n--- REVISION DE LA DESCRIPCION DE CADA PAGINA (R11) ---" -ForegroundColor Yellow
# Toda pagina con PageHeader lleva su descripcion (el icono de informacion con tooltip): sin ella la pagina queda sin ayuda.
$paginasSinDescripcion = @(Get-ChildItem -Path (Join-Path $appDir "Views") -Recurse -Filter "*.xaml" | Where-Object {
    $_.FullName -notmatch "\\Views\\Dev\\"
} | Where-Object {
    $texto = Get-Content $_.FullName -Raw
    $texto -match "<components:PageHeader" -and $texto -notmatch "Descripcion"
} | ForEach-Object { $_.Name })
if ($paginasSinDescripcion.Count -eq 0) {
    Write-Host "[OK] Toda pagina con PageHeader declara su Descripcion." -ForegroundColor Green
}
else {
    $hasErrors = $true
    Write-Host "[ERROR] Paginas con PageHeader sin Descripcion (agregue Descripcion=""Pagina . lo que muestra""):" -ForegroundColor Red
    foreach ($name in $paginasSinDescripcion) {
        Write-Host "  * $name" -ForegroundColor Red
    }
}

Write-Host "`n--- REVISION DE GRAFICOS Y GRUPOS DE OPCIONES (R13, R14) ---" -ForegroundColor Yellow
# R13: el control de gráficos de LiveCharts pinta blanco sobre el tema oscuro si no se le fija un Background.
# R14: un RadioButton con GroupName registra su grupo en toda la ventana. Las páginas se recrean en cada navegación y las
#      anteriores siguen registradas hasta que las recoge el recolector: al marcar una opción en la vista nueva, la vieja
#      recibe el desmarcado y su ViewModel actúa (en Ajustes volvía al tema claro). Los RadioButton hermanos de un mismo
#      panel ya forman un grupo, sin GroupName.
$graficosSinFondo = [System.Collections.Generic.List[string]]::new()
$gruposEnPaginas = [System.Collections.Generic.List[string]]::new()
foreach ($file in (Get-ChildItem -Path (Join-Path $appDir "Views") -Recurse -Filter "*.xaml")) {
    $texto = Get-Content $file.FullName -Raw
    foreach ($m in [regex]::Matches($texto, '<lvc:CartesianChart[^>]*>')) {
        if ($m.Value -notmatch 'Background=') {
            $graficosSinFondo.Add($file.Name)
        }
    }
    if ($file.FullName -match "\\Views\\Pages\\" -and $texto -match 'GroupName=') {
        $gruposEnPaginas.Add($file.Name)
    }
}

if ($graficosSinFondo.Count -eq 0) {
    Write-Host "[OK] Todo grafico declara su Background (Transparent)." -ForegroundColor Green
}
else {
    $hasErrors = $true
    Write-Host "[ERROR] Graficos sin Background (pintan blanco en el tema oscuro; use Background=""Transparent""):" -ForegroundColor Red
    foreach ($name in ($graficosSinFondo | Select-Object -Unique)) {
        Write-Host "  * $name" -ForegroundColor Red
    }
}

if ($gruposEnPaginas.Count -eq 0) {
    Write-Host "[OK] Ninguna pagina usa GroupName en sus RadioButton." -ForegroundColor Green
}
else {
    $hasErrors = $true
    Write-Host "[ERROR] Paginas con RadioButton GroupName (el grupo cruza las vistas; deje que el panel los agrupe):" -ForegroundColor Red
    foreach ($name in ($gruposEnPaginas | Select-Object -Unique)) {
        Write-Host "  * $name" -ForegroundColor Red
    }
}

Write-Host "`n--- REVISION DE BINDINGS A PROPIEDADES ADJUNTAS EN ESTILOS (R15) ---" -ForegroundColor Yellow
# R15: un {Binding (prefijo:Tipo.Propiedad)} dentro de una plantilla de Styles/ puede fallar con "property not found" al
#      instanciarla (la ruta con prefijo no se resolvia) y el rasgo visual desaparece sin avisar. Se usa TemplateBinding.
$bindingsAdjuntos = [System.Collections.Generic.List[string]]::new()
foreach ($file in (Get-ChildItem -Path $stylesDir -Filter "*.xaml")) {
    $texto = Get-Content $file.FullName -Raw
    foreach ($m in [regex]::Matches($texto, '\{Binding\s+\([A-Za-z]+:[A-Za-z]+\.[A-Za-z]+\)')) {
        $linea = ($texto.Substring(0, $m.Index) -split "`n").Count
        $bindingsAdjuntos.Add("$($file.Name):$linea  $($m.Value)")
    }
}

if ($bindingsAdjuntos.Count -eq 0) {
    Write-Host "[OK] Ningun estilo enlaza una propiedad adjunta con ruta de prefijo (se usa TemplateBinding)." -ForegroundColor Green
}
else {
    $hasErrors = $true
    Write-Host "[ERROR] Bindings a propiedades adjuntas por ruta en Styles/ (use TemplateBinding):" -ForegroundColor Red
    foreach ($item in $bindingsAdjuntos) {
        Write-Host "  * $item" -ForegroundColor Red
    }
}

Write-Host "`n--- REVISION DE COLORES ESTATICOS (TEMA CLARO/OSCURO) ---" -ForegroundColor Yellow
# Los pinceles de la paleta llegan congelados: un {StaticResource} de color conserva el pincel viejo y no cambia al
# alternar el tema. Todo color se toma con {DynamicResource}. Los pinceles son los de Colors.xaml (terminan en "Brush").
$pinceles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$contenidoPaleta = Get-Content (Join-Path $stylesDir "Colors.xaml") -Raw
foreach ($m in [regex]::Matches($contenidoPaleta, '<SolidColorBrush\s+x:Key="([^"]+)"')) {
    [void]$pinceles.Add($m.Groups[1].Value)
}
$coloresEstaticos = [System.Collections.Generic.List[string]]::new()
foreach ($archivo in $xamlFiles) {
    $texto = Get-Content $archivo.FullName -Raw
    foreach ($m in [regex]::Matches($texto, '\{StaticResource\s+(\w+)\}')) {
        if ($pinceles.Contains($m.Groups[1].Value)) {
            [void]$coloresEstaticos.Add("$($archivo.Name): $($m.Groups[1].Value)")
        }
    }
}
if ($coloresEstaticos.Count -eq 0) {
    Write-Host "[OK] Ningun color de la paleta se toma con StaticResource." -ForegroundColor Green
}
else {
    $hasErrors = $true
    Write-Host "[ERROR] Colores con StaticResource (use DynamicResource para que cambien con el tema):" -ForegroundColor Red
    foreach ($item in ($coloresEstaticos | Sort-Object -Unique)) {
        Write-Host "  * $item" -ForegroundColor Red
    }
}

# Aviso (no falla la auditoria): valores literales de Margin, Padding y CornerRadius fuera de la grilla de 4 px.
# Se ignoran 0, 1 y 2 (lineas finas y riel) y los negativos. Styles/ y Views/Dev/ quedan afuera: ahi se definen los tokens.
$fueraDeGrilla = [System.Collections.Generic.List[object]]::new()
$archivosVista = $xamlFiles | Where-Object {
    $_.FullName -notmatch "\\Styles\\" -and $_.FullName -notmatch "\\Views\\Dev\\"
}

foreach ($file in $archivosVista) {
    $texto = Get-Content $file.FullName -Raw
    $literales = [regex]::Matches($texto, '(?<![\w.])(Margin|Padding|CornerRadius)="([^"{}]*)"')
    foreach ($literal in $literales) {
        $numeros = [regex]::Matches($literal.Groups[2].Value, '(?<![\d.-])\d+(?:\.\d+)?')
        $invalidos = @($numeros | Where-Object {
            $n = [double]::Parse($_.Value, [System.Globalization.CultureInfo]::InvariantCulture)
            $n -notin 0, 1, 2 -and ($n % 4) -ne 0
        })
        if ($invalidos.Count -gt 0) {
            $linea = ($texto.Substring(0, $literal.Index) -split "`n").Count
            $fueraDeGrilla.Add([pscustomobject]@{
                Archivo = $file.Name
                Linea   = $linea
                Texto   = $literal.Value
            })
        }
    }
}

Write-Host "`n--- AVISO: VALORES FUERA DE LA GRILLA DE 4 PX (no afecta el resultado) ---" -ForegroundColor Yellow
if ($fueraDeGrilla.Count -eq 0) {
    Write-Host "[OK] Ningun Margin, Padding o CornerRadius literal se sale de la grilla." -ForegroundColor Green
}
else {
    Write-Host "[AVISO] Total de ocurrencias fuera de la grilla: $($fueraDeGrilla.Count) en $(@($fueraDeGrilla | Select-Object -ExpandProperty Archivo -Unique).Count) archivo(s). Top 10:" -ForegroundColor DarkYellow
    $fueraDeGrilla | Group-Object Archivo | Sort-Object Count -Descending | Select-Object -First 10 | ForEach-Object {
        Write-Host ("  {0,4}  {1}" -f $_.Count, $_.Name) -ForegroundColor DarkYellow
    }
    if ($Detalle) {
        Write-Host "Detalle (archivo:linea  valor):" -ForegroundColor DarkYellow
        foreach ($item in ($fueraDeGrilla | Sort-Object Archivo, Linea)) {
            Write-Host "  $($item.Archivo):$($item.Linea)  $($item.Texto)" -ForegroundColor Gray
        }
    }
    else {
        Write-Host "  (use -Detalle para listar cada ocurrencia)" -ForegroundColor Gray
    }
}

Write-Host "`n============================================================" -ForegroundColor Cyan
if ($hasErrors) {
    Write-Host " FALLO LA AUDITORIA XAML. Corrija los nombres o declare los estilos faltantes en Styles/." -ForegroundColor Red
    Write-Host "============================================================" -ForegroundColor Cyan
    exit 1
}
else {
    Write-Host " AUDITORIA XAML EXITOSA: 100% de coherencia en recursos y diccionarios." -ForegroundColor Green
    Write-Host "============================================================" -ForegroundColor Cyan
    exit 0
}
