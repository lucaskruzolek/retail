using FluentValidation;
using Retail.Application.DTOs.Listas;

namespace Retail.Application.Validators.Listas;

/// <summary>
/// Valida los criterios de la consulta paginada de listas antes de que lleguen a SQL Server.
/// </summary>
public class ConsultaListasValidator : AbstractValidator<ConsultaListasDto>
{
    /// <summary>Tope de filas por página: protege la memoria del cliente (RNF-03).</summary>
    public const int TamanoPaginaMaximo = 200;

    public ConsultaListasValidator()
    {
        RuleFor(x => x.Pagina).GreaterThanOrEqualTo(1).WithMessage("La página debe ser 1 o mayor.");
        RuleFor(x => x.TamanoPagina)
            .InclusiveBetween(1, TamanoPaginaMaximo)
            .WithMessage($"El tamaño de página debe estar entre 1 y {TamanoPaginaMaximo}.");
    }
}
