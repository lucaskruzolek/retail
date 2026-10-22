"""Experimentos E-3 (umbrales de abstención) y E-4 (aporte de cada nivel) del Track 6.6.

Uso (con ml/.venv): ml/.venv/Scripts/python ml/scripts/e3_e4_umbrales.py [--c 100] [--ngramas 3,4] [--con-numericos]

E-3 (sección 5.3): se sugiere una categoría solo si
    confianza (probabilidad máxima) >= tau   y   cobertura (fracción de n-gramas del texto en el vocabulario) >= kappa.
  Se calibran sobre las predicciones fuera de partición de los escenarios A y B de M-22, ponderados según la mezcla real
  de ítems que llegan al modelo (términos ambiguos frente a términos fuera del diccionario en la lista).
  Regla: la combinación de máxima cobertura con precisión >= 95 % (M-1).
E-4: el sistema completo sobre el conjunto de desarrollo (M-20): la tabla de términos resuelve los términos no ambiguos
  y el modelo, entrenado con todo el entrenamiento, el resto con los umbrales de E-3. Informa precisión y cobertura de
  cada nivel con su intervalo de confianza del 95 % (Wilson).
"""

import argparse
import math

import numpy as np
from sklearn.linear_model import LogisticRegression
from sklearn.model_selection import GroupKFold

from comun import leer_lista, primera_palabra
from datos_modelo import cargar_desarrollo, cargar_entrenamiento, cargar_terminos, misma_categoria
from vectorizador import crear_vectorizador

PLIEGUES = 5
PRECISION_OBJETIVO = 0.95  # M-1
VALORES_TAU = np.round(np.arange(0.0, 1.0001, 0.01), 2)
VALORES_KAPPA = np.round(np.arange(0.0, 1.0001, 0.05), 2)

parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
parser.add_argument("--c", type=float, default=100.0)
parser.add_argument("--ngramas", default="3,4")
parser.add_argument("--con-numericos", action="store_true")
argumentos = parser.parse_args()
NGRAMAS = tuple(int(n) for n in argumentos.ngramas.split(","))


def entrenar(textos, categorias):
    vectorizador = crear_vectorizador(NGRAMAS, not argumentos.con_numericos)
    modelo = LogisticRegression(C=argumentos.c, max_iter=3000).fit(vectorizador.fit_transform(textos), categorias)
    return vectorizador, modelo


def predecir(vectorizador, modelo, textos):
    """Categoría, confianza (probabilidad máxima) y cobertura del vocabulario de cada texto."""
    probabilidades = modelo.predict_proba(vectorizador.transform(textos))
    analizador = vectorizador.build_analyzer()
    vocabulario = vectorizador.vocabulary_
    coberturas = []
    for texto in textos:
        ngramas = analizador(texto)
        coberturas.append(sum(g in vocabulario for g in ngramas) / len(ngramas) if ngramas else 0.0)
    return modelo.classes_[probabilidades.argmax(axis=1)], probabilidades.max(axis=1), np.array(coberturas)


def intervalo_wilson(exitos, total, z=1.96):
    if total == 0:
        return 0.0, 1.0
    p = exitos / total
    centro = (p + z * z / (2 * total)) / (1 + z * z / total)
    margen = z * math.sqrt(p * (1 - p) / total + z * z / (4 * total * total)) / (1 + z * z / total)
    return max(0.0, centro - margen), min(1.0, centro + margen)


def mezcla_de_produccion():
    """Fracción de los ítems que llegan al modelo con término ambiguo (el resto: término fuera del diccionario)."""
    terminos = cargar_terminos()
    ambiguos = fuera = 0
    for _, descripcion, _ in leer_lista("ml/datos/lista-mayo-2025.csv"):
        termino = terminos.get(primera_palabra(descripcion))
        if termino is None:
            fuera += 1
        elif termino["ambiguo"] == "1":
            ambiguos += 1
    return ambiguos / (ambiguos + fuera)


# 1. Predicciones fuera de partición (M-22).
ejemplos, _ = cargar_entrenamiento()
textos = np.array([e.descripcion for e in ejemplos], dtype=object)
categorias = np.array([e.categoria for e in ejemplos])
escenarios = {
    "A": (np.array([e.familia for e in ejemplos]), np.array([e.manual for e in ejemplos])),
    "B": (np.array([e.termino for e in ejemplos]), np.ones(len(ejemplos), dtype=bool)),
}
fuera_de_particion = {}
for nombre, (grupos, evaluables) in escenarios.items():
    acierto, confianza, cobertura = [], [], []
    for entrenamiento, prueba in GroupKFold(n_splits=PLIEGUES).split(textos, categorias, grupos):
        prueba = prueba[evaluables[prueba]]
        if len(prueba) == 0:
            continue
        vectorizador, modelo = entrenar(textos[entrenamiento], categorias[entrenamiento])
        predichas, conf, cob = predecir(vectorizador, modelo, textos[prueba])
        acierto.extend(predichas == categorias[prueba])
        confianza.extend(conf)
        cobertura.extend(cob)
    fuera_de_particion[nombre] = (np.array(acierto), np.array(confianza), np.array(cobertura))

# 2. E-3: grilla de umbrales con los escenarios ponderados por la mezcla de producción.
fraccion_a = mezcla_de_produccion()
acierto = np.concatenate([fuera_de_particion[n][0] for n in "AB"])
confianza = np.concatenate([fuera_de_particion[n][1] for n in "AB"])
cobertura = np.concatenate([fuera_de_particion[n][2] for n in "AB"])
n_a, n_b = len(fuera_de_particion["A"][0]), len(fuera_de_particion["B"][0])
pesos = np.concatenate([np.full(n_a, fraccion_a / n_a), np.full(n_b, (1 - fraccion_a) / n_b)])


def medir(tau, kappa, mascara=None):
    """(precisión, cobertura) ponderadas de las sugerencias con esos umbrales."""
    elegidas = (confianza >= tau) & (cobertura >= kappa)
    if mascara is not None:
        elegidas &= mascara
        total = pesos[mascara].sum()
    else:
        total = pesos.sum()
    sugerido = pesos[elegidas].sum()
    precision = pesos[elegidas & acierto].sum() / sugerido if sugerido else 0.0
    return precision, sugerido / total


mejor = None
for tau in VALORES_TAU:
    for kappa in VALORES_KAPPA:
        precision, alcance = medir(tau, kappa)
        candidato = (alcance, precision, -tau, -kappa)
        if precision >= PRECISION_OBJETIVO and (mejor is None or candidato > mejor[0]):
            mejor = (candidato, tau, kappa)

print(f"Entrenamiento: {len(ejemplos)} filas | C = {argumentos.c:g} | n-gramas {NGRAMAS} | "
      f"{'con' if argumentos.con_numericos else 'sin'} numéricos")
print(f"Mezcla de producción: {fraccion_a:.1%} término ambiguo (A), {1 - fraccion_a:.1%} término desconocido (B)\n")
print("## E-3: umbrales\n")
print("| Umbrales | Precisión ponderada | Cobertura ponderada | Precisión A | Cobertura A | Precisión B | Cobertura B |")
print("| :--- | ---: | ---: | ---: | ---: | ---: | ---: |")
mascara_a = np.concatenate([np.ones(n_a, dtype=bool), np.zeros(n_b, dtype=bool)])
for etiqueta, tau, kappa in [("sin umbral", 0.0, 0.0)] + ([("**elegidos**", mejor[1], mejor[2])] if mejor else []):
    p, c = medir(tau, kappa)
    pa, ca = medir(tau, kappa, mascara_a)
    pb, cb = medir(tau, kappa, ~mascara_a)
    print(f"| {etiqueta}: τ = {tau:.2f}, κ = {kappa:.2f} | {p:.1%} | {c:.1%} | {pa:.1%} | {ca:.1%} | {pb:.1%} | {cb:.1%} |")
if mejor is None:
    print(f"\nNinguna combinación alcanza {PRECISION_OBJETIVO:.0%} de precisión.")
    raise SystemExit(1)
tau, kappa = mejor[1], mejor[2]

# 3. E-4: el sistema completo sobre el conjunto de desarrollo.
vectorizador, modelo = entrenar(textos, categorias)
desarrollo = cargar_desarrollo()
al_modelo = [f for f in desarrollo if f["ruta"] != "termino"]
predichas, conf, cob = predecir(vectorizador, modelo, [f["descripcion"] for f in al_modelo])
niveles = {"Tabla de términos": [], "Modelo · término ambiguo": [], "Modelo · término desconocido": []}
for fila in desarrollo:
    if fila["ruta"] == "termino":
        niveles["Tabla de términos"].append((True, misma_categoria(fila["categoria_termino"], fila["categoria"])))
for fila, categoria, c, k in zip(al_modelo, predichas, conf, cob):
    nivel = "Modelo · término ambiguo" if fila["ruta"] == "ambiguo" else "Modelo · término desconocido"
    niveles[nivel].append((c >= tau and k >= kappa, misma_categoria(categoria, fila["categoria"])))

print("\n## E-4: sistema completo sobre el conjunto de desarrollo\n")
print("| Nivel | Filas | Sugeridas | Cobertura | Correctas | Precisión | IC 95 % precisión |")
print("| :--- | ---: | ---: | ---: | ---: | ---: | :--- |")
total_filas = total_sugeridas = total_correctas = 0
for nivel, resultados in list(niveles.items()) + [("**Sistema**", None)]:
    if resultados is None:
        filas, sugeridas, correctas = total_filas, total_sugeridas, total_correctas
    else:
        filas = len(resultados)
        sugeridas = sum(1 for s, _ in resultados if s)
        correctas = sum(1 for s, ok in resultados if s and ok)
        total_filas, total_sugeridas, total_correctas = (total_filas + filas, total_sugeridas + sugeridas,
                                                         total_correctas + correctas)
    inferior, superior = intervalo_wilson(correctas, sugeridas)
    precision = f"{correctas / sugeridas:.1%}" if sugeridas else "—"
    print(f"| {nivel} | {filas} | {sugeridas} | {sugeridas / filas:.1%} | {correctas} | {precision} | "
          f"{inferior:.1%} a {superior:.1%} |")
print("\nLa tabla de términos se corrigió mirando este conjunto (M-20): su precisión acá es optimista.")
