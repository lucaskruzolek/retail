using Retail.Domain.Enums;

namespace Retail.Domain.Common;

/// <summary>
/// Cómo se decide el origen de una clasificación confirmada por una persona (Track 6.6, M-34): compara lo que la
/// persona confirmó con lo que había sugerido el sistema. Es la base de la tasa de aceptación y corrección (R-3) y de
/// los artículos confirmados de los que salen los pares propuestos (M-5, M-49).
/// </summary>
public static class ReglasOrigenClasificacion
{
    /// <param name="idSugerido">Categoría o marca que sugirió el sistema; nulo si no sugirió nada.</param>
    /// <param name="idConfirmado">Categoría o marca confirmada; nulo solo para "Sin marca" confirmada (D-18).</param>
    public static OrigenClasificacionEnum Derivar(int? idSugerido, int? idConfirmado)
    {
        if (!idSugerido.HasValue)
        {
            return OrigenClasificacionEnum.Manual;
        }

        return idSugerido == idConfirmado
            ? OrigenClasificacionEnum.SugerenciaAceptada
            : OrigenClasificacionEnum.SugerenciaCorregida;
    }
}
