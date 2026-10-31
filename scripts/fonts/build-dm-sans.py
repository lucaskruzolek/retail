"""Genera las fuentes DM Sans estáticas que viajan embebidas en Retail.App.

WPF ignora los ejes de las fuentes variables: hay que entregar un archivo por peso. El script descarga la DM Sans
variable oficial, fija el tamaño óptico y el peso de cada instancia y corrige los nombres de familia. Los glifos
quedan tal cual los diseñó la familia: los dígitos de DM Sans son proporcionales.

Requiere fonttools (solo para regenerar las fuentes, no es dependencia de la aplicación):
    pip install fonttools
    python scripts/fonts/build-dm-sans.py

Fuente oficial: https://github.com/google/fonts/tree/main/ofl/dmsans
"""

from __future__ import annotations

import tempfile
import urllib.request
from pathlib import Path

from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

URL_FUENTE = "https://raw.githubusercontent.com/google/fonts/main/ofl/dmsans/DMSans%5Bopsz%2Cwght%5D.ttf"
URL_LICENCIA = "https://raw.githubusercontent.com/google/fonts/main/ofl/dmsans/OFL.txt"
DESTINO = Path(__file__).resolve().parents[2] / "src" / "Retail.App" / "Fonts" / "DMSans"

# (archivo, familia, estilo, nombre PostScript, tamaño óptico, peso)
INSTANCIAS = (
    ("DMSans-Regular.ttf", "DM Sans", "Regular", "DMSans-Regular", 14, 400),
    ("DMSans-Medium.ttf", "DM Sans", "Medium", "DMSans-Medium", 14, 500),
    ("DMSans-SemiBold.ttf", "DM Sans", "SemiBold", "DMSans-SemiBold", 14, 600),
    ("DMSans-Bold.ttf", "DM Sans", "Bold", "DMSans-Bold", 14, 700),
    ("DMSansDisplay-SemiBold.ttf", "DM Sans Display", "SemiBold", "DMSansDisplay-SemiBold", 40, 600),
    ("DMSansDisplay-Bold.ttf", "DM Sans Display", "Bold", "DMSansDisplay-Bold", 40, 700),
)


def descargar(url: str, destino: Path) -> None:
    if destino.exists():
        return
    print(f"Descargando {url}")
    urllib.request.urlretrieve(url, destino)


def renombrar(fuente: TTFont, familia: str, estilo: str, postscript: str, peso: int) -> None:
    nombres = fuente["name"]
    version = nombres.getDebugName(5) or "Version 1.000"
    for name_id in (1, 2, 3, 4, 6, 16, 17, 21, 22):
        nombres.removeNames(nameID=name_id)

    registros = {
        1: familia,
        2: estilo,
        3: f"{postscript};{version}",
        4: f"{familia} {estilo}",
        6: postscript,
        16: familia,
        17: estilo,
    }
    for name_id, valor in registros.items():
        nombres.setName(valor, name_id, 3, 1, 0x409)
        nombres.setName(valor, name_id, 1, 0, 0)

    os2 = fuente["OS/2"]
    os2.usWeightClass = peso
    os2.fsSelection &= ~(0b100000 | 0b1000000)
    if peso == 700:
        os2.fsSelection |= 0b100000
    else:
        os2.fsSelection |= 0b1000000
    fuente["head"].macStyle = 1 if peso == 700 else 0


def main() -> None:
    DESTINO.mkdir(parents=True, exist_ok=True)
    temporal = Path(tempfile.gettempdir()) / "DMSans-Variable.ttf"
    descargar(URL_FUENTE, temporal)
    descargar(URL_LICENCIA, DESTINO / "OFL.txt")

    for archivo, familia, estilo, postscript, tamano_optico, peso in INSTANCIAS:
        fuente = TTFont(temporal)
        instancia = instantiateVariableFont(fuente, {"opsz": tamano_optico, "wght": peso}, inplace=True)
        renombrar(instancia, familia, estilo, postscript, peso)
        # WPF (DirectWrite) rechaza la familia si el STAT quedó describiendo ejes que la instancia ya fijó.
        for tabla in ("STAT", "DSIG"):
            if tabla in instancia:
                del instancia[tabla]
        salida = DESTINO / archivo
        instancia.save(salida)
        print(f"  {salida.name}: familia={instancia['name'].getDebugName(1)!r}, estilo={estilo}")

    temporal.unlink()


if __name__ == "__main__":
    main()
