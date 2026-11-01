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

        // Un id positivo, o el centinela de "Sin categoría" / "Sin marca" (M-78). Combinarlos es una unión válida.
        RuleForEach(x => x.IdsCategoria)
            .Must(id => id > 0 || id == ConsultaCatalogoProveedorDto.IdSinCategoria)
            .WithMessage("El identificador de la categoría debe ser válido.");

        RuleForEach(x => x.IdsMarca)
            .Must(id => id > 0 || id == ConsultaCatalogoProveedorDto.IdSinMarca)
            .WithMessage("El identificador de la marca debe ser válido.");
    }
}
