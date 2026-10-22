"""Cierra el etiquetado: valida las planillas y escribe oro.csv y ambiguos.csv (Track 6.6, secciones 6.2 y M-19).

Uso: python ml/scripts/cerrar_etiquetado.py
         [--oro ml/datos/oro-validar.csv]
         [--ambiguos ml/datos/ambiguos-a-etiquetar.csv ml/datos/ambiguos-2-a-etiquetar.csv]
         [--taxonomia ml/datos/taxonomia.csv] [--marcas ml/datos/marcas.csv] [--destino ml/datos]

Valida, y solo si no hay errores escribe (';', UTF-8 con BOM):
  oro.csv       codigo;descripcion;ean;categoria;marca   (evaluación final; nunca para entrenar)
  ambiguos.csv  codigo;descripcion;termino;categoria     (ejemplos de entrenamiento de términos ambiguos)

Errores (no se escribe nada): filas sin validar (en el oro, y en las planillas de ambiguos que tengan la columna
`validado`), celdas vacías, categorías que no están en la taxonomía,
códigos repetidos y familias (tres primeras palabras) presentes en las dos planillas.
Informe: tasa de corrección del validador sobre las etiquetas de Claude (M-19), con su intervalo de confianza, y las
marcas del oro que no están en el diccionario (el sistema no puede sugerirlas: cuentan como fallo de cobertura).
"""

import argparse
import math
import sys
from collections import Counter

from comun import escribir_csv, leer_planilla, normalizar_texto

SIN_MARCA = "SIN MARCA"

parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
parser.add_argument("--oro", default="ml/datos/oro-validar.csv")
parser.add_argument("--ambiguos", nargs="+",
                    default=["ml/datos/ambiguos-a-etiquetar.csv", "ml/datos/ambiguos-2-a-etiquetar.csv"])
parser.add_argument("--taxonomia", default="ml/datos/taxonomia.csv")
parser.add_argument("--marcas", default="ml/datos/marcas.csv")
parser.add_argument("--destino", default="ml/datos")
argumentos = parser.parse_args()

errores = []
categorias = {normalizar_texto(f["categoria"]): f["categoria"] for f in leer_planilla(argumentos.taxonomia)}
marcas = {normalizar_texto(f["marca"]): f["marca"] for f in leer_planilla(argumentos.marcas)}


def familia(descripcion):
    return " ".join(descripcion.split(" ")[:3])


def categoria_valida(valor, donde):
    """Devuelve la categoría con la escritura de la taxonomía, o None (y registra el error)."""
    if not valor.strip():
        errores.append(f"{donde}: categoría vacía")
        return None
    if normalizar_texto(valor) not in categorias:
        errores.append(f"{donde}: la categoría '{valor}' no está en la taxonomía")
        return None
    return categorias[normalizar_texto(valor)]


def intervalo_wilson(exitos, total, z=1.96):
    """Intervalo de confianza del 95 % para una proporción; se comporta bien con proporciones cercanas a 0."""
    p = exitos / total
    centro = (p + z * z / (2 * total)) / (1 + z * z / total)
    margen = z * math.sqrt(p * (1 - p) / total + z * z / (4 * total * total)) / (1 + z * z / total)
    return max(0.0, centro - margen), min(1.0, centro + margen)


# 1. Conjunto de oro validado.
oro = []
corregidas = Counter()
fuera_del_diccionario = Counter()
for fila in leer_planilla(argumentos.oro):
    donde = f"Oro {fila['codigo']}"
    if fila["validado"].strip() != "1":
        errores.append(f"{donde}: sin validar")
    categoria = categoria_valida(fila["categoria"], donde)
    marca = " ".join(fila["marca"].split())
    if not marca:
        errores.append(f"{donde}: marca vacía (usar {SIN_MARCA})")
    elif normalizar_texto(marca) == normalizar_texto(SIN_MARCA):
        marca = SIN_MARCA
    elif normalizar_texto(marca) in marcas:
        marca = marcas[normalizar_texto(marca)]
    else:
        fuera_del_diccionario[marca] += 1
    if categoria is None or not marca:
        continue
    corrigio_categoria = normalizar_texto(categoria) != normalizar_texto(fila["categoria_claude"])
    corrigio_marca = normalizar_texto(marca) != normalizar_texto(fila["marca_claude"])
    corregidas["categoria"] += corrigio_categoria
    corregidas["marca"] += corrigio_marca
    corregidas["fila"] += corrigio_categoria or corrigio_marca
    oro.append([fila["codigo"], fila["descripcion"], fila["ean"], categoria, marca])

# 2. Ejemplos de términos ambiguos.
ambiguos = []
for ruta in argumentos.ambiguos:
    for fila in leer_planilla(ruta):
        donde = f"Ambiguo {fila['codigo']}"
        if "validado" in fila and fila["validado"].strip() != "1":
            errores.append(f"{donde}: sin validar")
        categoria = categoria_valida(fila["categoria"], donde)
        if categoria is not None:
            ambiguos.append([fila["codigo"], fila["descripcion"], fila["termino"], categoria])

# 3. Independencia entre conjuntos (sección 6.2).
codigos = Counter(f[0] for f in oro + ambiguos)
errores.extend(f"Código {codigo} repetido" for codigo, n in codigos.items() if n > 1)
compartidas = {familia(f[1]) for f in oro} & {familia(f[1]) for f in ambiguos}
errores.extend(f"Familia '{f}' en el oro y en los ambiguos" for f in sorted(compartidas))

for error in errores:
    print(f"ERROR  {error}")
if errores:
    print(f"\n{len(errores)} errores: no se escribió ningún archivo.")
    sys.exit(1)

destino = argumentos.destino.rstrip("/\\")
escribir_csv(f"{destino}/oro.csv", ["codigo", "descripcion", "ean", "categoria", "marca"], oro)
escribir_csv(f"{destino}/ambiguos.csv", ["codigo", "descripcion", "termino", "categoria"], ambiguos)

total = len(oro)
print(f"Oro: {total} filas | ambiguos: {len(ambiguos)} filas | familias compartidas: 0")
print("Correcciones del validador sobre las etiquetas de Claude (M-19; cota inferior del error):")
for campo in ("categoria", "marca", "fila"):
    inferior, superior = intervalo_wilson(corregidas[campo], total)
    print(f"  {campo:<9} {corregidas[campo]:>3} de {total} = {corregidas[campo] / total:5.1%} "
          f"(IC 95 %: {inferior:.1%} a {superior:.1%})")
print(f"Marca {SIN_MARCA}: {sum(1 for f in oro if f[4] == SIN_MARCA)} | marcas fuera del diccionario: "
      f"{sum(fuera_del_diccionario.values())} filas, {len(fuera_del_diccionario)} marcas")
print(f"Archivos: {destino}/oro.csv, {destino}/ambiguos.csv")
