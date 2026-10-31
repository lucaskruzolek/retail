"""Vectores de referencia para la paridad Python-C# del vectorizador (Track 6.6, sección 10). Requiere ml/.venv.

Uso: ml/.venv/Scripts/python ml/scripts/vectores_paridad.py [--salida ml/paridad/vectores-referencia.json]

Usa descripciones INVENTADAS (la lista del distribuidor no se versiona) que ejercitan cada regla que C# debe replicar:
tildes y eñe, el prefijo de control '*', espacios repetidos, minúsculas, tokens solo numéricos (se descartan), tokens
mixtos (se conservan), palabras más cortas que el n-grama y barras. Con la configuración ganadora de E-2/E-1
(n-gramas de 3 a 4, sin tokens numéricos) escribe, para cada descripción: el texto preparado, los n-gramas en orden de
extracción y el vector TF-IDF (n-grama -> peso); y para el corpus: el vocabulario con su IDF.

Detalle de "char_wb" que C# debe respetar: cada palabra se rodea con un espacio a cada lado; si la palabra rodeada es
más corta que n, se emite una sola vez completa (" X " con n = 4 produce " X ").
"""

import argparse
import json
import os

from vectorizador import crear_vectorizador, preparar

NGRAMAS = (3, 4)
QUITAR_NUMERICOS = True

DESCRIPCIONES = [
    "**CUADERNO TAPA DURA 48 HJS RAYADO",
    "CUADERNO ESPIRAL 80 HJS CUADRICULADO",
    "  cuaderno   tapa blanda  x 24 ",
    "ABROCHADORA METÁLICA 24/6 NEGRA",
    "ABROCHADORA PLÁSTICA 21/6 AZUL",
    "MUÑECO DE PELUCHE OSO 30CM",
    "MUÑECA ARTICULADA C/ACCESORIOS",
    "PINTURA ACRÍLICA 250ML ROJO",
    "PINTURA ACRÍLICA 250ML AZUL",
    "GOMA EVA 40X60 VERDE",
    "GOMA DE BORRAR BLANCA X 2",
    "CARPETA N°3 C/E 2 ANILLOS",
]

parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
parser.add_argument("--salida", default="ml/paridad/vectores-referencia.json")
argumentos = parser.parse_args()

vectorizador = crear_vectorizador(NGRAMAS, QUITAR_NUMERICOS)
matriz = vectorizador.fit_transform(DESCRIPCIONES)
analizador = vectorizador.build_analyzer()
terminos = vectorizador.get_feature_names_out()

documentos = []
for indice, descripcion in enumerate(DESCRIPCIONES):
    fila = matriz.getrow(indice)
    documentos.append({
        "descripcion": descripcion,
        "preparado": preparar(descripcion, QUITAR_NUMERICOS),
        "ngramas": analizador(descripcion),
        "vector": {terminos[j]: float(v) for j, v in sorted(zip(fila.indices, fila.data), key=lambda p: terminos[p[0]])},
    })

referencia = {
    "configuracion": {
        "analizador": "char_wb", "ngramas": list(NGRAMAS), "quitar_tokens_sin_letras": QUITAR_NUMERICOS,
        "min_df": 2, "idf": "ln((1 + N) / (1 + df)) + 1", "tf": "conteo", "norma": "l2", "tolerancia": 1e-6,
    },
    "idf": {t: float(v) for t, v in sorted(zip(terminos, vectorizador.idf_))},
    "documentos": documentos,
}
os.makedirs(os.path.dirname(os.path.abspath(argumentos.salida)), exist_ok=True)
with open(argumentos.salida, "w", encoding="utf-8", newline="\n") as archivo:
    json.dump(referencia, archivo, ensure_ascii=False, indent=2)
    archivo.write("\n")
print(f"{len(DESCRIPCIONES)} descripciones | vocabulario: {len(terminos)} n-gramas | {argumentos.salida}")
