using FluentValidation;
using Retail.Application.DTOs.Usuarios;
using Retail.Application.Validators.Common;

namespace Retail.Application.Validators.Usuarios;

/// <summary>
/// Validador declarativo de reglas de negocio para el cambio o blanqueo de contraseñas de operadores (RF-03, RNF-04).
/// La regla "distinta del nombre de usuario" la verifica <c>UsuarioService</c>, porque el DTO solo trae el
/// identificador y el nombre de usuario hay que leerlo de la base.
/// </summary>
public class CambiarPasswordValidator : AbstractValidator<CambiarPasswordDto>
{
    public CambiarPasswordValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.IdUsuario)
            .GreaterThan(0).WithMessage("El identificador del usuario debe ser mayor a cero.");

        RuleFor(x => x.NuevaPassword)
            .Contrasena();
    }
}
