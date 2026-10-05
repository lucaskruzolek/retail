using Retail.Application.DTOs.Caja;

namespace Retail.App.Services;

/// <summary>
/// Contrato para la interacción con diálogos y ventanas modales del módulo de caja y tesorería.
/// </summary>
public interface ICajaDialogService
{
    bool? MostrarDialogoApertura(out decimal saldoInicial);

    MovimientoCajaDto? MostrarDialogoMovimiento(int idTurno);

    bool? MostrarDialogoArqueo(int idTurno, out decimal saldoDeclarado, out decimal montoRetenido);

    void MostrarInformacion(string titulo, string mensaje);

    void MostrarError(string titulo, string mensaje);

    void MostrarAdvertencia(string titulo, string mensaje);
}