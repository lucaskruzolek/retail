"""Casos de referencia para la paridad Python-C# de las reglas de texto de la clasificación (Track 6.6, sección 10).

Uso: python ml/scripts/reglas_paridad.py [--salida ml/paridad/reglas-texto-referencia.json]

Usa descripciones y códigos INVENTADOS (la lista del distribuidor no se versiona). Para cada descripción escribe lo que
C# debe reproducir en ReglasClasificacion: la descripción limpia (limpiar_descripcion), la primera palabra
(primera_palabra), el término en forma de comparación (normalizar_texto de la primera palabra), las palabras donde se
buscan las marcas (las de marcas_en: sin el término, sin el punto final y separadas también por la barra) y la segunda
palabra de los pares de clasificación (e6_comun.segunda_palabra, Fase 5 del Track 6.6). Para cada
código de barras escribe el GTIN normalizado (normalizar_ean), que C# reproduce en ReglasCodigoBarras.
Solo biblioteca estándar: no requiere ml/.venv.
"""

import argparse
import json
import os

from comun import limpiar_descripcion, normalizar_ean, normalizar_texto, primera_palabra, tokens_de
from e6_comun import segunda_palabra

DESCRIPCIONES = [
    "**CUADERNO TAPA DURA 48 HJS RAYADO",
    "* * CUADERNO ESPIRAL",
    "  cuaderno   tapa\tblanda  x 24 ",
    "ABROCHADORA METÁLICA 24/6 NEGRA",
    "MUÑECO DE PELUCHE OSO 30CM",
    "LÁPIZ NEGRO HB KOBY/DATA.",
    "REPUESTO RIVADAVIA N°3 C/E.",
    "SET ESCOLAR ARTÍSTICA DIBU X 6",
    "100 SOBRES MANILA",
    "CUADER.C/E 48H",
    "MOCHILA",
    "***",
    "",
    "SET DE GEOMETRIA 4 PIEZAS",
    "CINTA X 3 METROS",
    "GOMA P/ BORRAR",
    "BLOCK C/E 48H",
    "BLOCK A4 RAYADO",
    "VASO 250ML",
    "TINTA ÑANDÚ ROJA",
    "**SET ESC. DIBUJO",
    "CINTA DE LA Y CON",
    "LIBRO",
]

CODIGOS = [
    ("INT-1", "4006381333931"),
    ("INT-2", "036000291452"),
    ("INT-3", "36000291452"),
    ("INT-4", "00036000291452"),
    ("INT-5", "10036000291459"),
    ("INT-6", "10036000291452"),
    ("INT-7", "96385074"),
    ("INT-8", "2000000000015"),
    ("INT-9", "4006381333932"),
    ("INT-10", "ABC123"),
    ("12345678", "12345678"),
]


def palabras_para_marca(descripcion):
    """Las palabras que recorre comun.marcas_en, sobre la descripción limpia como la llaman los scripts."""
    return [parte for token in tokens_de(normalizar_texto(limpiar_descripcion(descripcion)))
            for parte in token.split("/") if parte]


parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
parser.add_argument("--salida", default="ml/paridad/reglas-texto-referencia.json")
argumentos = parser.parse_args()

referencia = {
    "descripciones": [
        {
            "descripcion": descripcion,
            "limpia": limpiar_descripcion(descripcion),
            "primera_palabra": primera_palabra(descripcion),
            "termino": normalizar_texto(primera_palabra(descripcion)),
            "palabras_para_marca": palabras_para_marca(descripcion),
            "segunda_palabra": segunda_palabra(descripcion),
        }
        for descripcion in DESCRIPCIONES
    ],
    "codigos_de_barras": [
        {"codigo_interno": interno, "codigo": codigo, "gtin": normalizar_ean(interno, codigo)[0]}
        for interno, codigo in CODIGOS
    ],
}
os.makedirs(os.path.dirname(os.path.abspath(argumentos.salida)), exist_ok=True)
with open(argumentos.salida, "w", encoding="utf-8", newline="\n") as archivo:
    json.dump(referencia, archivo, ensure_ascii=False, indent=2)
    archivo.write("\n")
print(f"{len(DESCRIPCIONES)} descripciones | {len(CODIGOS)} códigos | {argumentos.salida}")
