using FluentValidation;
using Retail.Application.DTOs.Proveedores;

namespace Retail.Application.Validators.Proveedores;

/// <summary>
/// Validador de los filtros del explorador del catálogo de proveedores. La paginación no se valida: el servicio de
/// consulta ya normaliza una página o un tamaño inválidos.
/// </summary>
public class ConsultaCatalogoProveedorValidator : AbstractValidator<ConsultaCatalogoProveedorDto>
{
    public ConsultaCatalogoProveedorValidator()
    {
        RuleFor(x => x.IdProveedor)
            .GreaterThan(0).When(x => x.IdProveedor.HasValue)
            .WithMessage("El identificador del proveedor debe ser válido.");

        RuleFor(x => x.IdCategoria)
            .GreaterThan(0).When(x => x.IdCategoria.HasValue)
            .WithMessage("El identificador de la categoría debe ser válido.");

        RuleFor(x => x.IdMarca)
            .GreaterThan(0).When(x => x.IdMarca.HasValue)
            .WithMessage("El identificador de la marca debe ser válido.");

        // "Sin categoría" y una categoría se excluyen: juntos siempre darían una página vacía (M-74).
        RuleFor(x => x.SoloSinCategoria)
            .Equal(false).When(x => x.IdCategoria.HasValue)
            .WithMessage("No se puede filtrar por una categoría y por los ítems sin categoría a la vez.");
    }
}
