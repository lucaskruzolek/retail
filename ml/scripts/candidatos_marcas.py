"""Candidatos de marca, alias y prefijos EAN (Track 6.6, sección 6.1, paso 3).

Uso: python ml/scripts/candidatos_marcas.py ml/datos/lista-mayo-2025.csv
         [--min-frecuencia 10] [--top 500] [--largo-prefijo 7] [--min-soporte 3] [--min-pureza 0.5]

Heurística:
  * Una marca candidata es una palabra que NO es la primera de la descripción (la primera es el término, es decir
    el producto), sin dígitos, de 3 o más letras, que no es una palabra vacía ni un término frecuente
    ("palabras frecuentes que no son términos") y aparece al menos --min-frecuencia veces.
  * Variantes: otras palabras que son una abreviatura de la candidata (p. ej. "FAB." de "FABER") o que
    difieren solo por el punto final. Son sugerencias de alias.
  * Prefijo EAN: los primeros --largo-prefijo dígitos de un GTIN-13 válido (mismo criterio que analizar_ean.py).
    Para cada prefijo se busca la candidata dominante; pureza = artículos del prefijo que la contienen / soporte.

Escribe en ml/datos/: marcas-candidatas.csv (marca_candidata;frecuencia;variantes_detectadas;ejemplo;aprobada;alias)
y prefijos-candidatos.csv (prefijo;marca_candidata;soporte;pureza;aprobado). El estudiante completa aprobada/alias/
aprobado y guarda el resultado como marcas.csv y prefijos.csv.
"""

import argparse
from collections import Counter, defaultdict

from comun import escribir_csv, leer_lista, limpiar_descripcion, normalizar_ean, primera_palabra

parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
parser.add_argument("lista")
parser.add_argument("--min-frecuencia", type=int, default=10)
parser.add_argument("--top", type=int, default=500, help="máximo de marcas candidatas; 0 = todas (500)")
parser.add_argument("--top-terminos", type=int, default=300, help="términos frecuentes que se excluyen (300)")
parser.add_argument("--largo-prefijo", type=int, default=7)
parser.add_argument("--min-soporte", type=int, default=3)
parser.add_argument("--min-pureza", type=float, default=0.5)
parser.add_argument("--salida-marcas", default="ml/datos/marcas-candidatas.csv")
parser.add_argument("--salida-prefijos", default="ml/datos/prefijos-candidatos.csv")
argumentos = parser.parse_args()

# Palabras que nunca son marca: las mismas que usa analizar_ean.py, más conectores y unidades habituales.
PALABRAS_VACIAS = {"X", "C/", "DE", "CON", "P/", "Y", "LA", "EL", "S/", "CM", "MM", "ML", "HJ", "HJS",
                   "AZUL", "NEGRO", "ROJO", "VERDE", "BLANCO", "SURTIDO", "SURTIDA", "COLOR", "COLORES",
                   "PARA", "POR", "SIN", "LOS", "LAS", "UNID", "UNIDAD", "UNIDADES", "GRS", "KG", "LTS"}


def tokens_de(descripcion):
    """Palabras de la descripción sin la primera (el término) y sin el punto final."""
    return [t.rstrip(".") for t in descripcion.split(" ")[1:]]


def es_forma_valida(token):
    return len(token) >= 3 and not any(c.isdigit() for c in token) and token not in PALABRAS_VACIAS


filas = leer_lista(argumentos.lista)
descripciones = [limpiar_descripcion(f[1]) for f in filas]

# Términos frecuentes (primera palabra): no pueden ser marca.
terminos = Counter(primera_palabra(f[1]) for f in filas)
excluidos = {t for t, _ in terminos.most_common(argumentos.top_terminos)}

frecuencia = Counter()
ejemplo = {}
crudas = set()  # palabras tal como aparecen (con el punto), para detectar abreviaturas
for d in descripciones:
    for t in {t for t in tokens_de(d) if es_forma_valida(t) and t not in excluidos}:
        frecuencia[t] += 1
        ejemplo.setdefault(t, d)
    crudas.update(d.split(" ")[1:])

candidatas = [(t, n) for t, n in frecuencia.most_common() if n >= argumentos.min_frecuencia]
if argumentos.top:
    candidatas = candidatas[:argumentos.top]
conjunto_candidatas = {t for t, _ in candidatas}

# Variantes: formas más cortas (abreviaturas de 3 o más letras) o con punto de la misma candidata.
por_inicio = defaultdict(list)
for t in conjunto_candidatas:
    por_inicio[t[:3]].append(t)
variantes = defaultdict(set)
for cruda in crudas:
    limpia = cruda.rstrip(".")
    if len(limpia) < 3 or any(c.isdigit() for c in limpia) or limpia in conjunto_candidatas:
        continue  # una palabra que ya es candidata por derecho propio no es variante de otra
    for candidata in por_inicio.get(limpia[:3], []):
        if cruda != candidata and candidata.startswith(limpia):
            variantes[candidata].add(cruda)

escribir_csv(argumentos.salida_marcas,
             ["marca_candidata", "frecuencia", "variantes_detectadas", "ejemplo", "aprobada", "alias"],
             [[t, n, " | ".join(sorted(variantes[t])), ejemplo[t], "", ""] for t, n in candidatas])

# Prefijos EAN: agrupar los artículos con GTIN-13 válido por sus primeros N dígitos.
grupos = defaultdict(list)
con_ean_valido = 0
for fila, descripcion in zip(filas, descripciones):
    gtin, _ = normalizar_ean(fila[0], fila[2])
    if gtin and len(gtin) == 13:
        con_ean_valido += 1
        grupos[gtin[:argumentos.largo_prefijo]].append(
            {t for t in tokens_de(descripcion) if t in conjunto_candidatas})

prefijos = []
for prefijo, conjuntos in grupos.items():
    soporte = len(conjuntos)
    if soporte < argumentos.min_soporte:
        continue
    cuenta = Counter(t for c in conjuntos for t in c)
    if not cuenta:
        continue
    marca, n = cuenta.most_common(1)[0]
    pureza = n / soporte
    if pureza >= argumentos.min_pureza:
        prefijos.append([prefijo, marca, soporte, f"{pureza:.2f}", ""])
prefijos.sort(key=lambda p: (-p[2], p[0]))
escribir_csv(argumentos.salida_prefijos, ["prefijo", "marca_candidata", "soporte", "pureza", "aprobado"], prefijos)

print(f"Artículos: {len(filas)} | con EAN-13 válido: {con_ean_valido} | "
      f"grupos de prefijo ({argumentos.largo_prefijo} dígitos): {len(grupos)}")
print(f"Marcas candidatas: {len(candidatas)} | con variantes detectadas: {sum(1 for t, _ in candidatas if variantes[t])}")
print(f"Prefijos candidatos (soporte >= {argumentos.min_soporte}, pureza >= {argumentos.min_pureza}): "
      f"{len(prefijos)} | artículos cubiertos: {sum(p[2] for p in prefijos)}")
print(f"Archivos: {argumentos.salida_marcas} | {argumentos.salida_prefijos}")
