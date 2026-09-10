using FluentValidation;
using Retail.Application.DTOs.Usuarios;

namespace Retail.Application.Validators.Usuarios;

/// <summary>
/// Validador declarativo de reglas de negocio para el alta de nuevos usuarios (RF-03).
/// </summary>
public class CrearUsuarioValidator : AbstractValidator<CrearUsuarioDto>
{
    public CrearUsuarioValidator()
    {
        RuleFor(x => x.NombreUsuario)
            .NotEmpty().WithMessage("El nombre de usuario es obligatorio.")
            .MinimumLength(3).WithMessage("El nombre de usuario debe contener al menos 3 caracteres.")
            .MaximumLength(50).WithMessage("El nombre de usuario no puede exceder 50 caracteres.")
            .Matches(@"^[a-zA-Z0-9_\.]+$").WithMessage("El nombre de usuario solo puede contener letras, números, puntos o guiones bajos.");

        RuleFor(x => x.NombreCompleto)
            .NotEmpty().WithMessage("El nombre completo es obligatorio.")
            .MinimumLength(3).WithMessage("El nombre completo debe contener al menos 3 caracteres.")
            .MaximumLength(100).WithMessage("El nombre completo no puede exceder 100 caracteres.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(6).WithMessage("La contraseña debe tener una longitud mínima de 6 caracteres.");

        RuleFor(x => x.Rol)
            .IsInEnum().WithMessage("El rol de usuario especificado no es válido.");
    }
}
