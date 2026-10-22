"""Funciones compartidas por los scripts de exploración del Track 6.6.

Solo usa la biblioteca estándar de Python: no hace falta instalar paquetes.
"""

import csv
import io
import os
import re
import unicodedata


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


def tokens_de(descripcion):
    """Palabras de una descripción limpia sin la primera (el término) y sin el punto final."""
    return [t.rstrip(".") for t in descripcion.split(" ")[1:]]


def normalizar_texto(texto):
    """Mayúsculas sin tildes ni espacios repetidos: compara nombres de marca escritos a mano con las descripciones."""
    sin_tildes = "".join(c for c in unicodedata.normalize("NFD", texto) if unicodedata.category(c) != "Mn")
    return re.sub(r"\s+", " ", sin_tildes).strip().upper()


def marcas_en(descripcion, forma_a_marca):
    """Marcas cuyo nombre o alias aparece en la descripción como palabra o frase completa (no como parte de otra).

    forma_a_marca asocia cada forma normalizada con normalizar_texto a su marca. La primera palabra no cuenta (es el
    término). La barra también separa palabras: el proveedor escribe la marca pegada a la línea ("KOBY/DATA").
    """
    palabras = [parte for token in tokens_de(normalizar_texto(descripcion)) for parte in token.split("/") if parte]
    max_palabras = max((len(forma.split(" ")) for forma in forma_a_marca), default=0)
    encontradas = set()
    for largo in range(1, max_palabras + 1):
        for inicio in range(len(palabras) - largo + 1):
            marca = forma_a_marca.get(" ".join(palabras[inicio:inicio + largo]))
            if marca:
                encontradas.add(marca)
    return encontradas


def leer_planilla(ruta):
    """Lee una planilla ';' completada a mano y devuelve una lista de diccionarios, uno por fila.

    Acepta UTF-8 (con o sin BOM), que es lo que escriben estos scripts, y ANSI de Windows (cp1252), que es lo que
    deja Excel al guardar como "CSV (delimitado por comas)". El separador es ';', salvo que el encabezado esté
    separado por tabulaciones (Excel, "Texto (delimitado por tabulaciones)").
    """
    with open(ruta, "rb") as archivo:
        crudo = archivo.read()
    try:
        texto = crudo.decode("utf-8-sig")
    except UnicodeDecodeError:
        texto = crudo.decode("cp1252")
    encabezado = texto.split("\n", 1)[0]
    separador = "\t" if "\t" in encabezado and ";" not in encabezado else ";"
    return list(csv.DictReader(io.StringIO(texto), delimiter=separador))


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


def escribir_csv(ruta, encabezado, filas):
    """Escribe un CSV ';' en UTF-8 con BOM (para que Excel respete tildes y eñes) y crea la carpeta si falta.

    Los scripts de ml/ que lean de nuevo estos archivos deben abrirlos con encoding="utf-8-sig".
    """
    os.makedirs(os.path.dirname(os.path.abspath(ruta)), exist_ok=True)
    with open(ruta, "w", encoding="utf-8-sig", newline="") as archivo:
        escritor = csv.writer(archivo, delimiter=";")
        escritor.writerow(encabezado)
        escritor.writerows(filas)
