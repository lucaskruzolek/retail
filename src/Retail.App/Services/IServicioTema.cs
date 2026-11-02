namespace Retail.App.Services;

/// <summary>
/// Cambia el tema visual de toda la aplicación sin reiniciarla.
/// </summary>
public interface IServicioTema
{
    /// <summary>Aplica el tema indicado a la aplicación en ejecución. Es idempotente. Debe llamarse en el hilo de UI.</summary>
    void Aplicar(TemaVisual tema);

    /// <summary>Lee las preferencias guardadas y aplica el tema que contienen (claro si no hay nada guardado).</summary>
    void AplicarGuardado();
}
