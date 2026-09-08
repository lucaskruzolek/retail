namespace Retail.Domain.Enums;

/// <summary>
/// Estado de autorización fiscal ante ARCA (AFIP) para una venta en mostrador.
/// </summary>
public enum EstadoFiscalEnum
{
    NoAplica = 0,
    Emitido = 1,
    ErrorFiscalReintentable = 2
}
