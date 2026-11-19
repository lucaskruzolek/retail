namespace Retail.App.Services;

/// <summary>
/// Respuesta del cajero cuando carga una lista con el ticket no vacío (L-14, L-15).
/// </summary>
public enum RespuestaCargaLista
{
    /// <summary>No hacer nada: el ticket queda como estaba.</summary>
    Cancelar,

    /// <summary>Vaciar el ticket y cargar la lista.</summary>
    Reemplazar,

    /// <summary>Sumar los artículos de la lista a los que ya están en el ticket.</summary>
    Agregar
}
