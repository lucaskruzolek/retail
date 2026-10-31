"""Semilla de pares "término ambiguo + segunda palabra" a partir de los ejemplos validados (Track 6.6, Fase 5, paso 5.7).

Uso: python ml/scripts/consolidar_pares.py [--ambiguos ml/datos/ambiguos.csv] [--terminos ml/datos/terminos.csv]
         [--taxonomia ml/datos/taxonomia.csv] [--planilla ml/datos/pares-a-validar.csv] [--destino ml/datos/pares.csv]
         [--cerrar]

Sin --cerrar, escribe --planilla (';', UTF-8 con BOM) para que el estudiante la valide:
  termino;segunda_palabra;categoria;soporte;revisar;observacion;ejemplo;validado
Un par es candidato si TODOS sus ejemplos tienen la misma categoría (unanimidad, M-58): sin umbrales. Si sus ejemplos
no coinciden, entra sin categoría y marcado "conflicto", con las categorías en disputa, y lo decide una persona. Solo se
usan los términos que siguen siendo ambiguos en --terminos: por P-3, un par de un término con categoría no puede
existir. La segunda palabra es la de e6_comun.segunda_palabra, la misma que reproduce C# con paridad.
validado viene en 1 para los pares con dos o más ejemplos unánimes y vacío para los que hay que revisar; ejemplo
muestra descripciones del distribuidor para validar con contexto (ml/datos no se versiona).

Con --cerrar, valida la planilla y, solo si no hay errores, escribe --destino: termino;segunda_palabra;categoria con
los pares en 1. Es la semilla que cargará Retail.Cli (paso 6.1, M-15): no contiene descripciones del distribuidor.
Se valida lo mismo que impone el Dominio: validado en 1 o 0, categoría de la taxonomía, término ambiguo, segunda
palabra válida (ReglasClasificacion.ExigirSegundaPalabra) y ningún par repetido.

Solo imprime conteos agregados (regla 6 del handoff). Biblioteca estándar: no requiere ml/.venv.
"""

import argparse
import sys
from collections import Counter, defaultdict

from comun import escribir_csv, leer_planilla, normalizar_texto
from e6_comun import RELLENO, segunda_palabra

LONGITUD_MAXIMA = 50  # ReglasClasificacion.LongitudMaximaTermino
EJEMPLOS_POR_CATEGORIA = 1
ENCABEZADO = ["termino", "segunda_palabra", "categoria", "soporte", "revisar", "observacion", "ejemplo", "validado"]

parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
parser.add_argument("--ambiguos", default="ml/datos/ambiguos.csv")
parser.add_argument("--terminos", default="ml/datos/terminos.csv")
parser.add_argument("--taxonomia", default="ml/datos/taxonomia.csv")
parser.add_argument("--planilla", default="ml/datos/pares-a-validar.csv")
parser.add_argument("--destino", default="ml/datos/pares.csv")
parser.add_argument("--cerrar", action="store_true", help="valida la planilla revisada y escribe la semilla")
argumentos = parser.parse_args()


def terminos_ambiguos(ruta):
    return {normalizar_texto(fila["termino"]) for fila in leer_planilla(ruta) if fila["ambiguo"].strip() == "1"}


def categorias_de_la_taxonomia(ruta):
    """Forma de comparación → nombre como figura en la taxonomía."""
    return {normalizar_texto(fila["categoria"]): fila["categoria"].strip() for fila in leer_planilla(ruta)}


def es_segunda_palabra_valida(forma):
    """La regla de ReglasClasificacion.ExigirSegundaPalabra sobre una forma ya normalizada."""
    return (0 < len(forma) <= LONGITUD_MAXIMA and " " not in forma
            and sum(c.isalpha() for c in forma) >= 2 and forma not in RELLENO)


def generar():
    ambiguos = terminos_ambiguos(argumentos.terminos)
    categorias = categorias_de_la_taxonomia(argumentos.taxonomia)
    filas = leer_planilla(argumentos.ambiguos)

    ejemplos = defaultdict(lambda: defaultdict(list))
    fuera = Counter()
    for fila in filas:
        termino = normalizar_texto(fila["termino"])
        segunda = segunda_palabra(fila["descripcion"])
        if termino not in ambiguos:
            fuera["término que ya no es ambiguo"] += 1
            continue
        if not segunda:
            fuera["sin segunda palabra"] += 1
            continue
        if normalizar_texto(fila["categoria"]) not in categorias:
            fuera["categoría fuera de la taxonomía"] += 1
            continue
        ejemplos[(termino, segunda)][categorias[normalizar_texto(fila["categoria"])]].append(fila["descripcion"])

    salida = []
    conteo = Counter()
    for (termino, segunda), por_categoria in sorted(ejemplos.items()):
        soporte = sum(len(d) for d in por_categoria.values())
        muestra = " | ".join(d for descripciones in por_categoria.values() for d in descripciones[:EJEMPLOS_POR_CATEGORIA])
        if len(por_categoria) > 1:
            disputa = " | ".join(f"{c} {len(d)}" for c, d in sorted(por_categoria.items(), key=lambda x: -len(x[1])))
            salida.append([termino, segunda, "", soporte, f"conflicto: {disputa}", "", muestra, ""])
            conteo["en conflicto"] += 1
        elif soporte == 1:
            salida.append([termino, segunda, next(iter(por_categoria)), soporte, "un solo ejemplo", "", muestra, ""])
            conteo["un solo ejemplo"] += 1
        else:
            salida.append([termino, segunda, next(iter(por_categoria)), soporte, "", "", muestra, "1"])
            conteo["unánimes con 2 o más ejemplos"] += 1

    escribir_csv(argumentos.planilla, ENCABEZADO, salida)
    print(f"{len(filas)} ejemplos | descartados: {dict(fuera)}")
    print(f"{len(salida)} pares: {dict(conteo)} | {argumentos.planilla}")


def cerrar():
    ambiguos = terminos_ambiguos(argumentos.terminos)
    categorias = categorias_de_la_taxonomia(argumentos.taxonomia)
    filas = leer_planilla(argumentos.planilla)

    errores = Counter()
    vistos = set()
    semilla = []
    descartados = 0
    for numero, fila in enumerate(filas, start=2):
        validado = (fila.get("validado") or "").strip()
        if validado == "0":
            descartados += 1
            continue
        if validado != "1":
            errores["validado vacío o distinto de 1 / 0 (pendiente)"] += 1
            continue

        termino = normalizar_texto(fila["termino"])
        segunda = normalizar_texto(fila["segunda_palabra"]).rstrip(".")
        categoria = categorias.get(normalizar_texto(fila["categoria"] or ""))
        if termino not in ambiguos:
            errores["término que no es ambiguo"] += 1
        elif not es_segunda_palabra_valida(segunda):
            errores["segunda palabra inválida"] += 1
        elif categoria is None:
            errores["categoría vacía o fuera de la taxonomía"] += 1
        elif (termino, segunda) in vistos:
            errores["par repetido"] += 1
        else:
            vistos.add((termino, segunda))
            semilla.append([termino, segunda, categoria])

    if errores:
        print(f"No se escribió {argumentos.destino}. Errores: {dict(errores)}")
        sys.exit(1)

    escribir_csv(argumentos.destino, ["termino", "segunda_palabra", "categoria"], semilla)
    print(f"{len(semilla)} pares en la semilla | {descartados} descartados | {argumentos.destino}")


if argumentos.cerrar:
    cerrar()
else:
    generar()
