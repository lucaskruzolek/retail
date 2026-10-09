using System.Text;
using FluentValidation;
using Retail.Domain.Common;
using Retail.Domain.Entities;
using Retail.Domain.Enums;

namespace Retail.Application.Validators.Common;

/// <summary>
/// Reglas de FluentValidation compartidas por los validadores de datos maestros. No definen reglas propias:
/// delegan en los predicados del Dominio (<see cref="ReglasTexto"/>, <see cref="ReglasContacto"/>,
/// <see cref="ReglasDocumento"/>) y solo agregan el mensaje para el usuario, de modo que la frontera y el agregado
/// nunca discrepan (una sola fuente de verdad).
/// </summary>
/// <remarks>
/// Los validadores que las usan fijan <c>RuleLevelCascadeMode = CascadeMode.Stop</c>, para que un campo vacío
/// muestre solo "Complete ..." y no también el error de formato.
/// </remarks>
public static class ReglasComunesExtensions
{
    public const int LongitudMinimaPassword = 6;

    // BCrypt solo procesa los primeros 72 bytes de la contraseña: a partir de ahí, dos contraseñas distintas que
    // compartan ese prefijo darían el mismo hash. Se mide en bytes UTF-8 porque una "ñ" o una tilde ocupan dos.
    public const int LongitudMaximaPasswordBytes = 72;

    /// <param name="campo">Nombre del campo con su artículo, tal como aparece en el mensaje ("el apellido").</param>
    public static IRuleBuilderOptions<T, string> NombreDePersona<T>(this IRuleBuilder<T, string> ruleBuilder, string campo)
    {
        return ruleBuilder
            .NotEmpty().WithMessage($"Complete {campo}.")
            .Must(valor => ReglasTexto.EsNombreDePersonaValido(valor))
            .WithMessage(
                $"Revise {campo}: solo admite letras, espacios, apóstrofos o guiones, entre " +
                $"{ReglasTexto.LongitudMinimaNombrePersona} y {ReglasTexto.LongitudMaximaNombrePersona} caracteres.");
    }

    public static IRuleBuilderOptions<T, string> NombreDeUsuario<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty().WithMessage("Complete el nombre de usuario.")
            .Must(valor => ReglasTexto.EsNombreUsuarioValido(valor))
            .WithMessage(
                "Revise el nombre de usuario: debe empezar con una letra y solo admite letras sin tilde, números, " +
                $"puntos o guiones bajos no consecutivos, entre {ReglasTexto.LongitudMinimaNombreUsuario} y " +
                $"{ReglasTexto.LongitudMaximaNombreUsuario} caracteres.");
    }

    /// <summary>
    /// La contraseña es la única regla que no vive en el Dominio: el agregado solo recibe el hash, nunca el
    /// texto plano, así que la longitud se controla en la frontera, antes de calcular el hash.
    /// </summary>
    public static IRuleBuilderOptions<T, string> Contrasena<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty().WithMessage("Complete la contraseña.")
            .MinimumLength(LongitudMinimaPassword)
            .WithMessage($"La contraseña debe tener al menos {LongitudMinimaPassword} caracteres.")
            .Must(valor => Encoding.UTF8.GetByteCount(valor) <= LongitudMaximaPasswordBytes)
            .WithMessage(
                $"La contraseña es demasiado larga: admite hasta {LongitudMaximaPasswordBytes} bytes " +
                "(unos 72 caracteres sin tildes).");
    }

    /// <summary>
    /// El nombre de un cliente se valida como nombre de persona o como razón social según su tipo de documento;
    /// la decisión la toma el agregado (<see cref="Cliente.EsNombreValido"/>), no el validador.
    /// </summary>
    public static IRuleBuilderOptions<T, string> NombreDeCliente<T>(
        this IRuleBuilder<T, string> ruleBuilder,
        Func<T, TipoDocumentoEnum> tipoDocumento)
    {
        return ruleBuilder
            .NotEmpty().WithMessage("Complete el nombre o la razón social del cliente.")
            .Must((dto, valor) => Cliente.EsNombreValido(tipoDocumento(dto), valor))
            .WithMessage((dto, _) => ReglasDocumento.IdentificaPersonaFisica(tipoDocumento(dto))
                ? "Revise el nombre del cliente: con DNI, CUIL o pasaporte es una persona física, así que solo " +
                  "admite letras, espacios, apóstrofos o guiones."
                : "Revise la razón social del cliente: debe contener al menos una letra y solo admite letras, " +
                  "números y la puntuación . , & ' - ( ) / º ª °.");
    }

    public static IRuleBuilderOptions<T, string> DocumentoSegunTipo<T>(
        this IRuleBuilder<T, string> ruleBuilder,
        Func<T, TipoDocumentoEnum> tipoDocumento)
    {
        return ruleBuilder
            .NotEmpty().WithMessage("Complete el número de documento.")
            .Must((dto, valor) => ReglasDocumento.EsValido(tipoDocumento(dto), valor))
            .WithMessage((dto, _) => $"Revise el número de documento: no es un {ReglasDocumento.DescribirFormato(tipoDocumento(dto))}.");
    }

    public static IRuleBuilderOptions<T, string?> DomicilioOpcional<T>(this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
            .Must(valor => string.IsNullOrWhiteSpace(valor) || ReglasTexto.EsDomicilioValido(valor))
            .WithMessage(
                "Revise el domicilio: debe contener al menos una letra, entre " +
                $"{ReglasTexto.LongitudMinimaDomicilio} y {ReglasTexto.LongitudMaximaDomicilio} caracteres.");
    }

    public static IRuleBuilderOptions<T, string?> EmailOpcional<T>(this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
            .Must(valor => string.IsNullOrWhiteSpace(valor) || ReglasContacto.EsEmailValido(valor))
            .WithMessage(
                "Revise el email: debe tener la forma usuario@dominio.ext y no superar " +
                $"{ReglasContacto.LongitudMaximaEmail} caracteres.");
    }

    public static IRuleBuilderOptions<T, string?> TelefonoOpcional<T>(this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
            .Must(valor => string.IsNullOrWhiteSpace(valor) || ReglasContacto.EsTelefonoValido(valor))
            .WithMessage(
                $"Revise el teléfono: debe contener entre {ReglasContacto.DigitosMinimosTelefono} y " +
                $"{ReglasContacto.DigitosMaximosTelefono} dígitos; se admiten espacios, guiones, paréntesis y un '+' inicial.");
    }
}
