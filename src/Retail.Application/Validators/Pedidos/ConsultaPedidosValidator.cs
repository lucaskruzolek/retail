using FluentValidation;
using Retail.Application.DTOs.Pedidos;

namespace Retail.Application.Validators.Pedidos;

/// <summary>
/// Valida los criterios de la consulta paginada de pedidos antes de que lleguen a SQL Server: identificadores
/// positivos, estados conocidos y una página razonable.
/// </summary>
public class ConsultaPedidosValidator : AbstractValidator<ConsultaPedidosDto>
{
    /// <summary>Tope de filas por página: protege la memoria del cliente (RNF-03) aunque la UI ofrezca menos.</summary>
    public const int TamanoPaginaMaximo = 200;

    public ConsultaPedidosValidator()
    {
        RuleForEach(x => x.Estados).IsInEnum().WithMessage("El estado del pedido no es válido.");
        RuleForEach(x => x.IdsProveedor).GreaterThan(0).WithMessage("El proveedor no es válido.");

        RuleFor(x => x.Pagina).GreaterThanOrEqualTo(1).WithMessage("La página debe ser 1 o mayor.");
        RuleFor(x => x.TamanoPagina)
            .InclusiveBetween(1, TamanoPaginaMaximo)
            .WithMessage($"El tamaño de página debe estar entre 1 y {TamanoPaginaMaximo}.");
    }
}
