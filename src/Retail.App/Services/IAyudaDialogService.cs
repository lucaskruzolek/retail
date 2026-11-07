namespace Retail.App.Services;

/// <summary>
/// Contrato del servicio de presentación y soporte para el Centro de Ayuda y apertura del manual de usuario.
/// </summary>
public interface IAyudaDialogService
{
    /// <summary>
    /// Despliega el diálogo modal con la guía rápida de atajos de mostrador y opciones de soporte.
    /// </summary>
    void MostrarCentroAyuda();

    /// <summary>
    /// Abre el manual de usuario en formato PDF mediante el visor predeterminado del sistema operativo.
    /// </summary>
    /// <returns>Verdadero si el archivo existe y pudo iniciarse el proceso; falso en caso contrario.</returns>
    bool AbrirManualPdf();

    /// <summary>
    /// Obtiene la ruta física esperada o resuelta del archivo de manual de usuario.
    /// </summary>
    string ObtenerRutaManual();
}

