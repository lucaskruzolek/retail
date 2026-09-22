using FluentValidation;
using Retail.Application.DTOs.Proveedores;

namespace Retail.Application.Validators.Proveedores;

/// <summary>
/// Validador para la configuración de columnas de importación de planillas (RF-07).
/// </summary>
public class MapeoColumnasValidator : AbstractValidator<MapeoColumnasDto>
{
    public MapeoColumnasValidator()
    {
        RuleFor(x => x.IdProveedor)
            .GreaterThan(0).WithMessage("Debe seleccionar un proveedor válido.");

        RuleFor(x => x.ColumnaCodigo)
            .NotEmpty().WithMessage("El nombre de la columna para código de producto es obligatorio.");

        RuleFor(x => x.ColumnaDescripcion)
            .NotEmpty().WithMessage("El nombre de la columna para descripción es obligatorio.");

        RuleFor(x => x.ColumnaPrecioCosto)
            .NotEmpty().WithMessage("El nombre de la columna para precio de costo es obligatorio.");

        RuleFor(x => x.FilaInicial)
            .GreaterThanOrEqualTo(1).WithMessage("La fila inicial debe ser mayor o igual a 1.");
    }
}
