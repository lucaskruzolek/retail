using Retail.App.ViewModels.Presupuestos;
using Retail.Application.DTOs.Presupuestos;

namespace Retail.App.Services;

/// <summary>
/// Diálogos del módulo de presupuestos (RF-11, RF-12). Desacopla los ViewModels de las ventanas WPF para que sus
/// flujos se prueben sin interfaz. La consulta (P4) usa el detalle, la confirmación y la elección de la ruta del PDF;
/// los diálogos del Punto de venta (guardar, recuperar y alerta de precios, P5) están en esta misma interfaz porque sus
/// ViewModels también usan la elección de la ruta del PDF y los avisos de aquí (<c>FlujoExportarPdfPresupuesto</c>).
/// </summary>
public interface IPresupuestoDialogService
{
    /// <summary>
    /// Abre el diálogo "Guardar como presupuesto" (F3). Bloquea hasta que se cierra y devuelve <c>true</c> si el
    /// presupuesto quedó guardado: el POS entonces limpia el ticket sin pedir confirmación.
    /// </summary>
    bool MostrarGuardarPresupuesto(GuardarPresupuestoViewModel viewModel);

    /// <summary>
    /// Abre el diálogo "Recuperar presupuesto" (F7). Devuelve el presupuesto recuperado, o <c>null</c> si se canceló.
    /// </summary>
    PresupuestoParaVentaDto? MostrarRecuperarPresupuesto(RecuperarPresupuestoViewModel viewModel);

    /// <summary>
    /// Abre la alerta de diferencias de precio, faltantes y artículos dados de baja. Devuelve <c>true</c> si el usuario
    /// elige Continuar.
    /// </summary>
    bool MostrarAlertaPreciosPresupuesto(AlertaPreciosPresupuestoViewModel viewModel);

    /// <summary>Abre, de solo lectura, el detalle del presupuesto. Bloquea hasta que el usuario lo cierra.</summary>
    void MostrarDetalle(int idPresupuesto);

    /// <summary>Pregunta por sí o no; devuelve <c>true</c> si el usuario acepta.</summary>
    bool Confirmar(string titulo, string mensaje);

    /// <summary>
    /// Abre el cuadro de Windows para guardar el PDF. Devuelve la ruta elegida, o <c>null</c> si el usuario cancela.
    /// </summary>
    string? ElegirRutaPdf(string nombreSugerido);

    void MostrarInformacion(string titulo, string mensaje);

    void MostrarAlerta(string titulo, string mensaje);

    void MostrarError(string titulo, string mensaje);
}
