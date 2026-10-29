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
/// <see cref="ObtenerRespuesta"/>.
/// </summary>
public partial class PedirArticulosViewModel : ObservableObject
{
    private bool _incorporarPrimeroSolicitado;

    /// <param name="vistaPrevia">Lo que devolvió la vista previa del lote.</param>
    /// <param name="avisosPrevios">
    /// Lo que la pantalla de origen ya excluyó antes de consultar (por ejemplo, un artículo sin proveedor): se muestra
    /// junto a los omitidos de la vista previa.
    /// </param>
    /// <param name="ofrecerIncorporar">
    /// Si la pantalla de origen sabe incorporar los ítems sin incorporar (el importador). En el segundo paso del
    /// recorrido se abre sin la oferta, para que el recorrido termine.
    /// </param>
    public PedirArticulosViewModel(
        VistaPreviaPedidoDto vistaPrevia,
        IReadOnlyList<string>? avisosPrevios = null,
        bool ofrecerIncorporar = false)
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

        Omitidos = (avisosPrevios ?? Array.Empty<string>())
            .Concat(vistaPrevia.Omitidos.Select(DescribirOmitido))
            .ToList();

        CantidadSinIncorporar = vistaPrevia.Omitidos.Count(o => o.Motivo == MotivoOmisionPedidoEnum.SinIncorporar);
        PuedeIncorporarPrimero = ofrecerIncorporar && CantidadSinIncorporar > 0;
    }

    public ObservableCollection<GrupoPedidoProveedor> Grupos { get; } = new();

    /// <summary>Un texto por ítem que no se puede pedir: qué ítem es y por qué.</summary>
    public IReadOnlyList<string> Omitidos { get; }

    public bool TieneOmitidos => Omitidos.Count > 0;

    /// <summary>Cuántos omitidos se podrían pedir si se incorporan antes (A-8).</summary>
    public int CantidadSinIncorporar { get; }

    /// <summary>Si el modal muestra "Incorporar primero": solo cuando se ofrece y hay ítems sin incorporar.</summary>
    public bool PuedeIncorporarPrimero { get; }

    public string TextoIncorporarPrimero => CantidadSinIncorporar == 1
        ? "Incorporar el ítem y volver"
        : $"Incorporar los {CantidadSinIncorporar} ítems y volver";

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
    /// Registra que el usuario eligió incorporar primero. Lo llama el diálogo antes de cerrarse; no hace nada si la
    /// oferta no corresponde.
    /// </summary>
    public void SolicitarIncorporacion()
    {
        _incorporarPrimeroSolicitado = PuedeIncorporarPrimero;
    }

    /// <summary>
    /// La respuesta del modal una vez cerrado con aceptación: incorporar primero, o los renglones confirmados en el
    /// orden en que se muestran. Si se cerró sin aceptar, quien lo abrió responde <see cref="RespuestaPedirArticulos.Cancelada"/>.
    /// </summary>
    public RespuestaPedirArticulos ObtenerRespuesta()
    {
        if (_incorporarPrimeroSolicitado)
        {
            return RespuestaPedirArticulos.IncorporarPrimero;
        }

        if (!PuedeConfirmar)
        {
            return RespuestaPedirArticulos.Cancelada;
        }

        var renglones = Grupos
            .SelectMany(g => g.Renglones)
            .Select(r => new RenglonAAgregarDto { IdCatalogoProveedor = r.IdCatalogoProveedor, Cantidad = r.Cantidad })
            .ToList();

        return RespuestaPedirArticulos.Confirmada(renglones);
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
