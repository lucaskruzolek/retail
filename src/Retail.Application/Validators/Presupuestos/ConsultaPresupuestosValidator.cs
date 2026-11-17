using FluentValidation;
using Retail.Application.DTOs.Presupuestos;

namespace Retail.Application.Validators.Presupuestos;

/// <summary>
/// Valida los criterios de la consulta paginada de presupuestos antes de que lleguen a SQL Server.
/// </summary>
public class ConsultaPresupuestosValidator : AbstractValidator<ConsultaPresupuestosDto>
{
    /// <summary>Tope de filas por página: protege la memoria del cliente (RNF-03).</summary>
    public const int TamanoPaginaMaximo = 200;

    public ConsultaPresupuestosValidator()
    {
        RuleFor(x => x.Filtro).IsInEnum().WithMessage("El filtro de estado no es válido.");

        RuleFor(x => x.Pagina).GreaterThanOrEqualTo(1).WithMessage("La página debe ser 1 o mayor.");
        RuleFor(x => x.TamanoPagina)
            .InclusiveBetween(1, TamanoPaginaMaximo)
            .WithMessage($"El tamaño de página debe estar entre 1 y {TamanoPaginaMaximo}.");
    }
}
