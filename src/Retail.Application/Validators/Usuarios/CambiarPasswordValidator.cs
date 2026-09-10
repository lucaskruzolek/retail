using FluentValidation;
using Retail.Application.DTOs.Usuarios;

namespace Retail.Application.Validators.Usuarios;

/// <summary>
/// Validador declarativo de reglas de negocio para el cambio o blanqueo de contraseñas de operadores (RF-03, RNF-04).
/// </summary>
public class CambiarPasswordValidator : AbstractValidator<CambiarPasswordDto>
{
    public CambiarPasswordValidator()
    {
        RuleFor(x => x.IdUsuario)
            .GreaterThan(0).WithMessage("El identificador del usuario debe ser mayor a cero.");

        RuleFor(x => x.NuevaPassword)
            .NotEmpty().WithMessage("La nueva contraseña es obligatoria.")
            .MinimumLength(6).WithMessage("La nueva contraseña debe tener una longitud mínima de 6 caracteres.");
    }
}
