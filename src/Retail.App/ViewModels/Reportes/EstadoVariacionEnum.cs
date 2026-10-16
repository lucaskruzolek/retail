namespace Retail.App.ViewModels.Reportes;

/// <summary>Sentido de la variación de un indicador contra el período anterior; elige el color del badge.</summary>
public enum EstadoVariacionEnum
{
    /// <summary>El indicador creció (badge de éxito).</summary>
    Sube,

    /// <summary>El indicador bajó (badge de peligro).</summary>
    Baja,

    /// <summary>No hay base de comparación, o no cambió (badge neutro). El color nunca es la única señal: el texto lleva la flecha y el signo.</summary>
    SinBase
}
