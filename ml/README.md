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

> **No guardar la lista como CSV desde Excel.** Excel convierte los códigos de barras a notación científica
> (`3,50118E+12`) y se pierden dígitos de forma irrecuperable. Por eso existe `exportar_lista_xls.ps1`.
