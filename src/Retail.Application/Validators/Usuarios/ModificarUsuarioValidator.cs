using FluentValidation;
using Retail.Application.DTOs.Usuarios;

namespace Retail.Application.Validators.Usuarios;

/// <summary>
/// Validador declarativo de reglas de negocio para la modificación de datos de operadores (RF-03).
/// </summary>
public class ModificarUsuarioValidator : AbstractValidator<ModificarUsuarioDto>
{
    public ModificarUsuarioValidator()
    {
        RuleFor(x => x.IdUsuario)
            .GreaterThan(0).WithMessage("El identificador del usuario debe ser mayor a cero.");

        RuleFor(x => x.NombreCompleto)
            .NotEmpty().WithMessage("El nombre completo es obligatorio.")
            .MinimumLength(3).WithMessage("El nombre completo debe contener al menos 3 caracteres.")
            .MaximumLength(100).WithMessage("El nombre completo no puede exceder 100 caracteres.");

        RuleFor(x => x.Rol)
            .IsInEnum().WithMessage("El rol de usuario especificado no es válido.");
    }
}
