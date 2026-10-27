"""Piezas compartidas por E-6 y E-6b (Track 6.6, secciones 11.3 y 11.4): la tabla "término ambiguo + segunda palabra"."""

from collections import Counter, defaultdict

from comun import limpiar_descripcion, normalizar_texto

RELLENO = {"DE", "DEL", "LA", "EL", "LOS", "LAS", "Y", "CON", "PARA", "X", "P/", "C/"}
SIN_SUGERENCIA = "\0"


def segunda_palabra(descripcion):
    """Primera palabra después del término con al menos 2 letras que no sea de relleno (sección 11.3).

    Sin el punto final, como tokens_de (M-56): "ESC." y "ESC" son la misma palabra.
    """
    for palabra in limpiar_descripcion(descripcion).split(" ")[1:]:
        normalizada = normalizar_texto(palabra).rstrip(".")
        if sum(c.isalpha() for c in normalizada) >= 2 and normalizada not in RELLENO:
            return normalizada
    return ""


class TablaDePares:
    """Categoría mayoritaria de cada par (término, segunda palabra), con su soporte y su pureza."""

    def fit(self, descripciones, terminos, categorias):
        conteos = defaultdict(Counter)
        for descripcion, termino, categoria in zip(descripciones, terminos, categorias):
            conteos[(termino, segunda_palabra(descripcion))][categoria] += 1
        self.pares_ = {}
        for clave, contador in conteos.items():
            categoria, mayoria = contador.most_common(1)[0]
            soporte = sum(contador.values())
            self.pares_[clave] = (categoria, soporte, mayoria / soporte)
        return self

    def consultar(self, descripciones, terminos):
        """(categoría, soporte, pureza) por fila; sin par conocido: (SIN_SUGERENCIA, 0, 0)."""
        return [self.pares_.get((t, segunda_palabra(d)), (SIN_SUGERENCIA, 0, 0.0))
                for d, t in zip(descripciones, terminos)]
