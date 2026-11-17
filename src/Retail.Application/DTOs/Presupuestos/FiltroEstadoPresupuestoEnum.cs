namespace Retail.Application.DTOs.Presupuestos;

/// <summary>
/// Filtro de estado de la grilla de presupuestos. <see cref="Vigentes"/> y <see cref="Vencidos"/> son estados de
/// <b>consulta</b>, no del dominio: son los presupuestos <c>Pendiente</c> con <c>FechaVencimiento &gt;= hoy</c> y
/// <c>FechaVencimiento &lt; hoy</c> respectivamente (PS-08).
/// </summary>
public enum FiltroEstadoPresupuestoEnum
{
    Todos = 0,
    Vigentes = 1,
    Vencidos = 2,
    Convertidos = 3,
    Anulados = 4
}
