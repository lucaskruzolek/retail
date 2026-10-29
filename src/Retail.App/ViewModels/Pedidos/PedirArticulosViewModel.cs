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
    // Destino de cada proveedor al que el combo puede mover un renglón, aunque hoy no tenga grupo (K1, K3).
    private readonly Dictionary<int, DestinoPedidoDto> _destinos;
    private readonly List<int> _ordenDestinos;
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

        _destinos = vistaPrevia.Destinos.ToDictionary(d => d.IdProveedor);
        _ordenDestinos = vistaPrevia.Destinos.Select(d => d.IdProveedor).ToList();

        // Los destinos vienen ordenados por proveedor; solo se muestran los que tienen renglones al abrir.
        foreach (var destino in vistaPrevia.Destinos)
        {
            var filas = vistaPrevia.Renglones
                .Where(r => r.IdProveedor == destino.IdProveedor)
                .Select(r => new RenglonPedirFila(r))
                .ToList();
            if (filas.Count == 0)
            {
                continue;
            }

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

        // Dos renglones que terminaron en la misma fuente se suman: el lote no admite un ítem repetido (K3). GroupBy
        // conserva el orden de la primera aparición.
        var renglones = Grupos
            .SelectMany(g => g.Renglones)
            .GroupBy(r => r.IdCatalogoProveedor)
            .Select(g => new RenglonAAgregarDto { IdCatalogoProveedor = g.Key, Cantidad = g.Sum(r => r.Cantidad) })
            .ToList();

        return RespuestaPedirArticulos.Confirmada(renglones);
    }

    private void Fila_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not RenglonPedirFila fila)
        {
            return;
        }

        if (e.PropertyName == nameof(RenglonPedirFila.Fuente))
        {
            MoverAlGrupoDeSuProveedor(fila);
        }
        else if (e.PropertyName == nameof(RenglonPedirFila.Cantidad))
        {
            Grupos.FirstOrDefault(g => g.Renglones.Contains(fila))?.NotificarSubtotal();
        }
        else
        {
            return;
        }

        NotificarTotales();
    }

    /// <summary>
    /// Lleva el renglón al grupo del proveedor de su nueva fuente (A-18, K3): lo crea si no existe, en el orden de los
    /// destinos, y quita el grupo que queda vacío. Si la fuente es del mismo proveedor, solo cambia el subtotal.
    /// </summary>
    private void MoverAlGrupoDeSuProveedor(RenglonPedirFila fila)
    {
        var origen = Grupos.FirstOrDefault(g => g.Renglones.Contains(fila));
        if (origen is null)
        {
            return;
        }

        if (origen.Destino.IdProveedor == fila.IdProveedor)
        {
            origen.NotificarSubtotal();
            return;
        }

        origen.Renglones.Remove(fila);
        if (origen.Renglones.Count == 0)
        {
            Grupos.Remove(origen);
        }
        else
        {
            origen.NotificarSubtotal();
        }

        var destino = Grupos.FirstOrDefault(g => g.Destino.IdProveedor == fila.IdProveedor);
        if (destino is null)
        {
            // La vista previa trae un destino por cada proveedor de las fuentes; el respaldo cubre una armada a mano.
            var datosDestino = _destinos.GetValueOrDefault(fila.IdProveedor)
                ?? new DestinoPedidoDto { IdProveedor = fila.IdProveedor, RazonSocialProveedor = fila.Fuente.RazonSocialProveedor };
            destino = new GrupoPedidoProveedor(datosDestino, []);
            Grupos.Insert(Grupos.Count(g => PosicionDelDestino(g) < PosicionDelDestino(destino)), destino);
        }

        destino.Renglones.Add(fila);
        destino.NotificarSubtotal();
    }

    /// <summary>
    /// Posición del grupo en el orden en que la vista previa entregó los destinos (por proveedor, con la intercalación
    /// de la base): un grupo nuevo se inserta donde estaría si hubiera existido al abrir.
    /// </summary>
    private int PosicionDelDestino(GrupoPedidoProveedor grupo)
    {
        var posicion = _ordenDestinos.IndexOf(grupo.Destino.IdProveedor);
        return posicion < 0 ? int.MaxValue : posicion;
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
