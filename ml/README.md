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
UTF-8 **con BOM** (Excel respeta tildes). Para releer una planilla completada a mano usar `leer_planilla` de
`comun.py`, que acepta también el ANSI (cp1252) que deja Excel al guardar.

```powershell
python ml/scripts/listar_terminos.py ml/datos/lista-mayo-2025.csv [--top 300]   # --top 0 = todos
python ml/scripts/candidatos_marcas.py ml/datos/lista-mayo-2025.csv [--min-frecuencia 10] [--top 500] `
    [--largo-prefijo 7] [--min-soporte 3] [--min-pureza 0.5]
# Segunda pasada, después de revisar marcas y prefijos:
python ml/scripts/prefijos_por_marca.py ml/datos/lista-mayo-2025.csv [--salida ml/datos/prefijos-revision.csv]
# Cierre: valida todas las planillas y escribe los diccionarios definitivos (no escribe nada si hay errores)
python ml/scripts/consolidar_diccionarios.py ml/datos/lista-mayo-2025.csv [--destino ml/datos]
# Muestras para etiquetar: términos ambiguos (entrenamiento) y conjunto de oro (evaluación final)
python ml/scripts/muestrear_etiquetado.py ml/datos/lista-mayo-2025.csv [--por-termino 25] [--oro 400] [--semilla 2026]
# Cierre del etiquetado: valida oro-validar.csv y las planillas de ambiguos y escribe oro.csv y ambiguos.csv
python ml/scripts/cerrar_etiquetado.py
```

Desde M-20, `oro.csv` es el **conjunto de desarrollo**. El oro de prueba para la evaluación final se muestreó aparte
y no se abre hasta la Fase 7. Comandos usados el 2026-10-22 (las exclusiones garantizan familias disjuntas):

```powershell
# Oro de prueba (primero, para que ninguna decisión posterior lo condicione)
python ml/scripts/muestrear_etiquetado.py ml/datos/lista-mayo-2025.csv --por-termino 0 --oro 400 --semilla 2027 `
    --excluir ml/datos/oro.csv ml/datos/ambiguos.csv --salida-oro ml/datos/oro-prueba-a-etiquetar.csv
# Ejemplos de los términos que pasaron a ambiguos con M-20
python ml/scripts/muestrear_etiquetado.py ml/datos/lista-mayo-2025.csv --oro 0 --solo GOMA,CINTA,LIBRO,BASE,TACO,VASO,TINTA `
    --semilla 2028 --excluir ml/datos/oro.csv ml/datos/ambiguos.csv ml/datos/oro-prueba-a-etiquetar.csv `
    --salida-ambiguos ml/datos/ambiguos-2-a-etiquetar.csv
```

`leer_planilla` acepta planillas separadas por `;` o por tabulaciones (Excel, "Texto delimitado por tabulaciones").

`muestrear_etiquetado.py` toma una fila por familia (tres primeras palabras) y ninguna familia aparece en las dos
muestras. El oro se etiqueta **a ciegas** (sin sugerencias) y se usa solo para la evaluación final. No sobrescribe
planillas existentes salvo con `--sobrescribir`.

| Script | Salida | Columnas a completar |
| :--- | :--- | :--- |
| `listar_terminos.py` | `terminos-candidatos.csv`: `termino;frecuencia;ejemplo1;ejemplo2;ejemplo3;rubro;categoria;ambiguo`, ordenado por frecuencia. Término = primera palabra de la descripción limpia. | `rubro`, `categoria`, `ambiguo` (poner `1` en `SET`, `REPUESTO`, etc.). Guardar como `terminos.csv`. |
| `candidatos_marcas.py` | `marcas-candidatas.csv`: `marca_candidata;frecuencia;variantes_detectadas;ejemplo;aprobada;alias` | `aprobada` (vacío = sí, `0` = no; convención usada en la revisión de 2026-10-21) y `alias` (formas que aparecen en el texto, separadas por `|`). Se puede reescribir `marca_candidata` con el nombre verdadero de la marca. |
| `candidatos_marcas.py` | `prefijos-candidatos.csv`: `prefijo;marca_candidata;soporte;pureza;aprobado` | `aprobado` (vacío = sí, `0` = no). `pureza` = fracción de los artículos del prefijo que contienen la marca candidata. |
| `prefijos_por_marca.py` | `prefijos-revision.csv`: `prefijo;marca;soporte;pureza;aprobado;origen`. Recalcula cada prefijo contando **solo marcas aprobadas** (una palabra genérica ya no tapa a la marca) y arrastra la revisión anterior. | `aprobado` en las filas con `?` (`1` = sí, `0` = no). `origen`: `conservado`, `reasignado` o `nuevo`. |

`consolidar_diccionarios.py` produce los cuatro diccionarios que cargará `Retail.Cli` (Track 6.6, M-15):
`taxonomia.csv` (`rubro;categoria`), `terminos.csv` (`termino;rubro;categoria;ambiguo`), `marcas.csv` (`marca;alias`,
alias separados por `|`) y `prefijos.csv` (`prefijo;marca`). Valida las reglas de la base (nombres obligatorios, de
hasta 100 caracteres, únicos sin distinguir mayúsculas ni acentos) y las referencias cruzadas, e informa la cobertura
de cada diccionario sobre la lista.

Los empates de frecuencia y pureza se desempatan alfabéticamente: dos corridas sobre la misma lista producen el mismo
archivo.

La consola solo muestra conteos; las descripciones de ejemplo están únicamente en los archivos de `datos/`.

> **Abrir los prefijos en Excel como texto.** Un doble clic convierte `0012345` en `12345` (se pierden ceros
> iniciales). Usar *Datos > Obtener datos > Desde texto/CSV*, y en la vista previa fijar la columna `prefijo` como
> **Texto** antes de cargar. Al guardar, Excel conserva el texto solo si la columna quedó con formato Texto.

> **No guardar la lista como CSV desde Excel.** Excel convierte los códigos de barras a notación científica
> (`3,50118E+12`) y se pierden dígitos de forma irrecuperable. Por eso existe `exportar_lista_xls.ps1`.

## Experimentos del modelo (Track 6.6, Fase 2)

Usan **scikit-learn** en un entorno virtual propio, aprobado solo para exploración: no llega al producto, que entrena
en C# sin dependencias nuevas (M-3). Las versiones están fijadas en `requirements.txt`.

```powershell
python -m venv ml/.venv
ml/.venv/Scripts/python -m pip install -r ml/requirements.txt
ml/.venv/Scripts/python ml/scripts/e1_e2_clasificador.py e2      # E-2: vectorizador
ml/.venv/Scripts/python ml/scripts/e1_e2_clasificador.py e1 --ngramas-logistica 3,4 --ngramas-centroide 3,5 --numericos-centroide
ml/.venv/Scripts/python ml/scripts/e3_e4_umbrales.py           # E-3 y E-4 (C = 100, n-gramas 3-4)
python ml/scripts/e5_marca.py                                    # E-5
ml/.venv/Scripts/python ml/scripts/vectores_paridad.py          # referencia para C#
```

| Archivo | Responsabilidad |
| :--- | :--- |
| `vectorizador.py` | TF-IDF de n-gramas de caracteres de la sección 5.1. Es la especificación que replica C#. |
| `modelo.py` | Configuración elegida en la Fase 2 (C = 100, n-gramas 3-4, τ = 0,94) y funciones `entrenar` y `predecir`, compartidas por E-3/E-4 y el diagnóstico externo. |
| `datos_modelo.py` | Entrenamiento (etiquetas débiles de términos no ambiguos + `ambiguos.csv`) sin las familias del desarrollo ni de la prueba, y sin categorías con menos de 5 ejemplos (M-9). |
| `e1_e2_clasificador.py` | Validación cruzada de M-22: escenario A (término ambiguo) y B (término desconocido). |
| `e3_e4_umbrales.py` | E-3: umbral de confianza `τ` ponderado por la mezcla de producción. E-4: el sistema completo sobre el desarrollo. |
| `e5_marca.py` | E-5: texto o prefijo primero y pureza mínima del prefijo (solo biblioteca estándar). |
| `diagnosticar_lista_externa.py` | Validación externa sin etiquetas sobre la lista de otro proveedor (sección 11.2): caminos, cobertura, abstención en kiosco, precisión con abreviaturas y tamaño de los grupos de M-26. |
| `vectores_paridad.py` | Escribe `ml/paridad/vectores-referencia.json` (**versionado**): descripciones inventadas con su texto preparado, n-gramas, IDF y vector TF-IDF, para el test de paridad de C# (sección 10). |

Los resultados crudos quedan en `ml/datos/resultados/` (no versionado); los que deciden se registran en la sección 11
del Track 6.6.
