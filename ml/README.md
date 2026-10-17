# ml/ — Exploración y entrenamiento del motor de catalogación (Track 6.6)

Carpeta **fuera de `Retail.sln`**. Contiene los scripts de análisis de datos que sustentan el diseño de
[`etapa-6-track-6.6-catalogacion-inteligente.md`](../docs/roadmap/etapa-6-track-6.6-catalogacion-inteligente.md).
El sistema en producción **no** usa Python: la clasificación corre en C# dentro del ERP.

## Estructura

| Ruta | Contenido | ¿Se versiona? |
| :--- | :--- | :---: |
| `scripts/` | Scripts de exploración. Solo biblioteca estándar de Python 3, sin paquetes que instalar. | Sí |
| `datos/` | Listas de distribuidores, conjuntos etiquetados y conjunto de oro. Son datos comerciales de terceros. | **No** (`.gitignore`) |

## Uso

```powershell
# 1. Exportar la lista .xls a CSV UTF-8 sin precios (abre el original en solo lectura con Excel)
powershell -ExecutionPolicy Bypass -File ml/scripts/exportar_lista_xls.ps1 `
    -Origen "C:\Users\lucas\Proyectos\LISTA MAYO 2025.xls" -Destino ml/datos/lista-mayo-2025.csv

# 2. Exploración
python ml/scripts/explorar_descripciones.py ml/datos/lista-mayo-2025.csv
python ml/scripts/analizar_ean.py ml/datos/lista-mayo-2025.csv
python ml/scripts/muestras.py ml/datos/lista-mayo-2025.csv palabra SET,REPUESTO
python ml/scripts/muestras.py ml/datos/lista-mayo-2025.csv prefijo 7792216
```

## Scripts de arranque (Track 6.6, sección 6.1)

Producen planillas **para completar a mano**. Todos escriben en `ml/datos/` (no versionado), con `;` como separador y
UTF-8 **con BOM** (Excel respeta tildes). Si un script posterior las relee, abrirlas con `encoding="utf-8-sig"`.

```powershell
python ml/scripts/listar_terminos.py ml/datos/lista-mayo-2025.csv [--top 300]   # --top 0 = todos
python ml/scripts/candidatos_marcas.py ml/datos/lista-mayo-2025.csv [--min-frecuencia 10] [--top 500] `
    [--largo-prefijo 7] [--min-soporte 3] [--min-pureza 0.5]
```

| Script | Salida | Columnas a completar |
| :--- | :--- | :--- |
| `listar_terminos.py` | `terminos-candidatos.csv`: `termino;frecuencia;ejemplo1;ejemplo2;ejemplo3;rubro;categoria;ambiguo`, ordenado por frecuencia. Término = primera palabra de la descripción limpia. | `rubro`, `categoria`, `ambiguo` (poner `1` en `SET`, `REPUESTO`, etc.). Guardar como `terminos.csv`. |
| `candidatos_marcas.py` | `marcas-candidatas.csv`: `marca_candidata;frecuencia;variantes_detectadas;ejemplo;aprobada;alias` | `aprobada` (`1`/vacío) y `alias` (variantes que se aceptan, separadas por `|`). Guardar como `marcas.csv` (`marca;alias`) con las aprobadas. |
| `candidatos_marcas.py` | `prefijos-candidatos.csv`: `prefijo;marca_candidata;soporte;pureza;aprobado` | `aprobado` (`1`/vacío). `pureza` = fracción de los artículos del prefijo que contienen la marca candidata. Guardar como `prefijos.csv` (`prefijo;marca`). |

La consola solo muestra conteos; las descripciones de ejemplo están únicamente en los archivos de `datos/`.

> **Abrir los prefijos en Excel como texto.** Un doble clic convierte `0012345` en `12345` (se pierden ceros
> iniciales). Usar *Datos > Obtener datos > Desde texto/CSV*, y en la vista previa fijar la columna `prefijo` como
> **Texto** antes de cargar. Al guardar, Excel conserva el texto solo si la columna quedó con formato Texto.

> **No guardar la lista como CSV desde Excel.** Excel convierte los códigos de barras a notación científica
> (`3,50118E+12`) y se pierden dígitos de forma irrecuperable. Por eso existe `exportar_lista_xls.ps1`.
