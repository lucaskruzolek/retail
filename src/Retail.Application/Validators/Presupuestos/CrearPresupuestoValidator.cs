using FluentValidation;
using Retail.Application.DTOs.Presupuestos;
using Retail.Domain.Entities;

namespace Retail.Application.Validators.Presupuestos;

/// <summary>
/// Validador de frontera para emitir un presupuesto (RF-11). Rechaza datos mal formados antes de consultar la base;
/// las reglas de negocio las garantiza el agregado <see cref="Presupuesto"/>. Los límites usan las constantes del
/// dominio: una sola fuente de verdad.
/// </summary>
public class CrearPresupuestoValidator : AbstractValidator<CrearPresupuestoDto>
{
    public CrearPresupuestoValidator()
    {
        RuleFor(x => x.IdUsuario)
            .GreaterThan(0).WithMessage("El presupuesto debe registrar al usuario que lo emite.");

        RuleFor(x => x.IdCliente)
            .GreaterThan(0).When(x => x.IdCliente.HasValue).WithMessage("El cliente seleccionado no es válido.");

        RuleFor(x => x.Titulo)
            .MaximumLength(Presupuesto.LongitudMaximaTitulo)
            .When(x => x.Titulo is not null)
            .WithMessage($"El título no puede superar los {Presupuesto.LongitudMaximaTitulo} caracteres.");

        RuleFor(x => x.Descuento)
            .GreaterThanOrEqualTo(0m).WithMessage("El descuento no puede ser negativo.");

        RuleFor(x => x.VigenciaDias)
            .InclusiveBetween(Presupuesto.VigenciaMinimaDias, Presupuesto.VigenciaMaximaDias)
            .WithMessage($"La vigencia debe estar entre {Presupuesto.VigenciaMinimaDias} y {Presupuesto.VigenciaMaximaDias} días.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("No se puede emitir un presupuesto sin artículos.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.IdArticulo)
                .GreaterThan(0).WithMessage("Hay un artículo inválido en el ticket.");

            item.RuleFor(i => i.Cantidad)
                .GreaterThanOrEqualTo(1).WithMessage("La cantidad de cada artículo debe ser al menos 1.");

            item.RuleFor(i => i.PrecioUnitarioPactado)
                .GreaterThanOrEqualTo(0m).WithMessage("El precio unitario no puede ser negativo.");
        });
    }
}
