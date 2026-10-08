using FluentValidation;
using Retail.Application.DTOs.Articulos;

namespace Retail.Application.Validators.Articulos;

/// <summary>
/// Validador declarativo para el alta de una presentación derivada de un artículo de compra (RF-21).
/// </summary>
public class CrearPresentacionValidator : AbstractValidator<CrearPresentacionDto>
{
    public CrearPresentacionValidator()
    {
        RuleFor(x => x.IdArticuloOrigen)
            .GreaterThan(0).WithMessage("Debe seleccionar un artículo de origen válido.");

        RuleFor(x => x.UnidadesPorOrigen)
            .GreaterThanOrEqualTo(1).WithMessage("Las unidades por origen deben ser al menos 1.");

        RuleFor(x => x.Descripcion)
            .NotEmpty().WithMessage("La descripción del artículo es obligatoria.")
            .MaximumLength(200).WithMessage("La descripción no puede exceder los 200 caracteres.");

        RuleFor(x => x.PorcentajeGanancia)
            .GreaterThanOrEqualTo(0m).WithMessage("El porcentaje de ganancia no puede ser negativo.");

        RuleFor(x => x.StockMinimo)
            .GreaterThanOrEqualTo(0).WithMessage("El stock mínimo no puede ser negativo.");

        When(x => !string.IsNullOrWhiteSpace(x.CodigoBarras), () =>
        {
            RuleFor(x => x.CodigoBarras)
                .MaximumLength(50).WithMessage("El código de barras no puede exceder 50 caracteres.");
        });
    }
}
