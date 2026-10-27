"""Muestras para el experimento E-6b del Track 6.6 (sección 11.4).

Uso (desde la raíz del repositorio): python ml/scripts/muestrear_e6b.py [--total-piatto 400] [--semilla 2028]
                                         [--sobrescribir]

Escribe (';', UTF-8 con BOM), con la columna `categoria` vacía para etiquetar:
  ml/datos/e6b-piatto-a-etiquetar.csv  codigo;descripcion;termino;categoria
      Filas de Piatto con término ambiguo (sin términos de kiosco), una por familia, repartidas entre términos con un
      tope común: cada término aporta min(familias del término, tope) y el tope es el menor que alcanza --total-piatto.
  ml/datos/e6b-lista1-a-etiquetar.csv  codigo;descripcion;termino;categoria
      Filas del proveedor 1 con término ambiguo, una por familia, de familias que no están en el entrenamiento manual
      (ambiguos.csv), el desarrollo ni el oro sellado.

Solo imprime conteos. Con la misma lista y la misma semilla, el resultado es idéntico. No sobrescribe una planilla
existente (puede tener trabajo de etiquetado) salvo con --sobrescribir.
"""

import argparse
import csv
import os
import random
from collections import defaultdict

from comun import leer_lista, leer_planilla, limpiar_descripcion, primera_palabra
from datos_modelo import AMBIGUOS, DESARROLLO, LISTA, PRUEBA, cargar_terminos, familia

PIATTO = "ml/datos/lista-piatto-1.csv"
KIOSCO = "ml/datos/piatto-kiosco.csv"
SALIDA_PIATTO = "ml/datos/e6b-piatto-a-etiquetar.csv"
SALIDA_LISTA1 = "ml/datos/e6b-lista1-a-etiquetar.csv"

parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
parser.add_argument("--total-piatto", type=int, default=400)
parser.add_argument("--semilla", type=int, default=2028)
parser.add_argument("--sobrescribir", action="store_true")
argumentos = parser.parse_args()

ambiguos = {t for t, f in cargar_terminos().items() if f["ambiguo"] == "1"}
azar = random.Random(argumentos.semilla)


def una_por_familia(filas):
    """{término: [fila elegida al azar de cada familia]}, en orden determinista."""
    por_familia = defaultdict(list)
    for codigo, descripcion in filas:
        por_familia[familia(descripcion)].append((codigo, descripcion))
    por_termino = defaultdict(list)
    for clave in sorted(por_familia):
        codigo, descripcion = azar.choice(sorted(por_familia[clave]))
        por_termino[primera_palabra(descripcion)].append((codigo, descripcion))
    return por_termino


def escribir(ruta, filas):
    if os.path.exists(ruta) and not argumentos.sobrescribir:
        raise SystemExit(f"{ruta} ya existe: no se sobrescribe (usar --sobrescribir).")
    with open(ruta, "w", encoding="utf-8-sig", newline="") as archivo:
        escritor = csv.writer(archivo, delimiter=";")
        escritor.writerow(["codigo", "descripcion", "termino", "categoria"])
        for codigo, descripcion in filas:
            escritor.writerow([codigo, descripcion, primera_palabra(descripcion), ""])


# Piatto: una fila por familia y un tope común por término.
kiosco = {f["termino"] for f in leer_planilla(KIOSCO) if f["es_kiosco"].strip() == "1"}
filas_piatto = [(f[0], f[1]) for f in leer_lista(PIATTO)
                if len(f) > 1 and limpiar_descripcion(f[1])
                and primera_palabra(f[1]) in ambiguos and primera_palabra(f[1]) not in kiosco]
por_termino = una_por_familia(filas_piatto)
disponibles = sum(len(v) for v in por_termino.values())
total = min(argumentos.total_piatto, disponibles)
tope = 1
while sum(min(len(v), tope) for v in por_termino.values()) < total:
    tope += 1
muestra_piatto = []
for termino in sorted(por_termino):
    elegidas = sorted(por_termino[termino])
    azar.shuffle(elegidas)
    muestra_piatto.extend(elegidas[:tope])
azar.shuffle(muestra_piatto)
escribir(SALIDA_PIATTO, muestra_piatto)
print(f"Piatto: {len(muestra_piatto)} filas ({disponibles} familias disponibles, tope {tope} por término); "
      f"por término: {sorted((min(len(v), tope) for v in por_termino.values()), reverse=True)}")

# Proveedor 1: una fila por familia, fuera de todo lo ya usado.
excluidas = {familia(f["descripcion"]) for ruta in (AMBIGUOS, DESARROLLO, PRUEBA) for f in leer_planilla(ruta)}
etiquetados = {f["codigo"] for f in leer_planilla(AMBIGUOS)}
filas_lista1 = [(codigo, descripcion) for codigo, descripcion, _ in leer_lista(LISTA)
                if limpiar_descripcion(descripcion) and primera_palabra(descripcion) in ambiguos
                and codigo not in etiquetados and familia(descripcion) not in excluidas]
muestra_lista1 = [fila for termino in sorted(una_por_familia(filas_lista1).items()) for fila in termino[1]]
azar.shuffle(muestra_lista1)
escribir(SALIDA_LISTA1, muestra_lista1)
print(f"Proveedor 1: {len(muestra_lista1)} filas (una por familia)")
