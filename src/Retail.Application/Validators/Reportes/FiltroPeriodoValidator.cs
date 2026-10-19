using Retail.Application.DTOs.Reportes;

namespace Retail.Application.Validators.Reportes;

/// <summary>Valida el período del tablero gerencial.</summary>
public class FiltroPeriodoValidator : FiltroPeriodoValidatorBase<FiltroPeriodoDto>
{
    public FiltroPeriodoValidator(TimeProvider reloj)
        : base(reloj)
    {
    }
}
