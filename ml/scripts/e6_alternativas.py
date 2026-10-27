"""Experimento E-6 del Track 6.6 (sección 11.3): alternativas de entrenamiento simple frente a la regresión logística.

Uso (con ml/.venv, desde la raíz del repositorio): ml/.venv/Scripts/python ml/scripts/e6_alternativas.py
                                                    [--extra ml/datos/e6b-lista1-validar.csv]

Candidatos:
  RL      regresión logística de producción (C = 100, n-gramas 3-4, sin tokens numéricos);
  T       tabla "término ambiguo + segunda palabra", con soporte y pureza mínimos;
  NB      Naive Bayes (multinomial o complementario) sobre los mismos n-gramas TF-IDF;
  T + NB  T cuando aplica y NB en el resto.
Método: validación cruzada de M-22 (A: término ambiguo, agrupado por familia, evaluado sobre los ejemplos manuales;
B: término desconocido, agrupado por término). Métrica de decisión: cobertura con precisión >= 95 % en A.
Regla (M-44): gana el candidato más simple cuya cobertura en A alcance al menos el 85 % de la de RL.

Solo imprime métricas agregadas: ninguna descripción de la lista entra a la salida (regla 6 del handoff).
"""

import time
import argparse
from collections import Counter

import numpy as np
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import f1_score
from sklearn.model_selection import GroupKFold
from sklearn.naive_bayes import ComplementNB, MultinomialNB

import modelo as configuracion
from comun import leer_planilla, primera_palabra
from datos_modelo import Ejemplo, cargar_desarrollo, cargar_entrenamiento, familia, misma_categoria
from e6_comun import TablaDePares
from vectorizador import crear_vectorizador

PLIEGUES = 5
PRECISION_OBJETIVO = 0.95  # M-1
FRACCION_DE_RL = 0.85  # M-44
SOPORTES = (1, 2, 3)
PUREZAS = (0.6, 0.7, 0.8, 0.9, 1.0)
VARIANTES_NB = [(nombre, alfa) for nombre in ("multinomial", "complementario") for alfa in (0.01, 0.1, 1.0)]


# ---------- Candidatos ----------

def crear_nb(variante):
    nombre, alfa = variante
    return MultinomialNB(alpha=alfa) if nombre == "multinomial" else ComplementNB(alpha=alfa)


def crear_rl():
    return LogisticRegression(C=configuracion.C, max_iter=3000)


def confianza_nb(modelo, X):
    """Clase y probabilidad máxima. ComplementNB no calibra predict_proba de forma confiable, pero sí ordena: alcanza
    para calibrar un umbral."""
    probabilidades = modelo.predict_proba(X)
    return modelo.classes_[probabilidades.argmax(axis=1)], probabilidades.max(axis=1)


# ---------- Calibración de umbrales ----------

def calibrar_tau(confianza, acierto):
    """Menor τ (entre las confianzas observadas) con precisión >= 95 % y máxima cobertura; None si ninguno alcanza."""
    orden = np.argsort(-confianza, kind="stable")
    conf, ok = confianza[orden], acierto[orden]
    aciertos_acumulados = np.cumsum(ok)
    mejor = None
    for i in range(len(conf)):
        # Se sugiere a todos los de confianza >= conf[i]: el corte cae al final del último empate.
        if i + 1 < len(conf) and conf[i + 1] == conf[i]:
            continue
        precision = aciertos_acumulados[i] / (i + 1)
        if precision >= PRECISION_OBJETIVO:
            mejor = conf[i]
    return mejor


def medir(sugiere, acierto):
    """(precisión, cobertura, sugeridas)."""
    sugeridas = int(sugiere.sum())
    precision = float((sugiere & acierto).sum() / sugeridas) if sugeridas else 0.0
    return precision, sugeridas / len(sugiere), sugeridas


def calibrar_tabla(soporte, pureza, acierto):
    """Par (s, p) de máxima cobertura con precisión >= 95 %; None si ninguno alcanza."""
    mejor = None
    for s in SOPORTES:
        for p in PUREZAS:
            sugiere = (soporte >= s) & (pureza >= p)
            precision, cobertura, _ = medir(sugiere, acierto)
            candidato = (cobertura, precision)
            if precision >= PRECISION_OBJETIVO and (mejor is None or candidato > mejor[0]):
                mejor = (candidato, s, p)
    return None if mejor is None else (mejor[1], mejor[2])


# ---------- Validación cruzada (M-22) ----------

parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
parser.add_argument("--extra", default=None,
                    help="planilla validada de E-6b (proveedor 1) que se suma a los ejemplos manuales")
argumentos = parser.parse_args()

ejemplos, escasas = cargar_entrenamiento()
if argumentos.extra:
    # Escenario A ampliado (sección 11.4): las filas validadas entran como ejemplos manuales; las de una categoría
    # que no está en el entrenamiento (M-9 o "SIN CATEGORIA") se descartan.
    categorias_validas = {e.categoria for e in ejemplos}
    validadas = leer_planilla(argumentos.extra)
    if any(f["validado"].strip() != "1" for f in validadas):
        raise SystemExit(f"{argumentos.extra}: hay filas sin validar; no se calcula ningún resultado.")
    ejemplos += [Ejemplo(f["codigo"], f["descripcion"], f["categoria"], True, primera_palabra(f["descripcion"]),
                         familia(f["descripcion"])) for f in validadas if f["categoria"] in categorias_validas]
textos = np.array([e.descripcion for e in ejemplos], dtype=object)
categorias = np.array([e.categoria for e in ejemplos])
terminos = np.array([e.termino for e in ejemplos], dtype=object)
manual = np.array([e.manual for e in ejemplos])
escenarios = {
    "A": (np.array([e.familia for e in ejemplos]), manual),
    "B": (terminos, np.ones(len(ejemplos), dtype=bool)),
}

print(f"Entrenamiento: {len(ejemplos)} filas ({manual.sum()} manuales), {len(set(categorias))} categorías | "
      f"excluidas por M-9: {escasas}\n")

# Fuera de partición, por escenario: categoría real y, por candidato, (predicha, confianza) o datos de la tabla.
fuera = {}
for escenario, (grupos, evaluables) in escenarios.items():
    registro = {"real": [], "RL": [], "T": [], **{v: [] for v in VARIANTES_NB}}
    for entrenamiento, prueba in GroupKFold(n_splits=PLIEGUES).split(textos, categorias, grupos):
        prueba = prueba[evaluables[prueba]]
        if len(prueba) == 0:
            continue
        vectorizador = crear_vectorizador(configuracion.NGRAMAS, configuracion.QUITAR_NUMERICOS)
        x_entrenamiento = vectorizador.fit_transform(textos[entrenamiento])
        x_prueba = vectorizador.transform(textos[prueba])
        registro["real"].extend(categorias[prueba])

        rl = crear_rl().fit(x_entrenamiento, categorias[entrenamiento])
        probabilidades = rl.predict_proba(x_prueba)
        registro["RL"].extend(zip(rl.classes_[probabilidades.argmax(axis=1)], probabilidades.max(axis=1)))

        for variante in VARIANTES_NB:
            nb = crear_nb(variante).fit(x_entrenamiento, categorias[entrenamiento])
            registro[variante].extend(zip(*confianza_nb(nb, x_prueba)))

        # La tabla aprende solo de los ejemplos manuales de la partición de entrenamiento.
        manuales = entrenamiento[manual[entrenamiento]]
        tabla = TablaDePares().fit(textos[manuales], terminos[manuales], categorias[manuales])
        registro["T"].extend(tabla.consultar(textos[prueba], terminos[prueba]))
    fuera[escenario] = registro
    print(f"Escenario {escenario}: {len(registro['real'])} filas evaluadas", flush=True)


def columnas(escenario, candidato):
    real = np.array(fuera[escenario]["real"])
    datos = fuera[escenario][candidato]
    predicha = np.array([d[0] for d in datos])
    return real, predicha


def f1(escenario, candidato):
    real, predicha = columnas(escenario, candidato)
    return f1_score(real, predicha, average="macro", labels=sorted(set(real)), zero_division=0)


# Elección de la variante de NB por F1-macro en A (como C en E-1).
mejor_nb = max(VARIANTES_NB, key=lambda v: f1("A", v))
print("\n## F1-macro de las variantes de Naive Bayes (A)\n")
print("| Variante | α | F1 A | F1 B |")
print("| :--- | ---: | ---: | ---: |")
for variante in VARIANTES_NB:
    marca = " (elegida)" if variante == mejor_nb else ""
    print(f"| {variante[0]}{marca} | {variante[1]:g} | {f1('A', variante):.3f} | {f1('B', variante):.3f} |")


# ---------- Cobertura con precisión >= 95 % ----------

def umbral_por_confianza(candidato):
    real, predicha = columnas("A", candidato)
    confianza = np.array([d[1] for d in fuera["A"][candidato]])
    return calibrar_tau(confianza, predicha == real)


def evaluar_por_confianza(escenario, candidato, tau):
    real, predicha = columnas(escenario, candidato)
    confianza = np.array([d[1] for d in fuera[escenario][candidato]])
    sugiere = confianza >= tau if tau is not None else np.zeros(len(real), dtype=bool)
    return medir(sugiere, predicha == real)


def datos_tabla(escenario):
    real = np.array(fuera[escenario]["real"])
    datos = fuera[escenario]["T"]
    predicha = np.array([d[0] for d in datos])
    soporte = np.array([d[1] for d in datos])
    pureza = np.array([d[2] for d in datos])
    return real, predicha, soporte, pureza


def evaluar_tabla(escenario, umbral):
    real, predicha, soporte, pureza = datos_tabla(escenario)
    sugiere = (soporte >= umbral[0]) & (pureza >= umbral[1]) if umbral else np.zeros(len(real), dtype=bool)
    return medir(sugiere, predicha == real), sugiere


tau_rl = umbral_por_confianza("RL")
tau_nb = umbral_por_confianza(mejor_nb)
real_a, predicha_t, soporte_a, pureza_a = datos_tabla("A")
umbral_t = calibrar_tabla(soporte_a, pureza_a, predicha_t == real_a)


def evaluar_combinado(escenario, tau_resto):
    """T donde sugiere; NB (con tau_resto) en el resto."""
    (_, _, _), sugiere_t = evaluar_tabla(escenario, umbral_t)
    real, predicha_t_esc, _, _ = datos_tabla(escenario)
    _, predicha_nb = columnas(escenario, mejor_nb)
    confianza_nb_esc = np.array([d[1] for d in fuera[escenario][mejor_nb]])
    sugiere_nb = ~sugiere_t & (confianza_nb_esc >= tau_resto) if tau_resto is not None else np.zeros(len(real), bool)
    acierto = np.where(sugiere_t, predicha_t_esc == real, predicha_nb == real)
    return medir(sugiere_t | sugiere_nb, acierto)


# τ de NB para la combinación: máxima cobertura conjunta con precisión conjunta >= 95 %.
tau_combinado = None
mejor_cobertura = -1.0
for tau in sorted({d[1] for d in fuera["A"][mejor_nb]}):
    precision, cobertura, _ = evaluar_combinado("A", tau)
    if precision >= PRECISION_OBJETIVO and cobertura > mejor_cobertura:
        tau_combinado, mejor_cobertura = tau, cobertura

resultados = {
    "T": (evaluar_tabla("A", umbral_t)[0], evaluar_tabla("B", umbral_t)[0],
          f"s = {umbral_t[0]}, p = {umbral_t[1]:.1f}" if umbral_t else "ninguno alcanza 95 %"),
    "NB": (evaluar_por_confianza("A", mejor_nb, tau_nb), evaluar_por_confianza("B", mejor_nb, tau_nb),
           f"τ = {tau_nb:.4f}" if tau_nb is not None else "ninguno alcanza 95 %"),
    "T + NB": (evaluar_combinado("A", tau_combinado), evaluar_combinado("B", tau_combinado),
               f"T + τ = {tau_combinado:.4f}" if tau_combinado is not None else "solo T"),
    "RL": (evaluar_por_confianza("A", "RL", tau_rl), evaluar_por_confianza("B", "RL", tau_rl),
           f"τ = {tau_rl:.4f}" if tau_rl is not None else "ninguno alcanza 95 %"),
}

print("\n## Cobertura con precisión ≥ 95 % (umbral calibrado en A)\n")
print("| Candidato | Umbral | F1 A | F1 B | Precisión A | Cobertura A | Sugeridas A | Precisión B | Cobertura B |")
print("| :--- | :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |")
f1_por_candidato = {"T": ("T",), "NB": (mejor_nb,), "T + NB": None, "RL": ("RL",)}
for nombre, ((pa, ca, sa), (pb, cb, _), umbral) in resultados.items():
    claves = f1_por_candidato[nombre]
    f1a = f"{f1('A', claves[0]):.3f}" if claves else "—"
    f1b = f"{f1('B', claves[0]):.3f}" if claves else "—"
    print(f"| {nombre} | {umbral} | {f1a} | {f1b} | {pa:.1%} | {ca:.1%} | {sa} | {pb:.1%} | {cb:.1%} |")


# ---------- Contribución sobre el conjunto de desarrollo (como E-4) ----------

inicio = time.perf_counter()
vectorizador = crear_vectorizador(configuracion.NGRAMAS, configuracion.QUITAR_NUMERICOS)
x_todo = vectorizador.fit_transform(textos)
segundos_vectorizador = time.perf_counter() - inicio
tiempos = {}
inicio = time.perf_counter()
rl_final = crear_rl().fit(x_todo, categorias)
tiempos["RL"] = time.perf_counter() - inicio
inicio = time.perf_counter()
nb_final = crear_nb(mejor_nb).fit(x_todo, categorias)
tiempos["NB"] = time.perf_counter() - inicio
inicio = time.perf_counter()
tabla_final = TablaDePares().fit(textos[manual], terminos[manual], categorias[manual])
tiempos["T"] = time.perf_counter() - inicio
tiempos["T + NB"] = tiempos["T"] + tiempos["NB"]

desarrollo = [f for f in cargar_desarrollo() if f["ruta"] != "termino"]
descripciones = [f["descripcion"] for f in desarrollo]
x_desarrollo = vectorizador.transform(descripciones)
esperadas = [f["categoria"] for f in desarrollo]
rutas = [f["ruta"] for f in desarrollo]

probabilidades = rl_final.predict_proba(x_desarrollo)
pred_rl = rl_final.classes_[probabilidades.argmax(axis=1)]
conf_rl = probabilidades.max(axis=1)
pred_nb, conf_nb = confianza_nb(nb_final, x_desarrollo)
consulta_t = tabla_final.consultar(descripciones, [primera_palabra(d) for d in descripciones])


def sugerencia_dev(nombre, i):
    """(sugiere, categoría) del candidato para la fila i del desarrollo."""
    categoria_t, soporte, pureza = consulta_t[i]
    aplica_t = umbral_t is not None and soporte >= umbral_t[0] and pureza >= umbral_t[1]
    if nombre == "RL":
        return tau_rl is not None and conf_rl[i] >= tau_rl, pred_rl[i]
    if nombre == "NB":
        return tau_nb is not None and conf_nb[i] >= tau_nb, pred_nb[i]
    if nombre == "T":
        return aplica_t, categoria_t
    if aplica_t:
        return True, categoria_t
    return tau_combinado is not None and conf_nb[i] >= tau_combinado, pred_nb[i]


print("\n## Contribución sobre el conjunto de desarrollo (filas que no resuelve la tabla de términos)\n")
print("| Candidato | Ambiguo: sugeridas | Ambiguo: correctas | Desconocido: sugeridas | Desconocido: correctas | "
      "Entrenar (s) |")
print("| :--- | ---: | ---: | ---: | ---: | ---: |")
total_ambiguo = rutas.count("ambiguo")
total_fuera = rutas.count("fuera")
for nombre in ("T", "NB", "T + NB", "RL"):
    conteo = Counter()
    for i, ruta in enumerate(rutas):
        sugiere, categoria = sugerencia_dev(nombre, i)
        if sugiere:
            conteo[(ruta, "sugeridas")] += 1
            if misma_categoria(categoria, esperadas[i]):
                conteo[(ruta, "correctas")] += 1
    print(f"| {nombre} | {conteo[('ambiguo', 'sugeridas')]} de {total_ambiguo} | {conteo[('ambiguo', 'correctas')]} | "
          f"{conteo[('fuera', 'sugeridas')]} de {total_fuera} | {conteo[('fuera', 'correctas')]} | {tiempos[nombre]:.2f} |")
print(f"\nVectorizador (común a NB y RL): {segundos_vectorizador:.2f} s.")


# ---------- Regla de M-44 ----------

cobertura_rl = resultados["RL"][0][1]
print(f"\n## Regla de M-44\n\nCobertura de RL en A: {cobertura_rl:.1%}; umbral de la regla: "
      f"{FRACCION_DE_RL:.0%} de RL = {FRACCION_DE_RL * cobertura_rl:.1%}.")
ganador = "RL"
for nombre in ("T", "NB", "T + NB"):
    (precision, cobertura, _), _, _ = resultados[nombre]
    cumple = precision >= PRECISION_OBJETIVO and cobertura >= FRACCION_DE_RL * cobertura_rl
    print(f"* {nombre}: cobertura {cobertura:.1%} ({cobertura / cobertura_rl:.0%} de RL) -> "
          f"{'cumple' if cumple else 'no cumple'}")
    if cumple:
        ganador = nombre
        break
print(f"\n**Ganador por la regla: {ganador}.**")
