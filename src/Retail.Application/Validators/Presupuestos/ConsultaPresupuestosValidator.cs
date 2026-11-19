using FluentValidation;
using Retail.Application.DTOs.Presupuestos;

namespace Retail.Application.Validators.Presupuestos;

/// <summary>
/// Valida los criterios de la consulta paginada de presupuestos antes de que lleguen a SQL Server: estados conocidos,
/// clientes positivos, rangos coherentes y una página razonable.
/// </summary>
public class ConsultaPresupuestosValidator : AbstractValidator<ConsultaPresupuestosDto>
{
    /// <summary>Tope de filas por página: protege la memoria del cliente (RNF-03).</summary>
    public const int TamanoPaginaMaximo = 200;

    public ConsultaPresupuestosValidator()
    {
        RuleForEach(x => x.Estados).IsInEnum().WithMessage("El estado del presupuesto no es válido.");
        RuleForEach(x => x.IdsCliente).GreaterThan(0).WithMessage("El cliente no es válido.");

        RuleFor(x => x.EmisionHastaUtc)
            .GreaterThan(x => x.EmisionDesdeUtc!.Value)
            .When(x => x.EmisionDesdeUtc is not null && x.EmisionHastaUtc is not null)
            .WithMessage("El rango de emisión no es válido: el final debe ser posterior al inicio.");

        RuleFor(x => x.VenceHasta)
            .GreaterThanOrEqualTo(x => x.VenceDesde!.Value)
            .When(x => x.VenceDesde is not null && x.VenceHasta is not null)
            .WithMessage("El rango de vencimiento no es válido: el final no puede ser anterior al inicio.");

        RuleFor(x => x.Pagina).GreaterThanOrEqualTo(1).WithMessage("La página debe ser 1 o mayor.");
        RuleFor(x => x.TamanoPagina)
            .InclusiveBetween(1, TamanoPaginaMaximo)
            .WithMessage($"El tamaño de página debe estar entre 1 y {TamanoPaginaMaximo}.");
    }
}
