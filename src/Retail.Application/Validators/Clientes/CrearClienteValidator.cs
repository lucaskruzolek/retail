using FluentValidation;
using Retail.Application.DTOs.Clientes;
using Retail.Application.Validators.Common;

namespace Retail.Application.Validators.Clientes;

/// <summary>
/// Validador declarativo para el alta de nuevos clientes comerciales (RF-20).
/// </summary>
public class CrearClienteValidator : AbstractValidator<CrearClienteDto>
{
    public CrearClienteValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.TipoDocumento)
            .IsInEnum().WithMessage("Debe especificar un tipo de documento válido.");

        RuleFor(x => x.RazonSocialONombre)
            .NombreDeCliente(x => x.TipoDocumento);

        RuleFor(x => x.NumeroDocumento)
            .DocumentoSegunTipo(x => x.TipoDocumento);

        RuleFor(x => x.CondicionIva)
            .IsInEnum().WithMessage("Debe especificar una condición de IVA válida.");

        RuleFor(x => x.LimiteCredito)
            .GreaterThanOrEqualTo(0m).WithMessage("El límite de crédito no puede ser negativo.");

        RuleFor(x => x.DomicilioFiscal)
            .DomicilioOpcional();

        RuleFor(x => x.Telefono)
            .TelefonoOpcional();

        RuleFor(x => x.Email)
            .EmailOpcional();
    }
}
