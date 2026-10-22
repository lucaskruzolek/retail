"""Modelo de categoría elegido en la Fase 2 del Track 6.6 (sección 11.1). Requiere ml/.venv (scikit-learn).

Configuración: regresión logística multinomial (E-1) sobre TF-IDF de n-gramas de caracteres (E-2), que sugiere solo si
la probabilidad máxima alcanza TAU (E-3; sin umbral de cobertura, M-23).
"""

import numpy as np
from sklearn.linear_model import LogisticRegression

from vectorizador import crear_vectorizador

C = 100.0  # E-1: óptimo interior de la grilla 1, 10, 100, 300, 1000
NGRAMAS = (3, 4)  # E-2
QUITAR_NUMERICOS = True  # E-2
TAU = 0.94  # E-3: máxima cobertura con precisión >= 95 % (M-1)


def entrenar(textos, categorias, c=C, ngramas=NGRAMAS, quitar_numericos=QUITAR_NUMERICOS):
    """Ajusta el vectorizador y el clasificador; devuelve (vectorizador, modelo)."""
    vectorizador = crear_vectorizador(ngramas, quitar_numericos)
    modelo = LogisticRegression(C=c, max_iter=3000).fit(vectorizador.fit_transform(textos), categorias)
    return vectorizador, modelo


def predecir(vectorizador, modelo, textos):
    """Categoría, confianza (probabilidad máxima) y fracción de n-gramas del texto presentes en el vocabulario."""
    probabilidades = modelo.predict_proba(vectorizador.transform(textos))
    analizador = vectorizador.build_analyzer()
    vocabulario = vectorizador.vocabulary_
    coberturas = []
    for texto in textos:
        ngramas = analizador(texto)
        coberturas.append(sum(g in vocabulario for g in ngramas) / len(ngramas) if ngramas else 0.0)
    return modelo.classes_[probabilidades.argmax(axis=1)], probabilidades.max(axis=1), np.array(coberturas)
