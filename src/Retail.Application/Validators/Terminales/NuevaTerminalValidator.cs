using FluentValidation;
using Retail.Application.DTOs.Terminales;
using Retail.Domain.Common;
using Retail.Domain.Entities;

namespace Retail.Application.Validators.Terminales;

/// <summary>
/// Validador de frontera para el alta de una terminal. El nombre se mide normalizado, igual que lo hace el agregado
/// <see cref="Terminal"/>.
/// </summary>
public class NuevaTerminalValidator : AbstractValidator<NuevaTerminalDto>
{
    public NuevaTerminalValidator()
    {
        RuleFor(x => x.Nombre)
            .Must(nombre => !string.IsNullOrWhiteSpace(nombre))
            .WithMessage("Debe indicarse el nombre de la terminal.")
            .Must(nombre => string.IsNullOrWhiteSpace(nombre)
                || ReglasTexto.NormalizarEspacios(nombre).Length <= Terminal.LongitudMaximaNombre)
            .WithMessage($"El nombre de la terminal no puede superar los {Terminal.LongitudMaximaNombre} caracteres.");
    }
}
