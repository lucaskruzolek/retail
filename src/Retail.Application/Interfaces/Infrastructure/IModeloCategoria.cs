using Retail.Application.DTOs.Clasificacion;

namespace Retail.Application.Interfaces.Infrastructure;

/// <summary>
/// Puerto del modelo de categorías del motor de catalogación (Track 6.6, sección 9). El servicio de clasificación no
/// conoce la técnica (n-gramas de caracteres y regresión logística, sección 5): el modelo se reemplaza sin tocar la capa
/// de Aplicación. El entrenamiento se agrega a este contrato en la Fase 5, junto con su implementación (M-37).
/// </summary>
public interface IModeloCategoria
{
    /// <summary>
    /// Falso mientras no haya un modelo entrenado que corresponda a la base (arranque en frío, sección 7): solo
    /// sugieren los diccionarios.
    /// </summary>
    bool EstaDisponible { get; }

    /// <summary>
    /// Predice la categoría de cada descripción, tal como la escribe el proveedor (el modelo la prepara igual que en
    /// el entrenamiento). Devuelve una predicción por descripción, en el mismo orden. Es un cálculo en memoria de
    /// microsegundos por ítem: no accede a la base.
    /// </summary>
    /// <exception cref="InvalidOperationException">Si <see cref="EstaDisponible"/> es falso.</exception>
    IReadOnlyList<PrediccionCategoriaDto> Predecir(IReadOnlyList<string> descripciones);
}
