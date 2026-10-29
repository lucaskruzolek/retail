using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Retail.App.Converters;
using Retail.Application.DTOs.Pedidos;
using Retail.Domain.Common;

namespace Retail.App.ViewModels.Pedidos;

/// <summary>
/// Modal de pedido (A-17): muestra la vista previa del lote agrupada por proveedor, con el destino de cada grupo,
/// el stock y la cantidad ya pedida de cada renglón, y los ítems que no se pueden pedir. El usuario ajusta las
/// cantidades o quita renglones y confirma. No llama a servicios: quien lo abre guarda el lote con lo que devuelve
/// <see cref="ObtenerRenglones"/>.
/// </summary>
public partial class PedirArticulosViewModel : ObservableObject
{
    public PedirArticulosViewModel(VistaPreviaPedidoDto vistaPrevia)
    {
        ArgumentNullException.ThrowIfNull(vistaPrevia);

        foreach (var destino in vistaPrevia.Destinos)
        {
            var filas = vistaPrevia.Renglones
                .Where(r => r.IdProveedor == destino.IdProveedor)
                .Select(r => new RenglonPedirFila(r))
                .ToList();
            foreach (var fila in filas)
            {
                fila.PropertyChanged += Fila_PropertyChanged;
            }

            Grupos.Add(new GrupoPedidoProveedor(destino, filas));
        }

        Omitidos = vistaPrevia.Omitidos.Select(DescribirOmitido).ToList();
    }

    public ObservableCollection<GrupoPedidoProveedor> Grupos { get; } = new();

    /// <summary>Un texto por ítem que no se puede pedir: qué ítem es y por qué.</summary>
    public IReadOnlyList<string> Omitidos { get; }

    public bool TieneOmitidos => Omitidos.Count > 0;

    public int CantidadRenglones => Grupos.Sum(g => g.Renglones.Count);

    public decimal TotalEstimado => ReglasMonetarias.Redondear(
        Grupos.SelectMany(g => g.Renglones).Sum(r => r.Cantidad * r.CostoActual));

    public bool PuedeConfirmar => CantidadRenglones > 0 && Grupos.SelectMany(g => g.Renglones).All(r => r.EsValida);

    [RelayCommand]
    private void QuitarRenglon(RenglonPedirFila? fila)
    {
        if (fila is null)
        {
            return;
        }

        var grupo = Grupos.FirstOrDefault(g => g.Renglones.Contains(fila));
        if (grupo is null)
        {
            return;
        }

        fila.PropertyChanged -= Fila_PropertyChanged;
        grupo.Renglones.Remove(fila);
        if (grupo.Renglones.Count == 0)
        {
            Grupos.Remove(grupo);
        }

        grupo.NotificarSubtotal();
        NotificarTotales();
    }

    /// <summary>
    /// Los renglones confirmados, en el orden en que se muestran. Solo tiene sentido si <see cref="PuedeConfirmar"/>.
    /// </summary>
    public IReadOnlyList<RenglonAAgregarDto> ObtenerRenglones()
    {
        return Grupos
            .SelectMany(g => g.Renglones)
            .Select(r => new RenglonAAgregarDto { IdCatalogoProveedor = r.IdCatalogoProveedor, Cantidad = r.Cantidad })
            .ToList();
    }

    private void Fila_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(RenglonPedirFila.Cantidad) || sender is not RenglonPedirFila fila)
        {
            return;
        }

        Grupos.FirstOrDefault(g => g.Renglones.Contains(fila))?.NotificarSubtotal();
        NotificarTotales();
    }

    private void NotificarTotales()
    {
        OnPropertyChanged(nameof(CantidadRenglones));
        OnPropertyChanged(nameof(TotalEstimado));
        OnPropertyChanged(nameof(PuedeConfirmar));
    }

    private static string DescribirOmitido(RenglonOmitidoDto omitido)
    {
        var item = omitido.CodigoProveedor is null
            ? $"Ítem {omitido.IdCatalogoProveedor}"
            : $"{omitido.CodigoProveedor} · {omitido.Descripcion}";

        return $"{item}: {EnumATextoConverter.ATexto(omitido.Motivo)}";
    }
}
