using QuestPDF.Infrastructure;

namespace Retail.Infrastructure.Pdf;

/// <summary>
/// Configuración global de QuestPDF compartida por todos los generadores de PDF (reportes y presupuestos). Vive en un
/// único lugar para que cada exportador la use sin duplicarla y sin depender de que otro se haya ejecutado antes.
/// </summary>
internal static class ConfiguracionQuestPdf
{
    private static readonly object Candado = new();
    private static bool _inicializada;

    /// <summary>
    /// Fuente de los documentos: la misma que la interfaz (sistema de diseño), instalada en Windows 10 y 11. R-12: la
    /// fuente por defecto de QuestPDF (Lato) se distribuye como un archivo suelto que el ejecutable único no embebe, y
    /// desde QuestPDF 2026.9.0 una fuente faltante detiene la generación del PDF en lugar de reemplazarse.
    /// </summary>
    internal const string FuenteDelDocumento = "Segoe UI";

    /// <summary>
    /// Fija la licencia y las opciones de fuentes de QuestPDF. Es idempotente: cada exportador la invoca antes de
    /// generar un documento y solo la primera llamada tiene efecto.
    /// </summary>
    public static void Inicializar()
    {
        if (_inicializada)
        {
            return;
        }

        lock (Candado)
        {
            if (_inicializada)
            {
                return;
            }

            // Licencia Community de QuestPDF (MIT): gratuita para uso educativo y para empresas con facturación anual
            // bruta menor a USD 1 M. Sin esta línea, QuestPDF lanza una excepción al generar el primer documento.
            QuestPDF.Settings.License = LicenseType.Community;

            // R-12: se usan las fuentes instaladas en Windows (Segoe UI) en lugar de depender de un archivo junto al .exe.
            QuestPDF.Settings.UseSystemFonts = true;

            // QuestPDF agrega Lato como familia de respaldo después de la elegida ("Segoe UI", "Lato"), y con este control
            // habilitado (valor por defecto desde 2026.9.0) detiene el PDF si falta CUALQUIER familia de la lista, aunque
            // la primera esté disponible: el .exe copiado sin QuestPDF.Fonts.Lato.br fallaba siempre (verificado en la
            // prueba R-5). Deshabilitado, usa la primera familia disponible; Segoe UI viene con Windows y Lato viaja en
            // el ZIP de la release. ThrowOnMissingTextGlyphs queda habilitado: un carácter que ninguna fuente puede
            // dibujar sigue deteniendo la generación en lugar de imprimirse mal.
            QuestPDF.Settings.ThrowOnMissingFontFamilies = false;

            _inicializada = true;
        }
    }
}
