"""Calidad de los códigos de barras y relación entre el prefijo del titular y la marca.

Uso: python ml/scripts/analizar_ean.py ml/datos/lista-mayo-2025.csv
"""

import sys
from collections import Counter, defaultdict

from comun import leer_lista, limpiar_descripcion, normalizar_ean

# Palabras que nunca son marca: conectores, unidades y colores frecuentes.
PALABRAS_VACIAS = {"X", "C/", "DE", "CON", "P/", "Y", "LA", "EL", "S/", "CM", "MM", "ML", "HJ", "HJS",
                   "AZUL", "NEGRO", "ROJO", "VERDE", "BLANCO", "SURTIDO", "SURTIDA", "COLOR", "COLORES"}


def palabras_candidatas(descripcion):
    """Palabras que podrían ser marca: no son la primera, no tienen dígitos y no son vacías."""
    tokens = descripcion.split(" ")[1:]
    return {t for t in tokens if len(t) >= 3 and not any(c.isdigit() for c in t) and t not in PALABRAS_VACIAS}


def pais(gtin):
    prefijo = gtin[:3]
    if prefijo in ("778", "779"):
        return "Argentina 778-779"
    if "690" <= prefijo <= "699":
        return "China 690-699"
    if "300" <= prefijo <= "379":
        return "Francia 300-379"
    if prefijo in ("789", "790"):
        return "Brasil 789-790"
    if prefijo <= "139":
        return "EEUU/Canadá 000-139"
    if "840" <= prefijo <= "849":
        return "España 840-849"
    if prefijo == "745":
        return "Panamá 745"
    return "otros"


filas = leer_lista(sys.argv[1])
estados = Counter()
validos = []
for fila in filas:
    gtin, estado = normalizar_ean(fila[0], fila[2])
    estados[estado] += 1
    if gtin:
        validos.append((gtin, limpiar_descripcion(fila[1])))

print(f"Filas: {len(filas)}")
for estado, n in estados.most_common():
    print(f"  {estado:30} {n:6} ({n / len(filas):.1%})")

print("\nOrigen por prefijo de país (sobre los válidos):")
for origen, n in Counter(pais(g) for g, _ in validos).most_common():
    print(f"  {origen:22} {n:6}")

for largo in (7, 8, 9):
    grupos = defaultdict(list)
    for gtin, descripcion in validos:
        grupos[gtin[:largo]].append(descripcion)
    filas_puras = 0
    for descripciones in grupos.values():
        if len(descripciones) < 3:
            continue
        cuenta = Counter(t for d in descripciones for t in palabras_candidatas(d))
        if cuenta and cuenta.most_common(1)[0][1] / len(descripciones) >= 0.8:
            filas_puras += len(descripciones)
    print(f"\nPrefijo de {largo} dígitos: {len(grupos)} grupos; filas en grupos con una palabra "
          f"dominante (>= 80 %): {filas_puras} ({filas_puras / len(validos):.1%} de los válidos)")

grupos = defaultdict(list)
for gtin, descripcion in validos:
    grupos[gtin[:7]].append(descripcion)
print("\nTop 30 prefijos de 7 dígitos con su palabra dominante:")
for prefijo, descripciones in sorted(grupos.items(), key=lambda par: -len(par[1]))[:30]:
    cuenta = Counter(t for d in descripciones for t in palabras_candidatas(d))
    palabra, n = cuenta.most_common(1)[0] if cuenta else ("-", 0)
    print(f"  {prefijo}  {len(descripciones):5}  {palabra:15} {n / len(descripciones):.0%}")
