namespace Retail.App.Services;

/// <summary>
/// Preferencias de la persona que usa el equipo (hoy solo el tema visual). Se guardan en el perfil de Windows,
/// no en la base de datos: son una comodidad de cada puesto, no un dato del negocio.
/// </summary>
public interface IPreferenciasUsuario
{
    /// <summary>Tema elegido. Es <see cref="TemaVisual.Claro"/> si nunca se guardó nada.</summary>
    TemaVisual Tema { get; set; }

    /// <summary>Lee las preferencias del archivo. Si no existe o está dañado, deja los valores por defecto.</summary>
    void Cargar();

    /// <summary>Escribe las preferencias en el archivo.</summary>
    void Guardar();
}
