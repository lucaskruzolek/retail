<#
.SYNOPSIS
    Exporta una lista de distribuidor en .xls a CSV UTF-8 sin las columnas de precio (Track 6.6).

.DESCRIPTION
    Usa Excel por automatización COM y abre el libro en modo solo lectura: el original no se modifica.
    Los números se escriben con todos sus dígitos, para que los códigos de barras no queden en notación
    científica como ocurre al guardar como CSV desde Excel. Las columnas cuyo encabezado contiene "precio"
    no se exportan.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File ml/scripts/exportar_lista_xls.ps1 `
        -Origen "C:\Users\lucas\Proyectos\LISTA MAYO 2025.xls" -Destino ml/datos/lista-mayo-2025.csv
#>
param(
    [Parameter(Mandatory = $true)][string]$Origen,
    [Parameter(Mandatory = $true)][string]$Destino
)

$rutaOrigen = (Resolve-Path $Origen).Path
$rutaDestino = [System.IO.Path]::GetFullPath($Destino)

$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
try
{
    $libro = $excel.Workbooks.Open($rutaOrigen, 0, $true)
    $hoja = $libro.Worksheets.Item(1)
    $datos = $hoja.UsedRange.Value2
    $filas = $datos.GetLength(0)
    $columnas = $datos.GetLength(1)

    $encabezados = for ($c = 1; $c -le $columnas; $c++) { "$($datos[1, $c])" }
    $indices = @(for ($c = 1; $c -le $columnas; $c++) { if ($encabezados[$c - 1] -notmatch "(?i)precio") { $c } })
    "Filas: $filas | columnas exportadas: " + (($indices | ForEach-Object { $encabezados[$_ - 1] }) -join " | ")

    $sb = New-Object System.Text.StringBuilder
    for ($f = 1; $f -le $filas; $f++)
    {
        $celdas = foreach ($c in $indices)
        {
            $valor = $datos[$f, $c]
            if ($valor -is [double])
            {
                $texto = $valor.ToString("0", [System.Globalization.CultureInfo]::InvariantCulture)
            }
            else
            {
                $texto = "$valor"
            }
            if ($texto -match '[;"]')
            {
                $texto = '"' + $texto.Replace('"', '""') + '"'
            }
            $texto
        }
        [void]$sb.AppendLine($celdas -join ";")
    }

    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($rutaDestino)) | Out-Null
    [System.IO.File]::WriteAllText($rutaDestino, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
    "Exportado a: $rutaDestino"
    $libro.Close($false)
}
finally
{
    $excel.Quit()
    [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel)
}
