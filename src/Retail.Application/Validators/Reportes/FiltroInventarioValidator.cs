using FluentValidation;
using Retail.Application.DTOs.Reportes;

namespace Retail.Application.Validators.Reportes;

/// <summary>Valida los filtros de los reportes de inventario.</summary>
public class FiltroInventarioValidator : AbstractValidator<FiltroInventarioDto>
{
    public FiltroInventarioValidator()
    {
        RuleFor(x => x.IdCategoria).GreaterThan(0).When(x => x.IdCategoria.HasValue).WithMessage("La categoría no es válida.");
        RuleFor(x => x.IdMarca).GreaterThan(0).When(x => x.IdMarca.HasValue).WithMessage("La marca no es válida.");
    }
}
