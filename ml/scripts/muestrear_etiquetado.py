"""Muestras para etiquetar a mano: términos ambiguos y conjunto de oro (Track 6.6, secciones 6.1 y 6.2).

Uso: python ml/scripts/muestrear_etiquetado.py ml/datos/lista-mayo-2025.csv
         [--terminos ml/datos/terminos.csv] [--por-termino 25] [--solo GOMA,CINTA] [--oro 400] [--semilla 2026]
         [--excluir ml/datos/oro.csv ml/datos/ambiguos.csv]
         [--salida-ambiguos ml/datos/ambiguos-a-etiquetar.csv] [--salida-oro ml/datos/oro-a-etiquetar.csv]
         [--sobrescribir]

Escribe (';', UTF-8 con BOM):
  --salida-ambiguos  codigo;descripcion;termino;categoria
      Hasta --por-termino familias por cada término ambiguo (o solo los de --solo), una fila por familia. Completar
      `categoria` con una categoría de taxonomia.csv. Son ejemplos de ENTRENAMIENTO. Con --por-termino 0 no se escribe.
  --salida-oro       codigo;descripcion;ean;categoria;marca
      --oro filas, una por familia, estratificadas por término en proporción a su frecuencia (los términos fuera del
      diccionario forman un estrato propio). Completar `categoria` y `marca` (una de marcas.csv, o SIN MARCA).
      Celda vacía = pendiente. Es SOLO para la evaluación final: nunca para entrenar ni elegir hiperparámetros.
      Con --oro 0 no se escribe.

Reglas del muestreo:
  * Familia = tres primeras palabras de la descripción limpia (sección 6.2). Las variantes de color de un producto
    comparten familia; ninguna familia aparece en dos muestras, ni en las planillas de --excluir, para que la
    evaluación mida generalización y no memoria.
  * El oro se etiqueta a ciegas: no trae sugerencias de los diccionarios, que anclarían la respuesta (riesgo R-2).
  * Con la misma lista, las mismas exclusiones y la misma --semilla, el resultado es idéntico.
  * No sobrescribe una planilla existente (puede tener trabajo de etiquetado) salvo con --sobrescribir.
"""

import argparse
import os
import random
import sys
from collections import defaultdict

from comun import escribir_csv, leer_lista, leer_planilla, limpiar_descripcion, normalizar_ean, primera_palabra

FUERA_DEL_DICCIONARIO = "(fuera del diccionario)"

parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
parser.add_argument("lista")
parser.add_argument("--terminos", default="ml/datos/terminos.csv")
parser.add_argument("--por-termino", type=int, default=25, help="familias por término ambiguo; 0 = no muestrear (25)")
parser.add_argument("--solo", default="", help="términos ambiguos a muestrear, separados por coma (todos)")
parser.add_argument("--oro", type=int, default=400, help="filas del conjunto de oro; 0 = no muestrear (400)")
parser.add_argument("--semilla", type=int, default=2026)
parser.add_argument("--excluir", nargs="*", default=[], help="planillas cuyas familias no pueden salir en la muestra")
parser.add_argument("--salida-ambiguos", default="ml/datos/ambiguos-a-etiquetar.csv")
parser.add_argument("--salida-oro", default="ml/datos/oro-a-etiquetar.csv")
parser.add_argument("--sobrescribir", action="store_true")
argumentos = parser.parse_args()

salidas = ([argumentos.salida_ambiguos] if argumentos.por_termino else []) + \
          ([argumentos.salida_oro] if argumentos.oro else [])
existentes = [ruta for ruta in salidas if os.path.exists(ruta)]
if existentes and not argumentos.sobrescribir:
    print(f"Ya existen {existentes}: pueden tener etiquetas. Usar --sobrescribir para reemplazarlas.")
    sys.exit(1)


def familia(descripcion):
    return " ".join(descripcion.split(" ")[:3])


# 1. Familias de cada término (estrato), sin las de las planillas excluidas. Todo se recorre ordenado para que la
#    semilla alcance para reproducir el resultado.
excluidas = {familia(limpiar_descripcion(fila["descripcion"]))
             for ruta in argumentos.excluir for fila in leer_planilla(ruta)}
terminos = {fila["termino"]: fila["ambiguo"] == "1" for fila in leer_planilla(argumentos.terminos)}
familias_de = defaultdict(lambda: defaultdict(list))  # estrato -> familia -> filas disponibles
filas_reales = defaultdict(int)  # estrato -> filas de la lista, incluidas las excluidas
total = 0
for fila in leer_lista(argumentos.lista):
    descripcion = limpiar_descripcion(fila[1])
    if not descripcion:
        continue
    termino = primera_palabra(fila[1])
    estrato = termino if termino in terminos else FUERA_DEL_DICCIONARIO
    total += 1
    filas_reales[estrato] += 1  # las cuotas del oro siguen la distribución real del catálogo
    if familia(descripcion) not in excluidas:
        familias_de[estrato][familia(descripcion)].append(fila)

azar = random.Random(argumentos.semilla)


def una_por_familia(estrato, cantidad, usadas):
    """Elige `cantidad` familias del estrato al azar (fuera de `usadas`) y una fila al azar de cada una."""
    disponibles = sorted(f for f in familias_de[estrato] if f not in usadas)
    elegidas = azar.sample(disponibles, min(cantidad, len(disponibles)))
    return [(f, azar.choice(familias_de[estrato][f])) for f in sorted(elegidas)]


# 2. Muestra de ambiguos (entrenamiento).
ambiguos = []
usadas = set()
if argumentos.por_termino:
    pedidos = {t.strip() for t in argumentos.solo.split(",") if t.strip()}
    desconocidos = pedidos - {t for t, es_ambiguo in terminos.items() if es_ambiguo}
    if desconocidos:
        print(f"No son términos ambiguos en {argumentos.terminos}: {sorted(desconocidos)}")
        sys.exit(1)
    for termino in sorted(pedidos or {t for t, es_ambiguo in terminos.items() if es_ambiguo}):
        for nombre_familia, fila in una_por_familia(termino, argumentos.por_termino, set()):
            usadas.add(nombre_familia)
            ambiguos.append([fila[0], limpiar_descripcion(fila[1]), termino, ""])

# 3. Conjunto de oro: asignación proporcional por estrato con el método del mayor resto, limitada por las familias
#    que quedan libres en cada estrato.
oro = []
cuota = {}
if argumentos.oro:
    libres = {e: len([f for f in familias if f not in usadas]) for e, familias in familias_de.items()}
    cuota_exacta = {e: argumentos.oro * filas_reales[e] / total for e in familias_de}
    cuota = {e: min(int(cuota_exacta[e]), libres[e]) for e in familias_de}
    for e in sorted(familias_de, key=lambda e: (-(cuota_exacta[e] - int(cuota_exacta[e])), e)):
        if sum(cuota.values()) >= argumentos.oro:
            break
        if cuota[e] < libres[e]:
            cuota[e] += 1
    for estrato in sorted(familias_de):
        for _, fila in una_por_familia(estrato, cuota[estrato], usadas):
            gtin, _ = normalizar_ean(fila[0], fila[2])
            oro.append([fila[0], limpiar_descripcion(fila[1]), gtin or "", "", ""])

# 4. Escritura y resumen (solo conteos: las descripciones quedan en los archivos).
print(f"Filas: {total} | familias excluidas: {len(excluidas)} | semilla: {argumentos.semilla}")
if argumentos.por_termino:
    ambiguos.sort(key=lambda f: (f[2], f[1]))
    escribir_csv(argumentos.salida_ambiguos, ["codigo", "descripcion", "termino", "categoria"], ambiguos)
    print(f"Ambiguos: {len(ambiguos)} filas de {len({f[2] for f in ambiguos})} términos -> {argumentos.salida_ambiguos}")
if argumentos.oro:
    oro.sort(key=lambda f: f[1])
    escribir_csv(argumentos.salida_oro, ["codigo", "descripcion", "ean", "categoria", "marca"], oro)
    print(f"Oro: {len(oro)} filas de {sum(1 for c in cuota.values() if c)} estratos -> {argumentos.salida_oro}")
    print(f"  Estrato '{FUERA_DEL_DICCIONARIO}': {cuota.get(FUERA_DEL_DICCIONARIO, 0)} filas | "
          f"términos ambiguos: {sum(c for e, c in cuota.items() if terminos.get(e))} filas | "
          f"con EAN válido: {sum(1 for f in oro if f[2])}")
