using Retail.Application.DTOs.Common;

namespace Retail.Application.DTOs.Reportes;

/// <summary>Un indicador del tablero comparado con el período anterior.</summary>
public record class IndicadorDto : BaseDto
{
    public decimal Valor { get; init; }

    public decimal ValorAnterior { get; init; }

    /// <summary>Variación porcentual contra el período anterior; <c>null</c> si el anterior fue cero.</summary>
    public decimal? VariacionPorcentual { get; init; }
}
