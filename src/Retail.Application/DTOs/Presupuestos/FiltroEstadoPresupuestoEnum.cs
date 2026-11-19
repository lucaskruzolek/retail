namespace Retail.Application.DTOs.Presupuestos;

/// <summary>
/// Estados que se pueden elegir en el filtro de la columna Estado de la grilla de presupuestos. <see cref="Vigentes"/> y
/// <see cref="Vencidos"/> son estados de <b>consulta</b>, no del dominio: son los presupuestos <c>Pendiente</c> con
/// <c>FechaVencimiento &gt;= hoy</c> y <c>FechaVencimiento &lt; hoy</c> respectivamente (PS-08). Sin ningún estado elegido
/// no se filtra.
/// </summary>
public enum FiltroEstadoPresupuestoEnum
{
    Vigentes = 1,
    Vencidos = 2,
    Convertidos = 3,
    Anulados = 4,
    Reemplazados = 5
}
