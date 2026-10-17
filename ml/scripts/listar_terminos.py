"""Lista de términos (primera palabra de la descripción) con frecuencia y ejemplos, para que el estudiante
los asigne a rubro y categoría (Track 6.6, sección 6.1, pasos 1 y 2).

Uso: python ml/scripts/listar_terminos.py ml/datos/lista-mayo-2025.csv [--top 300] [--salida ruta.csv]

Escribe ml/datos/terminos-candidatos.csv con: termino;frecuencia;ejemplo1;ejemplo2;ejemplo3;rubro;categoria;ambiguo
El estudiante completa las tres últimas columnas; al terminar, guarda el resultado como terminos.csv.
"""

import argparse
from collections import Counter, defaultdict

from comun import escribir_csv, leer_lista, limpiar_descripcion, primera_palabra

parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
parser.add_argument("lista")
parser.add_argument("--top", type=int, default=300, help="cantidad de términos a escribir; 0 = todos (300)")
parser.add_argument("--salida", default="ml/datos/terminos-candidatos.csv")
argumentos = parser.parse_args()

CANTIDAD_EJEMPLOS = 3

filas = leer_lista(argumentos.lista)
frecuencias = Counter()
ejemplos = defaultdict(list)
for fila in filas:
    descripcion = limpiar_descripcion(fila[1])
    termino = primera_palabra(fila[1])
    if not termino:
        continue
    frecuencias[termino] += 1
    # Ejemplos distintos entre sí: tres variantes de color de lo mismo no enseñan nada.
    if len(ejemplos[termino]) < CANTIDAD_EJEMPLOS and descripcion not in ejemplos[termino]:
        ejemplos[termino].append(descripcion)

ordenados = frecuencias.most_common()
elegidos = ordenados if argumentos.top == 0 else ordenados[:argumentos.top]

salida = []
for termino, frecuencia in elegidos:
    muestra = ejemplos[termino] + [""] * (CANTIDAD_EJEMPLOS - len(ejemplos[termino]))
    salida.append([termino, frecuencia, *muestra, "", "", ""])
escribir_csv(argumentos.salida,
             ["termino", "frecuencia", "ejemplo1", "ejemplo2", "ejemplo3", "rubro", "categoria", "ambiguo"],
             salida)

total = sum(frecuencias.values())
cubierto = sum(n for _, n in elegidos)
print(f"Artículos: {total} | términos distintos: {len(frecuencias)}")
print(f"Términos escritos: {len(elegidos)} | cobertura acumulada: {cubierto / total:.1%} de los artículos")
print(f"Archivo: {argumentos.salida}")
