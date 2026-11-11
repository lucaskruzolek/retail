<#
.SYNOPSIS
    Verifica los enlaces de la portada (wiki/index.html) y de los recorridos HTML de la wiki.

.DESCRIPTION
    Un HTML no avisa cuando un enlace se rompe: este script lo comprueba antes de entregar un cambio.

      1. Enlaces relativos (href y src): el archivo tiene que existir y, si el enlace lleva #ancla hacia un HTML, el
         id tiene que estar en ese HTML.
      2. Anclas dentro del mismo documento (#nivel-2): el id tiene que existir.
      3. Enlaces a este repositorio en GitHub (github.com/lucaskruzolek/retail/blob/main/... o /tree/main/...): la ruta
         tiene que existir en el árbol de trabajo. Se compara contra los archivos locales, sin conexión, así que vale
         para la rama que se está por mergear a main.
      4. wiki/recursos/recorridos.js: cada recorrido "escrito" tiene que existir, y uno "pendiente" no debería existir
         todavía (si existe, falta pasarlo a "escrito").

    No verifica los sitios externos (CDN, perfiles de GitHub): dependen de la red. Tampoco las anclas que arma
    comun.js al cargar (los títulos h3), porque no están en el HTML.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts/verificar-enlaces-wiki.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$raizRepo = Split-Path $PSScriptRoot -Parent
$wiki = Join-Path $raizRepo "wiki"
$prefijoRepo = "https://github.com/lucaskruzolek/retail/"
$errores = New-Object System.Collections.Generic.List[string]
$externos = 0
$verificados = 0

function Get-Ids([string]$rutaHtml)
{
    $contenido = [IO.File]::ReadAllText($rutaHtml)
    return @([regex]::Matches($contenido, '\sid="([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
}

function Get-RelativaAlRepo([string]$ruta)
{
    return $ruta.Substring($raizRepo.Length + 1).Replace('\', '/')
}

$documentos = @(Get-Item (Join-Path $wiki "index.html")) + @(Get-ChildItem $wiki -Recurse -Filter *.html | Where-Object { $_.Name -ne "index.html" -or $_.DirectoryName -ne $wiki })

foreach ($documento in $documentos)
{
    $contenido = [IO.File]::ReadAllText($documento.FullName)
    $idsPropios = Get-Ids $documento.FullName
    $nombre = Get-RelativaAlRepo $documento.FullName

    foreach ($coincidencia in [regex]::Matches($contenido, '\s(?:href|src)="([^"]+)"'))
    {
        $enlace = [Net.WebUtility]::HtmlDecode($coincidencia.Groups[1].Value)

        if ($enlace.StartsWith("#"))
        {
            $verificados++
            if ($idsPropios -notcontains $enlace.Substring(1))
            {
                $errores.Add("${nombre}: el ancla '$enlace' no existe en el documento.")
            }
            continue
        }

        if ($enlace.StartsWith($prefijoRepo))
        {
            $resto = $enlace.Substring($prefijoRepo.Length) -replace '#.*$', ''
            if ($resto -match '^(?:blob|tree)/main/(.+)$')
            {
                $verificados++
                $rutaLocal = Join-Path $raizRepo ([Uri]::UnescapeDataString($Matches[1]).Replace('/', '\'))
                if (-not (Test-Path -LiteralPath $rutaLocal))
                {
                    $errores.Add("${nombre}: '$enlace' apunta a un archivo que no está en el repositorio.")
                }
            }
            else
            {
                $externos++
            }
            continue
        }

        if ($enlace -match '^[a-z]+:')
        {
            $externos++
            continue
        }

        $verificados++
        $partes = $enlace.Split('#', 2)
        $destino = Join-Path $documento.DirectoryName ([Uri]::UnescapeDataString($partes[0]).Replace('/', '\'))
        if (-not (Test-Path -LiteralPath $destino))
        {
            $errores.Add("${nombre}: '$enlace' apunta a un archivo que no existe.")
            continue
        }
        if ($partes.Count -eq 2 -and $destino.EndsWith(".html") -and (Get-Ids $destino) -notcontains $partes[1])
        {
            $errores.Add("${nombre}: el ancla '#$($partes[1])' no existe en '$($partes[0])'.")
        }
    }
}

# Lista de recorridos: lo escrito existe y lo pendiente todavía no.
$lista = [IO.File]::ReadAllText((Join-Path $wiki "recursos\recorridos.js"))
$entradas = [regex]::Matches($lista, 'archivo:\s*"([^"]+)"[\s\S]*?estado:\s*"([^"]+)"')
foreach ($entrada in $entradas)
{
    $verificados++
    $archivo = $entrada.Groups[1].Value
    $estado = $entrada.Groups[2].Value
    $existe = Test-Path -LiteralPath (Join-Path $wiki $archivo.Replace('/', '\'))
    if ($estado -eq "escrito" -and -not $existe)
    {
        $errores.Add("recorridos.js: '$archivo' figura como escrito pero el archivo no existe.")
    }
    elseif ($estado -eq "pendiente" -and $existe)
    {
        $errores.Add("recorridos.js: '$archivo' existe pero figura como pendiente: pasarlo a ""escrito"".")
    }
    elseif ($estado -notin @("escrito", "pendiente"))
    {
        $errores.Add("recorridos.js: '$archivo' tiene un estado desconocido ('$estado').")
    }
}

Write-Host "Documentos revisados: $($documentos.Count). Enlaces verificados: $verificados. Externos sin verificar: $externos. Recorridos en la lista: $($entradas.Count)."
if ($errores.Count -gt 0)
{
    $errores | ForEach-Object { Write-Host "  ERROR $_" -ForegroundColor Red }
    Write-Host "$($errores.Count) enlace(s) roto(s)." -ForegroundColor Red
    exit 1
}
Write-Host "Todos los enlaces locales y del repositorio existen." -ForegroundColor Green
