using FluentValidation;
using Retail.Application.DTOs.Usuarios;
using Retail.Application.Validators.Common;
using Retail.Domain.Enums;

namespace Retail.Application.Validators.Usuarios;

/// <summary>
/// Validador declarativo de reglas de negocio para la modificación de datos de operadores (RF-03).
/// </summary>
public class ModificarUsuarioValidator : AbstractValidator<ModificarUsuarioDto>
{
    public ModificarUsuarioValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.IdUsuario)
            .GreaterThan(0).WithMessage("El identificador del usuario debe ser mayor a cero.");

        RuleFor(x => x.Nombre)
            .NombreDePersona("el nombre");

        RuleFor(x => x.Apellido)
            .NombreDePersona("el apellido");

        RuleFor(x => x.Rol)
            .IsInEnum().WithMessage("El rol de usuario especificado no es válido.");

        RuleFor(x => x.EmailRecuperacion)
            .EmailOpcional();

        // Misma invariante que Usuario.ActualizarDatos: se valida acá para mostrarla junto a los demás errores.
        RuleFor(x => x.EmailRecuperacion)
            .NotEmpty()
            .When(x => x.Rol == RolUsuarioEnum.Gerente)
            .WithMessage("El email de recuperación es obligatorio para el rol Gerente.");
    }
}
