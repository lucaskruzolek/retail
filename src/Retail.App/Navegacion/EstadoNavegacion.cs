using Retail.Domain.Enums;

namespace Retail.App.Navegacion;

/// <summary>
/// Dónde está el operador: la página abierta y, por sección, la última que visitó. Las vistas no se conservan al
/// navegar (cada una se crea de nuevo en su propio scope), así que la memoria de "volver a donde estaba" vive acá.
/// </summary>
public sealed class EstadoNavegacion
{
    private readonly Dictionary<string, string> _ultimaPorSeccion = [];

    public string? PaginaActualId { get; private set; }

    public string? SeccionActualId { get; private set; }

    public event Action? Cambio;

    /// <summary>Anota la página que se acaba de abrir.</summary>
    public void Registrar(string paginaId)
    {
        var ubicacion = RegistroNavegacion.Buscar(paginaId)
            ?? throw new ArgumentException($"No existe la página '{paginaId}'.", nameof(paginaId));

        PaginaActualId = ubicacion.Pagina.Id;
        SeccionActualId = ubicacion.Seccion.Id;
        _ultimaPorSeccion[ubicacion.Seccion.Id] = ubicacion.Pagina.Id;
        Cambio?.Invoke();
    }

    /// <summary>
    /// Página a la que lleva el acceso de una sección: la última visitada si el rol todavía puede abrirla, y si no la
    /// primera que puede abrir. <c>null</c> si no puede abrir ninguna.
    /// </summary>
    public PaginaNavegacion? PaginaDeEntrada(SeccionNavegacion seccion, RolUsuarioEnum? rol)
    {
        var visibles = RegistroNavegacion.PaginasVisibles(seccion, rol);

        if (_ultimaPorSeccion.TryGetValue(seccion.Id, out var ultima))
        {
            var recordada = visibles.FirstOrDefault(p => p.Id == ultima);
            if (recordada is not null)
            {
                return recordada;
            }
        }

        return visibles.Count > 0 ? visibles[0] : null;
    }

    /// <summary>Olvida todo: se usa al cambiar de operador, que arranca desde su página inicial.</summary>
    public void Reiniciar()
    {
        _ultimaPorSeccion.Clear();
        PaginaActualId = null;
        SeccionActualId = null;
        Cambio?.Invoke();
    }
}
