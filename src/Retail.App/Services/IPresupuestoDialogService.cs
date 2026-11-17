namespace Retail.App.Services;

/// <summary>
/// Diálogos del módulo de presupuestos (RF-11, RF-12). Desacopla los ViewModels de las ventanas WPF para que sus
/// flujos se prueben sin interfaz. La consulta (P4) usa el detalle, la confirmación y la elección de la ruta del PDF;
/// los diálogos del Punto de venta (guardar y recuperar, P5) se suman a esta misma interfaz.
/// </summary>
public interface IPresupuestoDialogService
{
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
