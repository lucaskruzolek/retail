using FluentValidation;
using Retail.Application.DTOs.Compras;

namespace Retail.Application.Validators.Compras;

/// <summary>
/// Validador declarativo de la frontera de entrada para registrar una compra a proveedor (RF-19).
/// Rechaza datos mal formados antes de cargar agregados; las reglas de negocio (stock en servicios,
/// artículo inexistente, proveedor inactivo) las garantiza el Dominio y el Servicio de Aplicación.
/// </summary>
public class CrearCompraValidator : AbstractValidator<CrearCompraDto>
{
    public CrearCompraValidator()
    {
        RuleFor(x => x.IdProveedor)
            .GreaterThan(0).WithMessage("Debe seleccionar un proveedor o distribuidor válido.");

        RuleFor(x => x.IdUsuario)
            .GreaterThan(0).WithMessage("Identificador de usuario inválido.");

        RuleFor(x => x.TipoComprobante)
            .NotEmpty().WithMessage("El tipo de comprobante es obligatorio.")
            .MaximumLength(20).WithMessage("El tipo de comprobante no puede superar los 20 caracteres.");

        RuleFor(x => x.NumeroComprobante)
            .NotEmpty().WithMessage("El número de comprobante es obligatorio.")
            .MaximumLength(50).WithMessage("El número de comprobante no puede superar los 50 caracteres.");

        RuleFor(x => x.FechaEmision)
            .LessThanOrEqualTo(_ => DateTime.UtcNow.AddDays(1))
            .WithMessage("La fecha de emisión no puede ser una fecha futura imposible.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("La factura de compra debe contener al menos un renglón de artículo.");

        // C-2: Rechazar artículos repetidos antes de llegar al agregado (un solo renglón por artículo).
        RuleFor(x => x.Items)
            .Must(items => items.GroupBy(i => i.IdArticulo).All(g => g.Count() == 1))
            .WithMessage("La factura no puede tener el mismo artículo en dos renglones distintos.")
            .When(x => x.Items is { Count: > 0 });

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.IdArticulo)
                .GreaterThan(0).WithMessage("Hay un artículo inválido en el remito de compra.");

            item.RuleFor(i => i.Cantidad)
                .GreaterThanOrEqualTo(1).WithMessage("La cantidad de cada artículo debe ser al menos 1.");

            item.RuleFor(i => i.CostoUnitario)
                .GreaterThan(0m).WithMessage("El costo unitario de compra debe ser mayor a cero.");
        });
    }
}
