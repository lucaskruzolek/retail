using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Valor de la clasificación del proveedor que todavía no tiene equivalencia (M-13): se ofrece en la pantalla de
/// equivalencias para traducirlo a una categoría o una marca del comercio, o ignorarlo. Uno por valor distinto, no por
/// ítem.
/// </summary>
public record class ValorSinTraducirDto
{
    public required TipoEquivalenciaEnum Tipo { get; init; }

    /// <summary>
    /// Clave en forma de comparación (M-31), la misma con que se registra la equivalencia.
    /// </summary>
    public string? TextoRubro { get; init; }

    public required string Texto { get; init; }

    /// <summary>
    /// Cuántos ítems del lote traen este valor.
    /// </summary>
    public required int CantidadDeItems { get; init; }
}
