using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Retail.Application.DTOs.Presupuestos;
using Retail.Application.DTOs.Ventas;

namespace Retail.App.ViewModels.Presupuestos;

/// <summary>
/// Alerta previa a cargar un presupuesto recuperado en el POS (RF-12): informa lo que cambió desde que se emitió y pide
/// confirmar. No permite elegir renglón por renglón (PS-03): o se carga el presupuesto como está o se cancela.
/// Muestra, según lo que traiga el presupuesto: las diferencias de precio (solo si venció), los faltantes de stock con la
/// sugerencia de fraccionar y los artículos dados de baja, que no se cargan.
/// </summary>
public partial class AlertaPreciosPresupuestoViewModel : ObservableObject
{
    private static readonly CultureInfo CulturaArgentina = CultureInfo.GetCultureInfo("es-AR");

    public AlertaPreciosPresupuestoViewModel(PresupuestoParaVentaDto presupuesto)
    {
        ArgumentNullException.ThrowIfNull(presupuesto);

        TituloVentana = $"Presupuesto N.º {PresentacionPresupuesto.FormatearNumero(presupuesto.IdPresupuesto)}";

        // Las diferencias de precio solo importan si venció: vigente, se cobra lo pactado (PS-02).
        if (!presupuesto.EstaVigente)
        {
            Discrepancias = presupuesto.Discrepancias.Select(CrearFila).ToList();
        }

        Faltantes = presupuesto.Faltantes.Select(CrearFilaFaltante).ToList();
        ArticulosNoDisponibles = presupuesto.ArticulosNoDisponibles;

        TextoIntroduccion = presupuesto.EstaVigente
            ? "El presupuesto está vigente: se cobra con los precios pactados. Revise lo siguiente antes de cargarlo."
            : "El presupuesto venció: sus artículos se cargan al precio de catálogo de hoy. Revise lo siguiente antes de cargarlo.";
    }

    public string TituloVentana { get; }

    public string TextoIntroduccion { get; }

    public IReadOnlyList<FilaDiscrepanciaPrecio> Discrepancias { get; } = [];

    public IReadOnlyList<FilaFaltanteStock> Faltantes { get; }

    public IReadOnlyList<string> ArticulosNoDisponibles { get; }

    public bool TieneDiscrepancias => Discrepancias.Count > 0;

    public bool TieneFaltantes => Faltantes.Count > 0;

    public bool TieneArticulosNoDisponibles => ArticulosNoDisponibles.Count > 0;

    /// <summary>El usuario eligió Continuar: el POS carga el presupuesto.</summary>
    [ObservableProperty]
    private bool _confirmado;

    private static FilaDiscrepanciaPrecio CrearFila(DiscrepanciaPrecioPresupuestoDto discrepancia)
    {
        decimal diferencia = discrepancia.DiferenciaUnitario;
        string signo = diferencia > 0m ? "+" : string.Empty;

        return new FilaDiscrepanciaPrecio(
            discrepancia.Descripcion,
            discrepancia.PrecioPactado.ToString("C2", CulturaArgentina),
            discrepancia.PrecioActualCatalogo.ToString("C2", CulturaArgentina),
            signo + diferencia.ToString("C2", CulturaArgentina),
            discrepancia.HuboAumento);
    }

    private static FilaFaltanteStock CrearFilaFaltante(FaltanteStockDto faltante)
    {
        string texto = $"{faltante.Descripcion}: se piden {faltante.CantidadSolicitada} y hay {faltante.StockActual}.";

        string? sugerencia = faltante.PuedeFraccionar
            ? $"Se puede fraccionar {faltante.OrigenesAFraccionar} de '{faltante.DescripcionOrigen}'; el cobro se lo va a ofrecer."
            : null;

        return new FilaFaltanteStock(texto, sugerencia);
    }
}
