using System.IO;
using Retail.Application.DTOs.Reportes;

namespace Retail.App.Services;

/// <summary>
/// Diálogos y archivos de la exportación de reportes (RF-26). Desacopla el ViewModel de los diálogos del sistema
/// operativo y del disco para poder probarlo sin pantalla ni archivos reales.
/// </summary>
public interface IReporteDialogService
{
    /// <summary>
    /// Muestra el diálogo "Guardar como" con el filtro del <paramref name="formato"/>. Devuelve la ruta elegida, o
    /// <c>null</c> si el Gerente canceló.
    /// </summary>
    string? ElegirArchivoDestino(string nombreSugerido, FormatoExportacionEnum formato);

    /// <summary>
    /// Abre (y trunca si existe) el archivo de destino para escritura asíncrona y sin compartir. Lanza
    /// <see cref="IOException"/> o <see cref="UnauthorizedAccessException"/> si no se puede escribir, por ejemplo
    /// porque el archivo está abierto en Excel. Vive acá y no en el ViewModel para poder simular esos fallos en las
    /// pruebas.
    /// </summary>
    Stream AbrirArchivoDestino(string ruta);

    /// <summary>
    /// Borra el archivo que quedó a medio escribir después de un fallo. No lanza si no se puede borrar: es una limpieza
    /// y el error original es el que importa.
    /// </summary>
    void EliminarArchivoParcial(string ruta);

    /// <summary>Pregunta si se reintenta guardar el archivo (Sí/No). Devuelve <c>true</c> si el Gerente acepta.</summary>
    bool ConfirmarReintento(string mensaje);

    void MostrarExito(string mensaje);
}
