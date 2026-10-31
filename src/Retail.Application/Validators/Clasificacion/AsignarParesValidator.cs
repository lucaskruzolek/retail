using FluentValidation;
using Retail.Application.DTOs.Clasificacion;
using Retail.Domain.Common;

namespace Retail.Application.Validators.Clasificacion;

/// <summary>
/// Validador de la asignación de varios pares en una sola operación (Track 6.6, Fase 5). Cada par se valida con
/// <see cref="AsignarParValidator"/>, y la lista no puede repetir un par: el segundo pisaría al primero sin aviso.
/// </summary>
public class AsignarParesValidator : AbstractValidator<AsignarParesDto>
{
    public AsignarParesValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        // En una sola cadena: con CascadeMode.Stop, una lista vacía no llega a la regla de repetidos.
        RuleFor(x => x.Pares)
            .NotEmpty().WithMessage("Indique al menos un par.")
            .Must(NoRepetirPares)
            .WithMessage("La lista repite un par: cada término y segunda palabra puede aparecer una sola vez.");

        RuleForEach(x => x.Pares)
            .SetValidator(new AsignarParValidator());
    }

    // Se compara en la forma de comparación: "geometria" y "GEOMETRÍA." son el mismo par. Los pares inválidos ya los
    // informa la regla de cada elemento.
    private static bool NoRepetirPares(IReadOnlyList<AsignarParDto> pares)
    {
        var claves = pares
            .Where(p => ReglasClasificacion.EsSegundaPalabraValida(p.SegundaPalabra))
            .Select(p => (p.IdTermino, ReglasClasificacion.ExigirSegundaPalabra(p.SegundaPalabra)))
            .ToList();

        return claves.Distinct().Count() == claves.Count;
    }
}
