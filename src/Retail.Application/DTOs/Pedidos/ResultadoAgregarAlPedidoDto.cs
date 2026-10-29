namespace Retail.Application.DTOs.Pedidos;

/// <summary>
/// Resultado de agregar un lote a los pedidos (RF-29): el resumen de cada borrador que recibió renglones y los ítems
/// omitidos con su motivo. Si todos se omitieron, <see cref="Pedidos"/> está vacío y no se guardó nada.
/// </summary>
public record class ResultadoAgregarAlPedidoDto
{
    public IReadOnlyList<PedidoResumenDto> Pedidos { get; init; } = Array.Empty<PedidoResumenDto>();

    public IReadOnlyList<RenglonOmitidoDto> Omitidos { get; init; } = Array.Empty<RenglonOmitidoDto>();
}
