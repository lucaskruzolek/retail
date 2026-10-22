"""Diagnóstico sin etiquetas de la lista de otro proveedor (Track 6.6, validación externa). Requiere ml/.venv.

Uso: ml/.venv/Scripts/python ml/scripts/diagnosticar_lista_externa.py
         [--lista ml/datos/lista-piatto-1.csv] [--kiosco ml/datos/piatto-kiosco.csv]

El sistema se construyó solo con la lista del primer proveedor. Sobre la lista nueva mide, sin etiquetar:
  1. Por qué camino llega cada producto del negocio: tabla de términos, modelo con término ambiguo o modelo con
     término desconocido; y la cobertura de marcas por texto.
  2. Qué fracción de los n-gramas del texto conoce el modelo y cuánto sugiere con el umbral TAU.
  3. Abstención fuera del dominio: en qué fracción de los productos de kiosco (fuera del negocio) sugiere igual.
  4. Precisión aproximada con abreviaturas: si la primera palabra es CARP, la categoría correcta es la de CARPETA en
     la tabla de términos. Es una evaluación de precisión en un proveedor nuevo que no requiere etiquetado.
El modelo se entrena con todo el entrenamiento del primer proveedor (datos_modelo.py), como en E-4.
"""

import argparse
from collections import Counter

import numpy as np

from comun import leer_lista, leer_planilla, limpiar_descripcion, marcas_en, normalizar_texto, primera_palabra
from datos_modelo import cargar_entrenamiento, cargar_terminos, misma_categoria
from modelo import TAU, entrenar, predecir

ABREVIATURAS = {"CARP": "CARPETA", "MARC": "MARCADOR", "ACRIL": "ACRILICO", "LAP": "LAPIZ", "PINT": "PINTURA",
                "CUAD": "CUADERNO"}

parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
parser.add_argument("--lista", default="ml/datos/lista-piatto-1.csv")
parser.add_argument("--kiosco", default="ml/datos/piatto-kiosco.csv")
argumentos = parser.parse_args()

terminos = cargar_terminos()
kiosco = {fila["termino"] for fila in leer_planilla(argumentos.kiosco) if fila["es_kiosco"].strip() == "1"}
forma_a_marca = {}
for fila in leer_planilla("ml/datos/marcas.csv"):
    for forma in [fila["marca"], *(a for a in fila["alias"].split("|") if a)]:
        forma_a_marca[normalizar_texto(forma)] = fila["marca"]

filas = [f for f in leer_lista(argumentos.lista) if len(f) > 1 and limpiar_descripcion(f[1])]
descripciones = [f[1] for f in filas]
primeras = [primera_palabra(d) for d in descripciones]


def ruta(termino):
    if termino in kiosco:
        return "kiosco"
    if termino not in terminos:
        return "desconocido"
    return "ambiguo" if terminos[termino]["ambiguo"] == "1" else "tabla"


rutas = np.array([ruta(t) for t in primeras])
ejemplos, _ = cargar_entrenamiento()
vectorizador, modelo = entrenar([e.descripcion for e in ejemplos], [e.categoria for e in ejemplos])
predichas, confianza, cobertura = predecir(vectorizador, modelo, descripciones)
sugiere = confianza >= TAU
negocio = rutas != "kiosco"
total_negocio = int(negocio.sum())

print(f"Lista: {len(filas)} filas | kiosco: {int((~negocio).sum())} | negocio: {total_negocio} | τ = {TAU}\n")
print("## 1 y 2. Camino de cada producto del negocio\n")
print("| Camino | Filas | % del negocio | n-gramas conocidos (media) | Sugiere el modelo |")
print("| :--- | ---: | ---: | ---: | ---: |")
sugeridas_sistema = 0
for nombre, etiqueta in (("tabla", "Tabla de términos"), ("ambiguo", "Modelo · término ambiguo"),
                         ("desconocido", "Modelo · término desconocido")):
    mascara = rutas == nombre
    n = int(mascara.sum())
    if nombre == "tabla":
        sugeridas_sistema += n
        modelo_texto = "— (decide la tabla)"
    else:
        s = int((sugiere & mascara).sum())
        sugeridas_sistema += s
        modelo_texto = f"{s} ({s / n:.1%})" if n else "—"
    print(f"| {etiqueta} | {n} | {n / total_negocio:.1%} | {cobertura[mascara].mean():.1%} | {modelo_texto} |")
print(f"\n**Cobertura del sistema en el negocio:** {sugeridas_sistema / total_negocio:.1%} "
      f"(tabla + sugerencias del modelo con τ = {TAU}).")
con_marca = sum(1 for d, n in zip(descripciones, negocio) if n and marcas_en(d, forma_a_marca))
print(f"**Marca por texto:** {con_marca} filas del negocio ({con_marca / total_negocio:.1%}); sin EAN no hay prefijos.")

print("\n## 3. Abstención fuera del dominio (kiosco)\n")
mascara = ~negocio
s = int((sugiere & mascara).sum())
print(f"Productos de kiosco: {int(mascara.sum())} | el modelo sugiere una categoría en {s} ({s / mascara.sum():.1%}) | "
      f"n-gramas conocidos (media): {cobertura[mascara].mean():.1%}")
if s:
    print("Categorías sugeridas a productos de kiosco: "
          + ", ".join(f"{c} ({n})" for c, n in Counter(predichas[sugiere & mascara]).most_common(6)))

print("\n## 4. Precisión aproximada con abreviaturas (categoría esperada = la del término completo)\n")
print("| Abreviatura | Término completo | Categoría esperada | Filas | Sugiere | Correctas | Precisión | "
      "Acierto sin umbral |")
print("| :--- | :--- | :--- | ---: | ---: | ---: | ---: | ---: |")
total_sugeridas = total_correctas = 0
for abreviatura, completo in ABREVIATURAS.items():
    esperada = terminos[completo]["categoria"]
    mascara = np.array([t == abreviatura for t in primeras])
    aciertos = np.array([misma_categoria(c, esperada) for c in predichas])
    n, s = int(mascara.sum()), int((mascara & sugiere).sum())
    correctas = int((mascara & sugiere & aciertos).sum())
    total_sugeridas, total_correctas = total_sugeridas + s, total_correctas + correctas
    precision = f"{correctas / s:.1%}" if s else "—"
    print(f"| {abreviatura} | {completo} | {esperada} | {n} | {s} ({s / n:.0%}) | {correctas} | {precision} | "
          f"{(mascara & aciertos).sum() / n:.1%} |")
if total_sugeridas:
    print(f"\nTotal con umbral: {total_correctas} de {total_sugeridas} sugerencias correctas "
          f"({total_correctas / total_sugeridas:.1%}).")

print("\n## 5. Grupos de término desconocido para asignación en bloque (M-16 antes que el modelo, M-26)\n")
desconocidos = rutas == "desconocido"
tamanio = Counter(t for t, d in zip(primeras, desconocidos) if d)
print(f"Términos desconocidos en el negocio: {len(tamanio)} | filas: {int(desconocidos.sum())}\n")
print("| Tamaño mínimo del grupo | Grupos (decisiones del usuario) | Filas resueltas en bloque | "
      "Filas que quedan para el modelo | El modelo sugiere en |")
print("| ---: | ---: | ---: | ---: | ---: |")
for minimo in (2, 3, 5, 10, 20):
    grandes = {t for t, n in tamanio.items() if n >= minimo}
    en_bloque = np.array([d and t in grandes for t, d in zip(primeras, desconocidos)])
    resto = desconocidos & ~en_bloque
    print(f"| {minimo} | {len(grandes)} | {int(en_bloque.sum())} ({en_bloque.sum() / desconocidos.sum():.1%}) | "
          f"{int(resto.sum())} | {int((resto & sugiere).sum())} |")
