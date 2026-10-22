"""Consolida las planillas revisadas en los diccionarios definitivos (Track 6.6, sección 6.1).

Uso: python ml/scripts/consolidar_diccionarios.py ml/datos/lista-mayo-2025.csv
         [--taxonomia ml/datos/taxonomia-borrador.csv] [--terminos ml/datos/terminos-borrador.csv]
         [--marcas ml/datos/marcas-candidatas.csv] [--prefijos ml/datos/prefijos-revision.csv]
         [--destino ml/datos] [--largo-prefijo 7]

Valida las planillas y, solo si no hay errores, escribe en --destino (';', UTF-8 con BOM):
  taxonomia.csv  rubro;categoria
  terminos.csv   termino;rubro;categoria;ambiguo   (ambiguo: 1 o vacío)
  marcas.csv     marca;alias                       (alias separados por |)
  prefijos.csv   prefijo;marca                     (solo los aprobados)
Son los diccionarios que cargará Retail.Cli (M-15): ninguno contiene descripciones del distribuidor.

Reglas que se validan, las mismas que impone la base (Track 6.5, D-4): nombres obligatorios, de hasta 100 caracteres
y únicos sin distinguir mayúsculas ni acentos. Además: cada término apunta a una categoría de la taxonomía, ningún
alias pertenece a dos marcas, ningún prefijo queda pendiente (?) y cada prefijo aprobado apunta a una marca aprobada.

Convenciones de entrada: en marcas-candidatas, aprobada vacía = sí y 0 = no (la columna marca_candidata vale como
nombre aunque se haya reescrito; las filas repetidas de una misma marca se unen). En prefijos-revision, 1 / 0.
"""

import argparse
import sys
from collections import Counter

from comun import escribir_csv, leer_lista, leer_planilla, limpiar_descripcion, marcas_en, normalizar_ean, \
    normalizar_texto, primera_palabra

LARGO_MAXIMO_NOMBRE = 100  # ReglasTexto del dominio (Track 6.5)

parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
parser.add_argument("lista")
parser.add_argument("--taxonomia", default="ml/datos/taxonomia-borrador.csv")
parser.add_argument("--terminos", default="ml/datos/terminos-borrador.csv")
parser.add_argument("--marcas", default="ml/datos/marcas-candidatas.csv")
parser.add_argument("--prefijos", default="ml/datos/prefijos-revision.csv")
parser.add_argument("--destino", default="ml/datos")
parser.add_argument("--largo-prefijo", type=int, default=7)
argumentos = parser.parse_args()

errores = []
advertencias = []


def limpio(texto):
    """Quita espacios sobrantes sin cambiar mayúsculas ni tildes: así se guarda el nombre."""
    return " ".join(texto.split())


def validar_nombre(nombre, que):
    if not nombre:
        errores.append(f"{que}: nombre vacío")
    elif len(nombre) > LARGO_MAXIMO_NOMBRE:
        errores.append(f"{que} '{nombre}': supera {LARGO_MAXIMO_NOMBRE} caracteres")


# 1. Taxonomía: rubro -> categorías. La categoría es única en todo el catálogo, no solo dentro de su rubro.
rubro_de_categoria = {}  # categoría normalizada -> (rubro, categoría) tal como se guardan
rubros = {}
for fila in leer_planilla(argumentos.taxonomia):
    rubro, categoria = limpio(fila["rubro"]), limpio(fila["categoria"])
    validar_nombre(rubro, "Rubro")
    validar_nombre(categoria, "Categoría")
    if rubros.setdefault(normalizar_texto(rubro), rubro) != rubro:
        errores.append(f"Rubro '{rubro}' escrito de dos formas: '{rubros[normalizar_texto(rubro)]}'")
    clave = normalizar_texto(categoria)
    if clave in rubro_de_categoria:
        errores.append(f"Categoría '{categoria}' repetida (rubros '{rubro_de_categoria[clave][0]}' y '{rubro}')")
    rubro_de_categoria[clave] = (rubro, categoria)

# 2. Términos: cada uno apunta a una categoría existente, salvo los ambiguos.
terminos = {}
categorias_usadas = set()
for fila in leer_planilla(argumentos.terminos):
    termino = limpio(fila["termino"]).upper()
    rubro, categoria, ambiguo = limpio(fila["rubro"]), limpio(fila["categoria"]), fila["ambiguo"].strip()
    if ambiguo not in ("", "1"):
        errores.append(f"Término '{termino}': ambiguo debe ser 1 o vacío, no '{ambiguo}'")
    if normalizar_texto(termino) in terminos:
        errores.append(f"Término '{termino}' repetido")
    if categoria:
        clave = normalizar_texto(categoria)
        if clave not in rubro_de_categoria:
            errores.append(f"Término '{termino}': la categoría '{categoria}' no está en la taxonomía")
            continue
        if normalizar_texto(rubro) != normalizar_texto(rubro_de_categoria[clave][0]):
            errores.append(f"Término '{termino}': '{categoria}' pertenece a '{rubro_de_categoria[clave][0]}', "
                           f"no a '{rubro}'")
            continue
        rubro, categoria = rubro_de_categoria[clave]  # se guarda con la escritura de la taxonomía
        categorias_usadas.add(clave)
        if ambiguo:
            advertencias.append(f"Término '{termino}' es ambiguo y tiene categoría: se conserva como orientación; "
                                "el entrenamiento no lo usa como etiqueta")
    elif not ambiguo:
        advertencias.append(f"Término '{termino}' sin categoría y sin marcar como ambiguo: se trata como ambiguo")
        ambiguo = "1"
    terminos[normalizar_texto(termino)] = [termino, rubro, categoria, ambiguo]

for clave, (rubro, categoria) in rubro_de_categoria.items():
    if clave not in categorias_usadas:
        advertencias.append(f"Categoría '{categoria}' ({rubro}) no tiene ningún término")

# 3. Marcas aprobadas: las filas repetidas de una misma marca se unen y sus alias se suman.
marcas = {}  # nombre normalizado -> [nombre, conjunto de alias]
for fila in leer_planilla(argumentos.marcas):
    if fila["aprobada"].strip() != "":
        continue
    nombre = limpio(fila["marca_candidata"])
    validar_nombre(nombre, "Marca")
    marca = marcas.setdefault(normalizar_texto(nombre), [nombre, set()])
    marca[1].update(limpio(alias) for alias in fila["alias"].split("|") if limpio(alias))

forma_a_marca = {}
for clave, (nombre, alias) in marcas.items():
    alias.discard(nombre)
    for forma in [clave, *(normalizar_texto(a) for a in alias)]:
        if forma_a_marca.setdefault(forma, nombre) != nombre:
            errores.append(f"La forma '{forma}' pertenece a dos marcas: '{forma_a_marca[forma]}' y '{nombre}'")

# 4. Prefijos: todos decididos, únicos, numéricos y apuntando a una marca aprobada.
prefijos = {}
for fila in leer_planilla(argumentos.prefijos):
    prefijo = fila["prefijo"].strip().zfill(argumentos.largo_prefijo)  # Excel borra los ceros iniciales
    decision, marca = fila["aprobado"].strip(), limpio(fila["marca"])
    if not prefijo.isdigit() or len(prefijo) != argumentos.largo_prefijo:
        errores.append(f"Prefijo '{prefijo}': debe tener {argumentos.largo_prefijo} dígitos")
    if decision not in ("1", "0"):
        errores.append(f"Prefijo {prefijo}: aprobado debe ser 1 o 0, no '{decision}'")
        continue
    if prefijo in prefijos:
        errores.append(f"Prefijo {prefijo} repetido")
    if decision == "1":
        if normalizar_texto(marca) not in marcas:
            errores.append(f"Prefijo {prefijo}: la marca '{marca}' no está aprobada")
            continue
        prefijos[prefijo] = marcas[normalizar_texto(marca)][0]

# 5. Informe y escritura.
for advertencia in advertencias:
    print(f"ADVERTENCIA  {advertencia}")
for error in errores:
    print(f"ERROR        {error}")
if errores:
    print(f"\n{len(errores)} errores: no se escribió ningún archivo.")
    sys.exit(1)

destino = argumentos.destino.rstrip("/\\")
escribir_csv(f"{destino}/taxonomia.csv", ["rubro", "categoria"],
             sorted(rubro_de_categoria.values(), key=lambda par: (normalizar_texto(par[0]), normalizar_texto(par[1]))))
escribir_csv(f"{destino}/terminos.csv", ["termino", "rubro", "categoria", "ambiguo"], sorted(terminos.values()))
escribir_csv(f"{destino}/marcas.csv", ["marca", "alias"],
             sorted([[nombre, "|".join(sorted(alias))] for nombre, alias in marcas.values()],
                    key=lambda fila: normalizar_texto(fila[0])))
escribir_csv(f"{destino}/prefijos.csv", ["prefijo", "marca"], sorted(prefijos.items()))

print(f"\nRubros: {len(rubros)} | categorías: {len(rubro_de_categoria)} | términos: {len(terminos)} "
      f"(ambiguos: {sum(1 for t in terminos.values() if t[3])}) | marcas: {len(marcas)} "
      f"(alias: {sum(len(a) for _, a in marcas.values())}) | prefijos aprobados: {len(prefijos)}")
print(f"Archivos: {destino}/taxonomia.csv, terminos.csv, marcas.csv, prefijos.csv")

# 6. Cobertura sobre la lista: cuántas filas resuelve cada diccionario por sí solo.
filas = leer_lista(argumentos.lista)
cobertura = Counter()
for fila in filas:
    termino = terminos.get(normalizar_texto(primera_palabra(fila[1])))
    if termino is None:
        cobertura["termino_fuera"] += 1
    elif termino[3]:
        cobertura["termino_ambiguo"] += 1
    else:
        cobertura["termino_con_categoria"] += 1
    por_texto = marcas_en(limpiar_descripcion(fila[1]), forma_a_marca)
    gtin, _ = normalizar_ean(fila[0], fila[2])
    por_prefijo = prefijos.get(gtin[:argumentos.largo_prefijo]) if gtin and len(gtin) == 13 else None
    cobertura["marca_texto"] += bool(por_texto)
    cobertura["marca_prefijo"] += por_prefijo is not None
    cobertura["marca_alguna"] += bool(por_texto) or por_prefijo is not None
    cobertura["marca_desacuerdo"] += bool(por_texto) and por_prefijo is not None and por_prefijo not in por_texto

total = len(filas)


def porcentaje(clave):
    return f"{cobertura[clave]:>6} ({cobertura[clave] / total:6.1%})"


print(f"\nCobertura sobre {total} filas")
print(f"  Categoría por término ........ {porcentaje('termino_con_categoria')}")
print(f"  Término ambiguo .............. {porcentaje('termino_ambiguo')}")
print(f"  Término fuera del diccionario  {porcentaje('termino_fuera')}")
print(f"  Marca por texto .............. {porcentaje('marca_texto')}")
print(f"  Marca por prefijo EAN ........ {porcentaje('marca_prefijo')}")
print(f"  Marca por alguno de los dos .. {porcentaje('marca_alguna')}")
print(f"  Texto y prefijo no coinciden . {porcentaje('marca_desacuerdo')}")
