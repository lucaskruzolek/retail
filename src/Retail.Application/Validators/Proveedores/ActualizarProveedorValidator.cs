using FluentValidation;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Validators.Common;

namespace Retail.Application.Validators.Proveedores;

/// <summary>
/// Validador declarativo para la modificación de distribuidores mayoristas. Usa las mismas reglas que el alta.
/// </summary>
public class ActualizarProveedorValidator : AbstractValidator<ActualizarProveedorDto>
{
    public ActualizarProveedorValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.IdProveedor)
            .GreaterThan(0).WithMessage("El identificador del proveedor debe ser válido.");

        RuleFor(x => x.RazonSocial)
            .RazonSocial("la razón social del proveedor");

        RuleFor(x => x.Cuit)
            .Cuit();

        RuleFor(x => x.Telefono)
            .TelefonoOpcional();

        RuleFor(x => x.Email)
            .EmailOpcional();
    }
}
