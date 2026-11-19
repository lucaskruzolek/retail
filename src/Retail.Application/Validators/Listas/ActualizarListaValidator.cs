using FluentValidation;
using Retail.Application.DTOs.Listas;
using Retail.Domain.Entities;

namespace Retail.Application.Validators.Listas;

/// <summary>
/// Validador de frontera para actualizar una lista (RF-30): mismas reglas de nombre y renglones que al crearla.
/// </summary>
public class ActualizarListaValidator : AbstractValidator<ActualizarListaDto>
{
    public ActualizarListaValidator()
    {
        RuleFor(x => x.Nombre)
            .Must(CrearListaValidator.NombreTieneTexto).WithMessage("Debe indicarse el nombre de la lista.")
            .Must(CrearListaValidator.NombreNoExcedeElMaximo)
            .WithMessage($"El nombre de la lista no puede superar los {ListaArticulos.LongitudMaximaNombre} caracteres.");

        RuleFor(x => x.Renglones)
            .NotEmpty().WithMessage("La lista debe tener al menos un artículo.");

        RuleForEach(x => x.Renglones).SetValidator(new RenglonListaEntradaValidator());
    }
}
