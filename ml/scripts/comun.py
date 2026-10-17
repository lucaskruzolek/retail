"""Funciones compartidas por los scripts de exploración del Track 6.6.

Solo usa la biblioteca estándar de Python: no hace falta instalar paquetes.
"""

import csv
import re


def leer_lista(ruta):
    """Lee el CSV exportado por exportar_lista_xls.ps1 (código;descripción;EAN) y descarta el encabezado."""
    with open(ruta, encoding="utf-8") as archivo:
        filas = [fila for fila in csv.reader(archivo, delimiter=";") if fila]
    return filas[1:]


def limpiar_descripcion(descripcion):
    """Quita los prefijos de control del proveedor (* y **), colapsa espacios y pasa a mayúsculas."""
    sin_prefijo = descripcion.strip().lstrip("*").strip()
    return re.sub(r"\s+", " ", sin_prefijo).upper()


def primera_palabra(descripcion):
    """Sustantivo núcleo aproximado: la primera palabra de la descripción limpia."""
    limpia = limpiar_descripcion(descripcion)
    return limpia.split(" ")[0] if limpia else ""


def digito_verificador_valido(codigo):
    """Algoritmo GS1 módulo 10: pondera 3 y 1 alternados desde la derecha, sin contar el verificador."""
    cuerpo = codigo[:-1]
    verificador = int(codigo[-1])
    suma = sum(int(digito) * (3 if indice % 2 == 0 else 1) for indice, digito in enumerate(reversed(cuerpo)))
    return (10 - suma % 10) % 10 == verificador


def normalizar_ean(codigo_interno, ean):
    """Devuelve (GTIN-13 o EAN-8 normalizado, estado). Si el código no sirve, el primer valor es None."""
    valor = ean.strip()
    if not valor.isdigit():
        return None, "no_numerico"
    if valor == codigo_interno.strip():
        return None, "igual_al_codigo_interno"
    if len(valor) == 11:
        valor = "0" + valor  # UPC-A al que Excel le quitó el cero inicial
    if len(valor) == 14:
        valor = valor[1:]  # GTIN-14: se descarta el indicador de empaque
    if len(valor) == 12:
        valor = "0" + valor  # UPC-A -> GTIN-13
    if len(valor) not in (8, 13):
        return None, f"largo_{len(valor)}"
    if not digito_verificador_valido(valor):
        return None, "digito_verificador_invalido"
    if len(valor) == 13 and valor[0] == "2":
        return None, "uso_interno_prefijo_2"
    return valor, "valido"
