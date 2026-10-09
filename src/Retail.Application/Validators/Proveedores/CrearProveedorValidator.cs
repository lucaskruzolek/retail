using FluentValidation;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Validators.Common;

namespace Retail.Application.Validators.Proveedores;

/// <summary>
/// Validador declarativo para el alta de distribuidores mayoristas (RF-05).
/// </summary>
public class CrearProveedorValidator : AbstractValidator<CrearProveedorDto>
{
    public CrearProveedorValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

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
