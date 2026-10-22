"""Conjuntos para los experimentos del modelo de categoría (Track 6.6, secciones 6.2 y M-22).

Entrenamiento = filas de la lista con etiqueta:
  * débil: la primera palabra es un término NO ambiguo de terminos.csv (su categoría etiqueta la fila);
  * manual: la fila está en ambiguos.csv (ejemplos etiquetados de términos ambiguos).
Se excluyen las familias del conjunto de desarrollo (oro.csv) y del oro de prueba, y las categorías con menos de
MINIMO_POR_CATEGORIA ejemplos (M-9): esas quedan a cargo de la tabla de términos.
"""

from collections import Counter
from dataclasses import dataclass

from comun import leer_lista, leer_planilla, limpiar_descripcion, normalizar_texto, primera_palabra

MINIMO_POR_CATEGORIA = 5  # M-9

LISTA = "ml/datos/lista-mayo-2025.csv"
TERMINOS = "ml/datos/terminos.csv"
AMBIGUOS = "ml/datos/ambiguos.csv"
DESARROLLO = "ml/datos/oro.csv"
PRUEBA = "ml/datos/oro-prueba-a-etiquetar.csv"


@dataclass(frozen=True)
class Ejemplo:
    codigo: str
    descripcion: str
    categoria: str
    manual: bool
    termino: str
    familia: str


def familia(descripcion):
    """Tres primeras palabras de la descripción limpia (sección 6.2)."""
    return " ".join(limpiar_descripcion(descripcion).split(" ")[:3])


def cargar_terminos():
    return {fila["termino"]: fila for fila in leer_planilla(TERMINOS)}


def cargar_entrenamiento():
    """Devuelve (ejemplos, categorías excluidas por M-9 con su cantidad)."""
    excluidas = {familia(fila["descripcion"]) for ruta in (DESARROLLO, PRUEBA) for fila in leer_planilla(ruta)}
    manuales = {fila["codigo"]: fila["categoria"] for fila in leer_planilla(AMBIGUOS)}
    terminos = cargar_terminos()

    ejemplos = []
    for codigo, descripcion, _ in leer_lista(LISTA):
        if not limpiar_descripcion(descripcion) or familia(descripcion) in excluidas:
            continue
        termino = primera_palabra(descripcion)
        if codigo in manuales:
            ejemplos.append(Ejemplo(codigo, descripcion, manuales[codigo], True, termino, familia(descripcion)))
        elif termino in terminos and terminos[termino]["ambiguo"] != "1":
            ejemplos.append(Ejemplo(codigo, descripcion, terminos[termino]["categoria"], False, termino,
                                    familia(descripcion)))

    por_categoria = Counter(e.categoria for e in ejemplos)
    escasas = {c: n for c, n in por_categoria.items() if n < MINIMO_POR_CATEGORIA}
    return [e for e in ejemplos if e.categoria not in escasas], escasas


def cargar_desarrollo():
    """Filas del conjunto de desarrollo con la ruta que seguirían en el sistema: 'termino', 'ambiguo' o 'fuera'."""
    terminos = cargar_terminos()
    filas = []
    for fila in leer_planilla(DESARROLLO):
        termino = terminos.get(primera_palabra(fila["descripcion"]))
        if termino is None:
            ruta = "fuera"
        elif termino["ambiguo"] == "1":
            ruta = "ambiguo"
        else:
            ruta = "termino"
        filas.append({**fila, "ruta": ruta,
                      "categoria_termino": termino["categoria"] if termino and ruta == "termino" else ""})
    return filas


def misma_categoria(a, b):
    return normalizar_texto(a) == normalizar_texto(b)
