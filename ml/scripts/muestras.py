"""Muestra aleatoria de descripciones por primera palabra o por prefijo de código de barras.

Uso:
  python ml/scripts/muestras.py ml/datos/lista-mayo-2025.csv palabra SET,REPUESTO
  python ml/scripts/muestras.py ml/datos/lista-mayo-2025.csv prefijo 7792216,7450077
"""

import random
import sys

from comun import leer_lista, limpiar_descripcion, primera_palabra

TAMANIO_MUESTRA = 7

ruta, modo, valores = sys.argv[1], sys.argv[2], sys.argv[3].split(",")
random.seed(7)  # muestra reproducible
filas = leer_lista(ruta)

for valor in valores:
    if modo == "palabra":
        grupo = [limpiar_descripcion(f[1]) for f in filas if primera_palabra(f[1]) == valor.upper()]
    else:
        grupo = [limpiar_descripcion(f[1]) for f in filas if f[2].strip().startswith(valor)]
    print(f"\n{valor} ({len(grupo)} filas):")
    for descripcion in random.sample(grupo, min(TAMANIO_MUESTRA, len(grupo))):
        print("  ", descripcion)
