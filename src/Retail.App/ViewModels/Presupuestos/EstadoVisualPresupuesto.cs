namespace Retail.App.ViewModels.Presupuestos;

/// <summary>
/// Cómo se ve el estado de un presupuesto en pantalla (badge). No es un estado del dominio: "vigente" y "vencido" son
/// dos caras del estado <c>Pendiente</c> que se distinguen por la fecha de vencimiento (PS-08), así que la presentación
/// combina el estado persistido con la vigencia que calculó el servidor.
/// </summary>
public enum EstadoVisualPresupuesto
{
    Vigente,
    Vencido,
    Convertido,
    Anulado,
    Reemplazado
}
