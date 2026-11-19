using Retail.App.Helpers;
using Retail.App.ViewModels.Listas;
using Retail.Application.DTOs.Listas;

namespace Retail.App.Services;

/// <summary>
/// Diálogos del módulo de listas de artículos (RF-30). Desacopla los ViewModels de las ventanas WPF para que sus flujos
/// se prueben sin interfaz. La pestaña Listas usa el editor, la confirmación de la baja y la elección de la ruta del PDF.
/// </summary>
public interface IListaDialogService
{
    /// <summary>
    /// Abre el editor de una lista: una nueva (<paramref name="lista"/> en <c>null</c>) o la existente. Bloquea hasta que
    /// se cierra y devuelve <c>true</c> si se guardó. <paramref name="acceso"/> es la fila de accesos a datos de la
    /// pantalla que lo abre: el editor comparte su DbContext y no debe usarlo en paralelo (H-19).
    /// </summary>
    bool AbrirEditor(ListaDto? lista, CargaSerializada acceso);

    /// <summary>
    /// Abre el selector "Cargar → Lista" del POS (F7). Bloquea hasta que se cierra y devuelve el número de la lista elegida,
    /// o <c>null</c> si el usuario canceló.
    /// </summary>
    int? MostrarSeleccionarLista(SeleccionarListaViewModel viewModel);

    /// <summary>
    /// Abre el diálogo "Guardar como → Lista" del POS (F3). Bloquea hasta que se cierra y devuelve <c>true</c> si la lista
    /// quedó guardada (el resultado está en <see cref="GuardarListaViewModel.ListaGuardada"/>).
    /// </summary>
    bool MostrarGuardarLista(GuardarListaViewModel viewModel);

    /// <summary>
    /// Pregunta qué hacer al cargar la lista <paramref name="nombreLista"/> con el ticket no vacío (L-14). Con un
    /// presupuesto cargado <paramref name="permitirAgregar"/> es <c>false</c> y solo se ofrece Reemplazar o Cancelar (L-15).
    /// </summary>
    RespuestaCargaLista PreguntarReemplazarOAgregar(string nombreLista, bool permitirAgregar);

    /// <summary>Pide confirmar la baja de la lista; devuelve <c>true</c> si el usuario acepta.</summary>
    bool ConfirmarBaja(string nombre);

    /// <summary>Pregunta por sí o no; devuelve <c>true</c> si el usuario acepta.</summary>
    bool Confirmar(string titulo, string mensaje);

    /// <summary>Abre el cuadro de Windows para guardar el PDF. Devuelve la ruta elegida, o <c>null</c> si se cancela.</summary>
    string? ElegirRutaPdf(string nombreSugerido);

    void MostrarInformacion(string titulo, string mensaje);

    void MostrarAlerta(string titulo, string mensaje);

    void MostrarError(string titulo, string mensaje);
}
