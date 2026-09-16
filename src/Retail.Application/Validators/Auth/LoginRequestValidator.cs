using FluentValidation;
using Retail.Application.DTOs.Auth;

namespace Retail.Application.Validators.Auth;

/// <summary>
/// Validador declarativo de formato y completitud para solicitudes de autenticación (RF-01).
/// </summary>
public class LoginRequestValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.NombreUsuario)
            .NotEmpty().WithMessage("El nombre de usuario es obligatorio.")
            .MaximumLength(50).WithMessage("El nombre de usuario no puede exceder 50 caracteres.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.");
    }
}
