namespace Retail.App.ViewModels.Proveedores;

/// <summary>
/// Un paso del indicador de pasos del modal de incorporación (P6-3): número, título y si es el actual o ya quedó
/// atrás. Es solo lectura: el indicador informa el recorrido, no se usa para navegar.
/// </summary>
public sealed record PasoDelRecorrido(int Numero, string Titulo, bool EsActual, bool EsCompletado, bool EsUltimo);
