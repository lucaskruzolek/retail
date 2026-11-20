namespace Retail.Application.DTOs.Sistema;

/// <summary>
/// Dónde corre SQL Server respecto de esta PC (MT-12): las copias de seguridad y la restauración se hacen solo en la PC
/// servidor.
/// </summary>
public record class InformacionServidorDto
{
    /// <summary>Verdadero si el motor de base de datos corre en esta misma PC.</summary>
    public required bool EsEstaPc { get; init; }

    /// <summary>Nombre del equipo donde corre el motor (para mostrarlo en el mensaje al operador).</summary>
    public required string NombreEquipoServidor { get; init; }
}
