using FluentValidation;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Validators.Common;
using Retail.Domain.Enums;

namespace Retail.Application.Validators.Clasificacion;

/// <summary>
/// Validador de la asignación de una equivalencia del proveedor (Track 6.6, M-13, M-31, M-32).
/// </summary>
public class AsignarEquivalenciaValidator : AbstractValidator<AsignarEquivalenciaDto>
{
    public AsignarEquivalenciaValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.IdProveedor)
            .GreaterThan(0).WithMessage("Debe indicar un proveedor válido.");

        RuleFor(x => x.Tipo)
            .IsInEnum().WithMessage("El tipo de equivalencia no es válido.");

        RuleFor(x => x.Texto)
            .TextoDeProveedor("el texto del proveedor");

        // El rubro es contexto de la categoría (M-31): en una equivalencia de marca se rechaza en lugar de descartarlo
        // en silencio, porque indica un error de quien arma la solicitud.
        RuleFor(x => x.TextoRubro)
            .Empty().When(x => x.Tipo == TipoEquivalenciaEnum.Marca)
            .WithMessage("Una equivalencia de marca no lleva rubro.");

        RuleFor(x => x.TextoRubro!)
            .TextoDeProveedor("el rubro del proveedor")
            .When(x => x.Tipo == TipoEquivalenciaEnum.Categoria && !string.IsNullOrWhiteSpace(x.TextoRubro));

        RuleFor(x => x.IdDestino)
            .GreaterThan(0).When(x => x.IdDestino.HasValue)
            .WithMessage("El destino de la equivalencia debe ser válido.");
    }
}
