"""Prefijos EAN por marca aprobada: segunda pasada del paso 3 (Track 6.6, sección 6.1).

Uso: python ml/scripts/prefijos_por_marca.py ml/datos/lista-mayo-2025.csv
         [--marcas ml/datos/marcas-candidatas.csv] [--prefijos ml/datos/prefijos-candidatos.csv]
         [--salida ml/datos/prefijos-revision.csv] [--largo-prefijo 7] [--min-soporte 3] [--min-pureza 0.5]

Por qué existe: candidatos_marcas.py busca en cada prefijo la palabra candidata más frecuente. Cuando una palabra
genérica (ACRILICA, PASTEL) supera a la marca, la marca queda oculta: es lo que le pasó a AD. Esta pasada repite el
cálculo contando solo las marcas que el estudiante aprobó (nombre y alias, también de varias palabras), así que
ninguna palabra genérica compite.

Entrada: las planillas revisadas a mano, con su convención (aprobada/aprobado vacío = sí, 0 = no). En marcas, la
columna marca_candidata vale como nombre de la marca aunque el estudiante la haya reescrito.

Salida: prefijos-revision.csv (prefijo;marca;soporte;pureza;aprobado;origen), con la decisión explícita:
  aprobado  1 = sí, 0 = no, ? = pendiente: lo decide el estudiante
  origen    conservado = mismo prefijo y misma marca que en la revisión anterior: se copia la decisión
            reasignado = el prefijo estaba aprobado con una palabra que no es la marca; ahora apunta a una aprobada
            nuevo      = el prefijo no estaba, o estaba rechazado con otra palabra
Las filas pendientes van primero.
"""

import argparse
from collections import Counter, defaultdict

from comun import escribir_csv, leer_lista, leer_planilla, limpiar_descripcion, marcas_en, normalizar_ean, \
    normalizar_texto

parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
parser.add_argument("lista")
parser.add_argument("--marcas", default="ml/datos/marcas-candidatas.csv")
parser.add_argument("--prefijos", default="ml/datos/prefijos-candidatos.csv")
parser.add_argument("--salida", default="ml/datos/prefijos-revision.csv")
parser.add_argument("--largo-prefijo", type=int, default=7)
parser.add_argument("--min-soporte", type=int, default=3)
parser.add_argument("--min-pureza", type=float, default=0.5)
argumentos = parser.parse_args()


def aprobada_a_mano(valor):
    """Convención de las planillas revisadas por el estudiante: celda vacía = aprobada, 0 = rechazada."""
    return valor.strip() == ""


# 1. Formas de texto de cada marca aprobada (nombre y alias) -> nombre de la marca.
forma_a_marca = {}
conflictos = set()
for fila in leer_planilla(argumentos.marcas):
    if not aprobada_a_mano(fila["aprobada"]):
        continue
    nombre = fila["marca_candidata"].strip()
    for forma in [nombre, *fila["alias"].split("|")]:
        forma = normalizar_texto(forma)
        if not forma:
            continue
        if forma_a_marca.setdefault(forma, nombre) != nombre:
            conflictos.add(forma)  # la misma forma escrita para dos marcas distintas

# 2. Revisión anterior de prefijos. Excel borra los ceros iniciales: se restituyen con zfill.
anteriores = {}
for fila in leer_planilla(argumentos.prefijos):
    prefijo = fila["prefijo"].strip().zfill(argumentos.largo_prefijo)
    anteriores[prefijo] = (forma_a_marca.get(normalizar_texto(fila["marca_candidata"])),
                           aprobada_a_mano(fila["aprobado"]))

# 3. Agrupar los artículos con GTIN-13 válido por prefijo (mismo criterio que candidatos_marcas.py).
filas = leer_lista(argumentos.lista)
grupos = defaultdict(list)
apariciones = Counter()
for fila in filas:
    encontradas = marcas_en(limpiar_descripcion(fila[1]), forma_a_marca)
    apariciones.update(encontradas)
    gtin, _ = normalizar_ean(fila[0], fila[2])
    if gtin and len(gtin) == 13:
        grupos[gtin[:argumentos.largo_prefijo]].append(encontradas)

# 4. Marca dominante de cada prefijo y decisión, arrastrando la revisión anterior.
salida = []
vistos = set()
for prefijo, conjuntos in grupos.items():
    soporte = len(conjuntos)
    cuenta = Counter(m for c in conjuntos for m in c)
    if soporte < argumentos.min_soporte or not cuenta:
        continue
    marca, n = min(cuenta.items(), key=lambda par: (-par[1], par[0]))  # empate: la primera alfabéticamente
    if n / soporte < argumentos.min_pureza:
        continue
    vistos.add(prefijo)
    if prefijo not in anteriores:
        decision, origen = "?", "nuevo"
    else:
        marca_anterior, aprobado_antes = anteriores[prefijo]
        if marca_anterior == marca:
            decision, origen = ("1" if aprobado_antes else "0"), "conservado"
        elif aprobado_antes:
            decision, origen = "1", "reasignado"
        else:
            decision, origen = "?", "nuevo"
    salida.append([prefijo, marca, soporte, f"{n / soporte:.2f}", decision, origen])

salida.sort(key=lambda p: (p[4] != "?", -p[2], p[0]))
escribir_csv(argumentos.salida, ["prefijo", "marca", "soporte", "pureza", "aprobado", "origen"], salida)

# 5. Resumen (solo conteos y nombres de marca; las descripciones quedan en ml/datos/).
resumen = Counter((p[5], p[4]) for p in salida)
print(f"Marcas aprobadas: {len(set(forma_a_marca.values()))} | formas de texto (nombre + alias): {len(forma_a_marca)}")
if conflictos:
    print(f"ATENCIÓN, formas asignadas a dos marcas: {sorted(conflictos)}")
sin_aparicion = sorted(set(forma_a_marca.values()) - set(apariciones))
print(f"Marcas aprobadas que no aparecen en ninguna descripción: {sin_aparicion or 'ninguna'}")
print(f"Prefijos (soporte >= {argumentos.min_soporte}, pureza >= {argumentos.min_pureza}): {len(salida)}")
for (origen, decision), cantidad in sorted(resumen.items()):
    print(f"  {origen:<11} aprobado={decision}: {cantidad}")
perdidos = Counter(aprobado for prefijo, (_, aprobado) in anteriores.items() if prefijo not in vistos)
print(f"Prefijos de la revisión anterior sin marca aprobada dominante: {sum(perdidos.values())} "
      f"(estaban aprobados: {perdidos[True]}, rechazados: {perdidos[False]})")
print(f"Artículos cubiertos por prefijos aprobados: {sum(p[2] for p in salida if p[4] == '1')}")
print(f"Archivo: {argumentos.salida}")
