using FluentValidation;
using Retail.Application.DTOs.Listas;
using Retail.Domain.Common;
using Retail.Domain.Entities;

namespace Retail.Application.Validators.Listas;

/// <summary>
/// Validador de frontera para crear una lista (RF-30). El nombre se mide normalizado, igual que lo hace el agregado
/// <see cref="ListaArticulos"/>: así el borde y el dominio nunca discrepan.
/// </summary>
public class CrearListaValidator : AbstractValidator<CrearListaDto>
{
    public CrearListaValidator()
    {
        RuleFor(x => x.IdUsuario)
            .GreaterThan(0).WithMessage("La lista debe registrar al usuario que la crea.");

        RuleFor(x => x.Nombre)
            .Must(NombreTieneTexto).WithMessage("Debe indicarse el nombre de la lista.")
            .Must(NombreNoExcedeElMaximo)
            .WithMessage($"El nombre de la lista no puede superar los {ListaArticulos.LongitudMaximaNombre} caracteres.");

        RuleFor(x => x.Renglones)
            .NotEmpty().WithMessage("La lista debe tener al menos un artículo.");

        RuleForEach(x => x.Renglones).SetValidator(new RenglonListaEntradaValidator());
    }

    internal static bool NombreTieneTexto(string? nombre)
    {
        return !string.IsNullOrWhiteSpace(nombre);
    }

    internal static bool NombreNoExcedeElMaximo(string? nombre)
    {
        return string.IsNullOrWhiteSpace(nombre)
            || ReglasTexto.NormalizarEspacios(nombre).Length <= ListaArticulos.LongitudMaximaNombre;
    }
}
