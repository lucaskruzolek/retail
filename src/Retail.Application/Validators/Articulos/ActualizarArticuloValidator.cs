using FluentValidation;
using Retail.Application.DTOs.Articulos;

namespace Retail.Application.Validators.Articulos;

/// <summary>
/// Validador declarativo de reglas de negocio para la modificación de artículos o servicios existentes (RF-04).
/// </summary>
public class ActualizarArticuloValidator : AbstractValidator<ActualizarArticuloDto>
{
    public ActualizarArticuloValidator()
    {
        RuleFor(x => x.IdArticulo)
            .GreaterThan(0).WithMessage("El identificador del artículo debe ser mayor a cero.");

        RuleFor(x => x.Descripcion)
            .NotEmpty().WithMessage("La descripción del artículo es obligatoria.")
            .MaximumLength(200).WithMessage("La descripción no puede exceder los 200 caracteres.");

        RuleFor(x => x.IdCategoria)
            .GreaterThan(0)
            .When(x => x.IdCategoria.HasValue)
            .WithMessage("Debe seleccionar una categoría válida.");

        RuleFor(x => x.IdMarca)
            .GreaterThan(0)
            .When(x => x.IdMarca.HasValue)
            .WithMessage("Debe seleccionar una marca válida.");

        RuleFor(x => x.CostoReposicion)
            .GreaterThanOrEqualTo(0m).WithMessage("El costo de reposición no puede ser negativo.");

        RuleFor(x => x.PorcentajeGanancia)
            .GreaterThanOrEqualTo(0m).WithMessage("El porcentaje de ganancia no puede ser negativo.");

        When(x => !string.IsNullOrWhiteSpace(x.CodigoBarras), () =>
        {
            RuleFor(x => x.CodigoBarras)
                .MaximumLength(50).WithMessage("El código de barras no puede exceder 50 caracteres.");
        });

        When(x => !x.EsServicio, () =>
        {
            RuleFor(x => x.StockActual)
                .GreaterThanOrEqualTo(0).WithMessage("El stock actual no puede ser negativo.");

            RuleFor(x => x.StockMinimo)
                .GreaterThanOrEqualTo(0).WithMessage("El stock mínimo no puede ser negativo.");
        });
    }
}
