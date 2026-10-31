"""Vectorizador TF-IDF de n-gramas de caracteres del Track 6.6 (sección 5.1). Requiere ml/.venv (scikit-learn).

Es la especificación que la Fase 5 replica en C#; los vectores de referencia de vectores_paridad.py salen de acá.

  * Entrada: descripción limpia (comun.limpiar_descripcion), sin tildes y en mayúsculas (comun.normalizar_texto).
    Opcionalmente sin los tokens que no tienen ninguna letra (códigos de modelo como "24/6" o "1750").
  * Unidad: n-gramas de caracteres dentro de cada palabra, con un espacio de borde a cada lado ("char_wb").
  * Ponderación: TF por conteo, IDF suavizado ln((1 + N) / (1 + df)) + 1, norma L2.
  * Vocabulario: n-gramas presentes en al menos 2 artículos.
"""

from functools import partial

import numpy as np
from sklearn.feature_extraction.text import TfidfVectorizer

from comun import limpiar_descripcion, normalizar_texto


def preparar(descripcion, quitar_numericos=True):
    """Texto que ve el vectorizador. Es puro y determinista: C# debe producir exactamente la misma cadena."""
    palabras = normalizar_texto(limpiar_descripcion(descripcion)).split(" ")
    if quitar_numericos:
        palabras = [p for p in palabras if any(c.isalpha() for c in p)]
    return " ".join(palabras)


def crear_vectorizador(ngramas=(3, 5), quitar_numericos=True):
    return TfidfVectorizer(
        analyzer="char_wb",
        ngram_range=ngramas,
        preprocessor=partial(preparar, quitar_numericos=quitar_numericos),
        lowercase=False,
        min_df=2,
        smooth_idf=True,
        sublinear_tf=False,
        norm="l2",
        dtype=np.float64,
    )
