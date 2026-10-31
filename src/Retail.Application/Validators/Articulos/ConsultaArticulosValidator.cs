using FluentValidation;
using Retail.Application.DTOs.Articulos;

namespace Retail.Application.Validators.Articulos;

/// <summary>
/// Valida los criterios de la consulta paginada del catálogo antes de que lleguen a SQL Server: identificadores
/// positivos (en distribuidores se admite además el valor de "manual"), estados de stock conocidos y una página
/// razonable.
/// </summary>
public class ConsultaArticulosValidator : AbstractValidator<ConsultaArticulosDto>
{
    /// <summary>Tope de filas por página: protege la memoria del cliente (RNF-03) aunque la UI ofrezca menos.</summary>
    public const int TamanoPaginaMaximo = 200;

    public ConsultaArticulosValidator()
    {
        RuleForEach(x => x.IdsCategoria).GreaterThan(0).WithMessage("La categoría no es válida.");
        RuleForEach(x => x.IdsMarca).GreaterThan(0).WithMessage("La marca no es válida.");
        RuleForEach(x => x.IdsProveedor)
            .Must(id => id > 0 || id == ConsultaArticulosDto.IdProveedorManual)
            .WithMessage("El distribuidor no es válido.");
        RuleForEach(x => x.EstadosStock).IsInEnum().WithMessage("El estado de stock no es válido.");

        RuleFor(x => x.Pagina).GreaterThanOrEqualTo(1).WithMessage("La página debe ser 1 o mayor.");
        RuleFor(x => x.TamanoPagina)
            .InclusiveBetween(1, TamanoPaginaMaximo)
            .WithMessage($"El tamaño de página debe estar entre 1 y {TamanoPaginaMaximo}.");
    }
}
