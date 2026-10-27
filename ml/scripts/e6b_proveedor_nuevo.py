"""Experimento E-6b del Track 6.6 (sección 11.4): los candidatos de E-6 con un proveedor nuevo (Piatto).

Uso (con ml/.venv, desde la raíz del repositorio):
    ml/.venv/Scripts/python ml/scripts/e6b_proveedor_nuevo.py [--validacion ml/datos/e6b-piatto-validar.csv]
                                                              [--extra ml/datos/e6b-lista1-validar.csv]

Caso real "proveedor nuevo, día uno": los cuatro candidatos se entrenan con el proveedor 1 y se aplican a la muestra
validada de Piatto con los umbrales de E-6, SIN recalibrar. Naive Bayes solo actúa sobre términos ambiguos (la muestra
solo tiene términos ambiguos). Con --extra, se repite agregando al entrenamiento la muestra validada del proveedor 1.

Exige que todas las filas estén validadas (`validado` = 1): ningún resultado se calcula antes de la validación (M-19).
Regla (M-46): Naive Bayes se justifica si T + NB mantiene precisión >= 95 % y suma al menos 10 puntos de cobertura
sobre T. Solo imprime métricas agregadas.
"""

import argparse
import math
from collections import defaultdict

import numpy as np
from sklearn.linear_model import LogisticRegression
from sklearn.naive_bayes import MultinomialNB

import modelo as configuracion
from comun import leer_planilla, primera_palabra
from datos_modelo import Ejemplo, cargar_entrenamiento, familia, misma_categoria
from e6_comun import TablaDePares
from vectorizador import crear_vectorizador

# Umbrales calibrados en E-6 (sección 11.3); no se recalibran: es el día uno con un proveedor nuevo.
UMBRAL_T = (1, 0.6)
TAU_NB = 1.0
TAU_T_NB = 0.9989
TAU_RL = 0.9364
ALFA_NB = 0.01
PRECISION_OBJETIVO = 0.95  # M-1
PUNTOS_MINIMOS_NB = 0.10  # M-46
SIN_CATEGORIA = "SIN CATEGORIA"

parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
parser.add_argument("--validacion", default="ml/datos/e6b-piatto-validar.csv")
parser.add_argument("--extra", default=None, help="muestra validada del proveedor 1 para sumar al entrenamiento")
argumentos = parser.parse_args()


def leer_validada(ruta):
    filas = leer_planilla(ruta)
    pendientes = [i for i, f in enumerate(filas, start=2) if f["validado"].strip() != "1"]
    if pendientes:
        raise SystemExit(f"{ruta}: {len(pendientes)} filas sin validar (primera en la línea {pendientes[0]}). "
                         "No se calcula ningún resultado antes de la validación.")
    taxonomia = {f["categoria"] for f in leer_planilla("ml/datos/taxonomia.csv")} | {SIN_CATEGORIA}
    invalidas = sorted({f["categoria"] for f in filas} - taxonomia)
    if invalidas:
        raise SystemExit(f"{ruta}: categorías fuera de la taxonomía: {invalidas}")
    return filas


def wilson(exitos, total, z=1.96):
    if total == 0:
        return "—"
    p = exitos / total
    centro = (p + z * z / (2 * total)) / (1 + z * z / total)
    margen = z * math.sqrt(p * (1 - p) / total + z * z / (4 * total * total)) / (1 + z * z / total)
    return f"{100 * max(0.0, centro - margen):.1f} a {100 * min(1.0, centro + margen):.1f}"


def entrenar(ejemplos):
    textos = [e.descripcion for e in ejemplos]
    categorias = np.array([e.categoria for e in ejemplos])
    vectorizador = crear_vectorizador(configuracion.NGRAMAS, configuracion.QUITAR_NUMERICOS)
    x = vectorizador.fit_transform(textos)
    rl = LogisticRegression(C=configuracion.C, max_iter=3000).fit(x, categorias)
    nb = MultinomialNB(alpha=ALFA_NB).fit(x, categorias)
    manuales = [e for e in ejemplos if e.manual]
    tabla = TablaDePares().fit([e.descripcion for e in manuales], [e.termino for e in manuales],
                               [e.categoria for e in manuales])
    return vectorizador, rl, nb, tabla


def evaluar(ejemplos, filas, titulo):
    vectorizador, rl, nb, tabla = entrenar(ejemplos)
    descripciones = [f["descripcion"] for f in filas]
    esperadas = [f["categoria"] for f in filas]
    terminos = [primera_palabra(d) for d in descripciones]
    x = vectorizador.transform(descripciones)

    probabilidades = rl.predict_proba(x)
    pred_rl, conf_rl = rl.classes_[probabilidades.argmax(axis=1)], probabilidades.max(axis=1)
    probabilidades = nb.predict_proba(x)
    pred_nb, conf_nb = nb.classes_[probabilidades.argmax(axis=1)], probabilidades.max(axis=1)
    consulta = tabla.consultar(descripciones, terminos)

    def sugerencia(candidato, i):
        categoria_t, soporte, pureza = consulta[i]
        aplica_t = soporte >= UMBRAL_T[0] and pureza >= UMBRAL_T[1]
        if candidato == "T":
            return aplica_t, categoria_t
        if candidato == "NB":
            return conf_nb[i] >= TAU_NB, pred_nb[i]
        if candidato == "RL":
            return conf_rl[i] >= TAU_RL, pred_rl[i]
        if aplica_t:
            return True, categoria_t
        return conf_nb[i] >= TAU_T_NB, pred_nb[i]

    print(f"\n## {titulo}\n")
    print(f"Entrenamiento: {len(ejemplos)} filas ({sum(e.manual for e in ejemplos)} manuales). "
          f"Evaluación: {len(filas)} filas validadas de {len({familia(d) for d in descripciones})} familias "
          f"({sum(e == SIN_CATEGORIA for e in esperadas)} sin categoría en la taxonomía).\n")
    print("| Candidato | Sugeridas | Correctas | Precisión (IC 95 %) | Cobertura (IC 95 %) |")
    print("| :--- | ---: | ---: | :--- | :--- |")
    resumen = {}
    por_termino = defaultdict(lambda: defaultdict(lambda: [0, 0, 0]))  # término -> candidato -> [filas, sug, ok]
    for candidato in ("T", "NB", "T + NB", "RL"):
        sugeridas = correctas = 0
        for i, esperada in enumerate(esperadas):
            sugiere, categoria = sugerencia(candidato, i)
            celda = por_termino[terminos[i]][candidato]
            celda[0] += 1
            if sugiere:
                sugeridas += 1
                celda[1] += 1
                if esperada != SIN_CATEGORIA and misma_categoria(categoria, esperada):
                    correctas += 1
                    celda[2] += 1
        precision = correctas / sugeridas if sugeridas else 0.0
        cobertura = sugeridas / len(filas)
        resumen[candidato] = (precision, cobertura)
        print(f"| {candidato} | {sugeridas} | {correctas} | {precision:.1%} ({wilson(correctas, sugeridas)}) | "
              f"{cobertura:.1%} ({wilson(sugeridas, len(filas))}) |")

    print("\nPor término (sugeridas / correctas):\n")
    print("| Término | Filas | T | NB | T + NB | RL |")
    print("| :--- | ---: | :--- | :--- | :--- | :--- |")
    for termino in sorted(por_termino, key=lambda t: -por_termino[t]["T"][0]):
        celdas = por_termino[termino]
        valores = " | ".join(f"{celdas[c][1]} / {celdas[c][2]}" for c in ("T", "NB", "T + NB", "RL"))
        print(f"| {termino} | {celdas['T'][0]} | {valores} |")
    return resumen


filas = leer_validada(argumentos.validacion)
ejemplos, _ = cargar_entrenamiento()
resumen = evaluar(ejemplos, filas, "Proveedor nuevo, día uno (entrenamiento: proveedor 1)")

if argumentos.extra:
    categorias_validas = {e.categoria for e in ejemplos}
    extra = [Ejemplo(f["codigo"], f["descripcion"], f["categoria"], True, primera_palabra(f["descripcion"]),
                     familia(f["descripcion"]))
             for f in leer_validada(argumentos.extra) if f["categoria"] in categorias_validas]
    evaluar(ejemplos + extra, filas, "Con la muestra validada del proveedor 1 sumada al entrenamiento")

precision_t, cobertura_t = resumen["T"]
precision_tnb, cobertura_tnb = resumen["T + NB"]
print("\n## Regla de M-46 (sobre el día uno)\n")
print(f"* T: precisión {precision_t:.1%}, cobertura {cobertura_t:.1%}"
      + (" -> **bajo el 95 %: revisar sus umbrales**" if precision_t < PRECISION_OBJETIVO else ""))
suma = cobertura_tnb - cobertura_t
justificado = precision_tnb >= PRECISION_OBJETIVO and suma >= PUNTOS_MINIMOS_NB
print(f"* T + NB: precisión {precision_tnb:.1%}, cobertura {cobertura_tnb:.1%} ({suma * 100:+.1f} puntos sobre T)")
print(f"\n**Naive Bayes {'se justifica' if justificado else 'no se justifica'} por la regla de M-46.**")
