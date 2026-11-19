using FluentValidation;
using Retail.Application.DTOs.Ventas;

namespace Retail.Application.Validators.Ventas;

/// <summary>
/// Validador declarativo de la frontera de entrada para registrar una venta en mostrador (RF-09).
/// Rechaza datos mal formados antes de cargar agregados; las reglas de negocio (stock, pagos que cubren el total,
/// límite de crédito) las garantiza el Dominio.
/// </summary>
public class CrearVentaValidator : AbstractValidator<CrearVentaDto>
{
    public CrearVentaValidator()
    {
        RuleFor(x => x.IdTurno)
            .GreaterThan(0).WithMessage("La venta debe registrarse en un turno de caja válido.");

        RuleFor(x => x.IdUsuario)
            .GreaterThan(0).WithMessage("La venta debe registrar al usuario que la realiza.");

        RuleFor(x => x.IdCliente)
            .GreaterThan(0).When(x => x.IdCliente.HasValue).WithMessage("El cliente seleccionado no es válido.");

        RuleFor(x => x.IdPresupuestoOrigen)
            .GreaterThan(0).When(x => x.IdPresupuestoOrigen.HasValue).WithMessage("El presupuesto de origen no es válido.");

        RuleFor(x => x.IdListaOrigen)
            .GreaterThan(0).When(x => x.IdListaOrigen.HasValue).WithMessage("La lista de origen no es válida.");

        RuleFor(x => x)
            .Must(x => !(x.IdPresupuestoOrigen.HasValue && x.IdListaOrigen.HasValue))
            .WithMessage("Una venta no puede originarse a la vez en un presupuesto y en una lista.");

        RuleFor(x => x.Descuento)
            .GreaterThanOrEqualTo(0m).WithMessage("El descuento no puede ser negativo.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("No se puede registrar una venta sin artículos.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.IdArticulo)
                .GreaterThan(0).WithMessage("Hay un artículo inválido en el ticket.");

            item.RuleFor(i => i.Cantidad)
                .GreaterThanOrEqualTo(1).WithMessage("La cantidad de cada artículo debe ser al menos 1.");

            item.RuleFor(i => i.PrecioUnitario)
                .GreaterThanOrEqualTo(0m).WithMessage("El precio unitario no puede ser negativo.");
        });

        RuleForEach(x => x.Pagos).ChildRules(pago =>
        {
            pago.RuleFor(p => p.MedioPago)
                .IsInEnum().WithMessage("El medio de pago no es válido.");

            pago.RuleFor(p => p.Monto)
                .GreaterThan(0m).WithMessage("El monto de cada pago debe ser mayor a cero.");

            pago.RuleFor(p => p.ReferenciaPago)
                .MaximumLength(100).WithMessage("La referencia del pago no puede superar los 100 caracteres.");
        });
    }
}
