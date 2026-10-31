using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Clave de la equivalencia que se da de baja (M-31): la próxima importación vuelve a ofrecer el valor para traducirlo.
/// </summary>
public record class QuitarEquivalenciaDto
{
    public required int IdProveedor { get; init; }

    public required TipoEquivalenciaEnum Tipo { get; init; }

    public string? TextoRubro { get; init; }

    public required string Texto { get; init; }
}
