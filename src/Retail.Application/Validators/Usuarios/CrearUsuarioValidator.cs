using FluentValidation;
using Retail.Application.DTOs.Usuarios;
using Retail.Application.Validators.Common;
using Retail.Domain.Common;
using Retail.Domain.Enums;

namespace Retail.Application.Validators.Usuarios;

/// <summary>
/// Validador declarativo de reglas de negocio para el alta de nuevos usuarios (RF-03).
/// </summary>
public class CrearUsuarioValidator : AbstractValidator<CrearUsuarioDto>
{
    public CrearUsuarioValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.NombreUsuario)
            .NombreDeUsuario();

        RuleFor(x => x.Nombre)
            .NombreDePersona("el nombre");

        RuleFor(x => x.Apellido)
            .NombreDePersona("el apellido");

        RuleFor(x => x.Password)
            .Contrasena()
            .Must((dto, password) => !EsIgualAlNombreUsuario(password, dto.NombreUsuario))
            .WithMessage("La contraseña no puede ser igual al nombre de usuario.");

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

    private static bool EsIgualAlNombreUsuario(string password, string? nombreUsuario)
    {
        return !string.IsNullOrWhiteSpace(nombreUsuario)
            && string.Equals(
                password.Trim(),
                ReglasTexto.NormalizarNombreUsuario(nombreUsuario),
                StringComparison.OrdinalIgnoreCase);
    }
}
