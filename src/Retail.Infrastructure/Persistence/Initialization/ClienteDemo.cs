using Retail.Domain.Enums;

namespace Retail.Infrastructure.Persistence.Initialization;

/// <summary>
/// Un cliente de demostración. Sin <see cref="LimiteCredito"/> no tiene cuenta corriente; uno moroso compra a crédito y
/// nunca paga, así que llega al final del historial cerca de su límite.
/// </summary>
internal sealed record ClienteDemo(
    string Nombre,
    TipoDocumentoEnum TipoDocumento,
    string NumeroDocumento,
    CondicionIvaEnum CondicionIva,
    string? Telefono = null,
    string? Email = null,
    decimal? LimiteCredito = null,
    bool EsMoroso = false);
