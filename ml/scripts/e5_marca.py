"""Experimento E-5 del Track 6.6: orden entre texto y prefijo EAN para sugerir la marca (M-17), sobre el conjunto de
desarrollo (M-20). Solo biblioteca estándar.

Uso: python ml/scripts/e5_marca.py

Estrategias (sobre marcas.csv y los prefijos aprobados de prefijos-revision.csv, que guarda la pureza):
  prefijo primero  prefijo aprobado con pureza >= p -> su marca; si no, el texto: una marca -> esa; varias -> nada.
  texto primero    el texto nombra una marca -> esa; varias -> la del prefijo si está entre ellas, si no nada;
                   ninguna -> el prefijo (pureza >= p).
Medición (M-19: el oro se etiquetó leyendo solo el texto):
  * filas con marca en el oro: la sugerencia es correcta o no; definen precisión y cobertura;
  * filas SIN MARCA: una sugerencia por texto es un error verificable; una por prefijo NO es verificable (la marca
    puede existir y no estar escrita), así que se informa aparte y no entra en la precisión.
Regla: máxima cobertura con precisión >= 95 % (M-1); ante un empate práctico (< 1 punto), texto primero.
"""

import math

from comun import leer_planilla, marcas_en, normalizar_ean, normalizar_texto

PRECISION_OBJETIVO = 0.95
PUREZAS = (0.5, 0.6, 0.7, 0.8, 0.9, 1.01)  # 1.01 = sin prefijos
SIN_MARCA = "SIN MARCA"

forma_a_marca = {}
for fila in leer_planilla("ml/datos/marcas.csv"):
    for forma in [fila["marca"], *(a for a in fila["alias"].split("|") if a)]:
        forma_a_marca[normalizar_texto(forma)] = fila["marca"]
pureza_de = {}
for fila in leer_planilla("ml/datos/prefijos-revision.csv"):
    if fila["aprobado"].strip() == "1":
        pureza_de[fila["prefijo"].zfill(7)] = (fila["marca"], float(fila["pureza"]))
aprobados = {fila["prefijo"]: fila["marca"] for fila in leer_planilla("ml/datos/prefijos.csv")}
pureza_de = {p: (aprobados[p], pureza) for p, (_, pureza) in pureza_de.items() if p in aprobados}

desarrollo = []
for fila in leer_planilla("ml/datos/oro.csv"):
    gtin, _ = normalizar_ean(fila["codigo"], fila["ean"]) if fila["ean"] else (None, "")
    desarrollo.append((marcas_en(fila["descripcion"], forma_a_marca), (gtin or "")[:7], fila["marca"]))


def sugerir(estrategia, en_texto, prefijo, pureza_minima):
    """Devuelve (marca, origen) u (None, None)."""
    por_prefijo = None
    if prefijo in pureza_de and pureza_de[prefijo][1] >= pureza_minima:
        por_prefijo = pureza_de[prefijo][0]
    if estrategia == "prefijo primero":
        if por_prefijo:
            return por_prefijo, "prefijo"
        return (next(iter(en_texto)), "texto") if len(en_texto) == 1 else (None, None)
    if len(en_texto) == 1:
        return next(iter(en_texto)), "texto"
    if len(en_texto) > 1:
        return (por_prefijo, "texto+prefijo") if por_prefijo in en_texto else (None, None)
    return (por_prefijo, "prefijo") if por_prefijo else (None, None)


def intervalo_wilson(exitos, total, z=1.96):
    if total == 0:
        return 0.0, 1.0
    p = exitos / total
    centro = (p + z * z / (2 * total)) / (1 + z * z / total)
    margen = z * math.sqrt(p * (1 - p) / total + z * z / (4 * total * total)) / (1 + z * z / total)
    return max(0.0, centro - margen), min(1.0, centro + margen)


con_marca = sum(1 for _, _, real in desarrollo if real != SIN_MARCA)
print(f"Desarrollo: {len(desarrollo)} filas | con marca en el oro: {con_marca} | SIN MARCA: {len(desarrollo) - con_marca}"
      f" | prefijos aprobados con pureza: {len(pureza_de)}\n")
print("| Estrategia | Pureza mínima | Sugeridas (con marca) | Cobertura | Precisión | IC 95 % | "
      "Errores en SIN MARCA (texto) | No verificables (prefijo en SIN MARCA) |")
print("| :--- | ---: | ---: | ---: | ---: | :--- | ---: | ---: |")
resultados = []
for estrategia in ("prefijo primero", "texto primero"):
    for pureza in PUREZAS:
        sugeridas = sugeridas_con_marca = correctas = errores_sin_marca = no_verificables = 0
        for en_texto, prefijo, real in desarrollo:
            marca, origen = sugerir(estrategia, en_texto, prefijo, pureza)
            if marca is None:
                continue
            if real == SIN_MARCA:
                if origen == "prefijo":
                    no_verificables += 1
                else:
                    errores_sin_marca += 1
                    sugeridas += 1  # un error verificable cuenta en la precisión
                continue
            sugeridas += 1
            sugeridas_con_marca += 1
            correctas += normalizar_texto(marca) == normalizar_texto(real)
        precision = correctas / sugeridas if sugeridas else 0.0
        cobertura = sugeridas_con_marca / con_marca  # filas con marca en el oro que reciben una sugerencia
        inferior, superior = intervalo_wilson(correctas, sugeridas)
        etiqueta = "sin prefijos" if pureza > 1 else f"{pureza:.1f}"
        resultados.append((estrategia, etiqueta, precision, cobertura, pureza))
        print(f"| {estrategia} | {etiqueta} | {sugeridas_con_marca} | {cobertura:.1%} | {precision:.1%} | "
              f"{inferior:.1%} a {superior:.1%} | {errores_sin_marca} | {no_verificables} |")

validas = [r for r in resultados if r[2] >= PRECISION_OBJETIVO]
if not validas:
    print(f"\nNinguna combinación alcanza {PRECISION_OBJETIVO:.0%} de precisión.")
else:
    # Máxima cobertura; ante empate, texto primero; después mayor precisión; después la pureza más exigente (deja
    # menos sugerencias por prefijo sin verificar).
    elegida = max(validas, key=lambda r: (round(r[3], 6), r[0] == "texto primero", round(r[2], 6), r[4]))
    print(f"\nE-5: {elegida[0]}, pureza mínima {elegida[1]} -> cobertura {elegida[3]:.1%}, precisión {elegida[2]:.1%}"
          " (regla: máxima cobertura con precisión >= 95 %; empate < 1 punto -> texto primero)")
