using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Interfaces.Infrastructure;

namespace Retail.Infrastructure.Clasificacion;

/// <summary>
/// El modelo de categorías cuando todavía no hay ninguno entrenado (Track 6.6, M-37): es el arranque en frío de la
/// sección 7, no un doble de prueba. Sugieren solo los diccionarios y los bloques de términos nuevos llegan sin
/// propuesta. La Fase 5 lo reemplaza por el modelo entrenado en el ERP, sin cambiar la capa de Aplicación.
/// </summary>
public sealed class ModeloCategoriaNoDisponible : IModeloCategoria
{
    public bool EstaDisponible => false;

    public IReadOnlyList<PrediccionCategoriaDto> Predecir(IReadOnlyList<string> descripciones)
    {
        throw new InvalidOperationException(
            "No hay un modelo de categorías entrenado: consulte EstaDisponible antes de predecir.");
    }
}
