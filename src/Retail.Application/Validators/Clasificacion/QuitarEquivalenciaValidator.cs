using FluentValidation;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Validators.Common;
using Retail.Domain.Enums;

namespace Retail.Application.Validators.Clasificacion;

/// <summary>
/// Validador de la baja de una equivalencia del proveedor: la misma clave que la asignación, sin destino.
/// </summary>
public class QuitarEquivalenciaValidator : AbstractValidator<QuitarEquivalenciaDto>
{
    public QuitarEquivalenciaValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.IdProveedor)
            .GreaterThan(0).WithMessage("Debe indicar un proveedor válido.");

        RuleFor(x => x.Tipo)
            .IsInEnum().WithMessage("El tipo de equivalencia no es válido.");

        RuleFor(x => x.Texto)
            .TextoDeProveedor("el texto del proveedor");

        RuleFor(x => x.TextoRubro)
            .Empty().When(x => x.Tipo == TipoEquivalenciaEnum.Marca)
            .WithMessage("Una equivalencia de marca no lleva rubro.");

        RuleFor(x => x.TextoRubro!)
            .TextoDeProveedor("el rubro del proveedor")
            .When(x => x.Tipo == TipoEquivalenciaEnum.Categoria && !string.IsNullOrWhiteSpace(x.TextoRubro));
    }
}
