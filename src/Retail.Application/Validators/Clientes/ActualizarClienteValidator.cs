using FluentValidation;
using Retail.Application.DTOs.Clientes;
using Retail.Application.Validators.Common;

namespace Retail.Application.Validators.Clientes;

/// <summary>
/// Validador declarativo para la modificación de clientes comerciales (RF-20). Usa las mismas reglas que el alta.
/// </summary>
public class ActualizarClienteValidator : AbstractValidator<ActualizarClienteDto>
{
    public ActualizarClienteValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.IdCliente)
            .GreaterThan(0).WithMessage("El identificador de cliente debe ser mayor a cero.");

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
