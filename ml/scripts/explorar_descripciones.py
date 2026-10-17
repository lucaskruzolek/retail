"""Exploración de las descripciones: cobertura de la primera palabra, abreviaturas, truncado y familias.

Uso: python ml/scripts/explorar_descripciones.py ml/datos/lista-mayo-2025.csv
"""

import sys
from collections import Counter

from comun import leer_lista, limpiar_descripcion

filas = leer_lista(sys.argv[1])
descripciones = [limpiar_descripcion(fila[1]) for fila in filas]
largo_maximo = max(len(d) for d in descripciones)

print(f"Filas: {len(filas)} | descripciones distintas: {len(set(descripciones))}")
print(f"Largo máximo: {largo_maximo} | filas en el tope (posible truncado): "
      f"{sum(1 for d in descripciones if len(d) >= largo_maximo - 1)}")

primeras = Counter(d.split(" ")[0] for d in descripciones if d)
total = sum(primeras.values())
print(f"\nPrimeras palabras distintas: {len(primeras)}")
print(f"Con una sola aparición: {sum(1 for n in primeras.values() if n == 1)}")
print(f"Empiezan con número: {sum(n for p, n in primeras.items() if p[0].isdigit())}")
print(f"Abreviadas con punto: {sum(n for p, n in primeras.items() if '.' in p)}")

ordenadas = primeras.most_common()
print("\nCobertura acumulada de las N primeras palabras más frecuentes:")
for corte in (10, 25, 50, 100, 200, 300, 500):
    cubierto = sum(n for _, n in ordenadas[:corte])
    print(f"  top {corte:4}: {cubierto / total:.1%}")

print("\nTop 60 primeras palabras:")
for palabra, n in ordenadas[:60]:
    print(f"  {palabra:20} {n}")

familias = Counter(" ".join(d.split(" ")[:3]) for d in descripciones)
print(f"\nFamilias (tres primeras palabras): {len(familias)} | con 10 o más filas: "
      f"{sum(1 for n in familias.values() if n >= 10)}")
