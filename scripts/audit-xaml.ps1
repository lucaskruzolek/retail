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
