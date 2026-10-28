"""Experimentos E-2 (vectorizador) y E-1 (clasificador) del Track 6.6, con la validación cruzada de M-22.

Uso (con ml/.venv): ml/.venv/Scripts/python ml/scripts/e1_e2_clasificador.py e2
                    ml/.venv/Scripts/python ml/scripts/e1_e2_clasificador.py e1 --ngramas 3,5 [--con-numericos]

Escenarios (M-22), 5 particiones cada uno:
  A  término ambiguo: particiones agrupadas por familia; se evalúa solo sobre los ejemplos manuales.
  B  término desconocido: particiones agrupadas por término; se evalúa sobre todas las filas excluidas.
Métrica: F1-macro de las predicciones fuera de partición, sobre las categorías presentes en lo evaluado.
Decisión: promedio de A y B.

E-2: largo de n-gramas (3-4, 3-5, 2-5) x tokens numéricos (quitar o no), para cada clasificador.
     Regla: mejor F1-macro.
E-1: regresión logística (C en 1, 10, 100) contra centroide, con el vectorizador ganador de cada uno.
     Regla: centroide, salvo que la regresión lo supere por al menos 2 puntos de F1-macro (sección 5.2).
"""

import argparse
import time

import numpy as np
from scipy import sparse
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import f1_score
from sklearn.model_selection import GroupKFold

from datos_modelo import cargar_entrenamiento
from vectorizador import crear_vectorizador

PLIEGUES = 5


class Centroide:
    """Centroide más cercano por similitud coseno (sección 5.2): un promedio normalizado por categoría."""

    def fit(self, X, y):
        self.classes_, indices = np.unique(y, return_inverse=True)
        pertenencia = sparse.csr_matrix((np.ones(len(y)), (indices, np.arange(len(y)))),
                                        shape=(len(self.classes_), len(y)))
        centroides = np.asarray((pertenencia @ X).todense())
        normas = np.linalg.norm(centroides, axis=1, keepdims=True)
        self.centroides_ = centroides / np.where(normas == 0, 1, normas)
        return self

    def decision_function(self, X):
        return np.asarray(X @ self.centroides_.T)  # las filas de X ya tienen norma L2 = 1: es la similitud coseno

    def predict(self, X):
        return self.classes_[self.decision_function(X).argmax(axis=1)]


def crear_clasificador(nombre, c=10.0):
    if nombre == "centroide":
        return Centroide()
    return LogisticRegression(C=c, max_iter=3000)


def evaluar(ejemplos, ngramas, quitar_numericos, clasificador, c=10.0):
    """F1-macro de los escenarios A y B (M-22) y el tiempo total."""
    textos = np.array([e.descripcion for e in ejemplos], dtype=object)
    categorias = np.array([e.categoria for e in ejemplos])
    manual = np.array([e.manual for e in ejemplos])
    escenarios = {
        "A": (np.array([e.familia for e in ejemplos]), manual),
        "B": (np.array([e.termino for e in ejemplos]), np.ones(len(ejemplos), dtype=bool)),
    }
    resultado = {}
    inicio = time.perf_counter()
    for nombre, (grupos, evaluables) in escenarios.items():
        reales, predichas = [], []
        for entrenamiento, prueba in GroupKFold(n_splits=PLIEGUES).split(textos, categorias, grupos):
            prueba = prueba[evaluables[prueba]]
            if len(prueba) == 0:
                continue
            vectorizador = crear_vectorizador(ngramas, quitar_numericos)  # se ajusta dentro de la partición
            modelo = crear_clasificador(clasificador, c).fit(
                vectorizador.fit_transform(textos[entrenamiento]), categorias[entrenamiento])
            reales.extend(categorias[prueba])
            predichas.extend(modelo.predict(vectorizador.transform(textos[prueba])))
        resultado[nombre] = f1_score(reales, predichas, average="macro", labels=sorted(set(reales)), zero_division=0)
    resultado["promedio"] = (resultado["A"] + resultado["B"]) / 2
    resultado["segundos"] = time.perf_counter() - inicio
    return resultado


def fila(etiqueta, r):
    return f"| {etiqueta} | {r['A']:.3f} | {r['B']:.3f} | **{r['promedio']:.3f}** | {r['segundos']:.0f} |"


parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
parser.add_argument("etapa", choices=["e2", "e1"])
parser.add_argument("--ngramas-logistica", default="3,5", help="E-1: n-gramas ganadores de la regresión en E-2")
parser.add_argument("--ngramas-centroide", default="3,5", help="E-1: n-gramas ganadores del centroide en E-2")
parser.add_argument("--numericos-logistica", action="store_true", help="E-1: la regresión conserva tokens numéricos")
parser.add_argument("--numericos-centroide", action="store_true", help="E-1: el centroide conserva tokens numéricos")
parser.add_argument("--valores-c", default="1,10,100", help="E-1: valores de C de la regresión, separados por coma")
argumentos = parser.parse_args()

ejemplos, escasas = cargar_entrenamiento()
print(f"Entrenamiento: {len(ejemplos)} filas ({sum(e.manual for e in ejemplos)} manuales), "
      f"{len({e.categoria for e in ejemplos})} categorías, {len({e.familia for e in ejemplos})} familias, "
      f"{len({e.termino for e in ejemplos})} términos | excluidas por M-9: {escasas}\n")
print("| Configuración | F1 A (ambiguo) | F1 B (desconocido) | Promedio | s |")
print("| :--- | ---: | ---: | ---: | ---: |")

if argumentos.etapa == "e2":
    for clasificador in ("centroide", "logistica"):
        for ngramas in ((3, 4), (3, 5), (2, 5)):
            for quitar in (True, False):
                r = evaluar(ejemplos, ngramas, quitar, clasificador)
                print(fila(f"{clasificador} · {ngramas[0]}-{ngramas[1]} · "
                           f"{'sin' if quitar else 'con'} numéricos", r), flush=True)
else:
    def leer_ngramas(texto):
        return tuple(int(n) for n in texto.split(","))

    centroide = evaluar(ejemplos, leer_ngramas(argumentos.ngramas_centroide), not argumentos.numericos_centroide,
                        "centroide")
    print(fila("centroide", centroide), flush=True)
    mejor = None
    for c in (float(valor) for valor in argumentos.valores_c.split(",")):
        r = evaluar(ejemplos, leer_ngramas(argumentos.ngramas_logistica), not argumentos.numericos_logistica,
                    "logistica", c)
        print(fila(f"logística C={c:g}", r), flush=True)
        if mejor is None or r["promedio"] > mejor["promedio"]:
            mejor = r
    diferencia = (mejor["promedio"] - centroide["promedio"]) * 100
    ganador = "la regresión logística" if diferencia >= 2 else "el centroide"
    print(f"\nE-1: la mejor regresión supera al centroide por {diferencia:+.1f} puntos -> se usa {ganador} "
          "(regla: centroide salvo diferencia >= 2 puntos)")
