using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Persistence;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Pasos de persistencia que comparten los servicios que escriben los diccionarios del motor de catalogación (Track 6.6,
/// Fase 4b), para que todos traduzcan una carrera y apliquen la cascada de M-35 de la misma forma.
/// </summary>
internal static class PersistenciaDiccionarios
{
    /// <summary>
    /// Guarda y traduce la violación de un índice único a un mensaje de negocio. La comprobación previa del servicio
    /// da el mensaje preciso; esto cubre la carrera entre esa comprobación y el guardado, cuando otro usuario registró
    /// lo mismo al mismo tiempo y el índice único de la base dejó pasar solo a uno.
    /// </summary>
    public static async Task GuardarAsync(IUnitOfWork unitOfWork, string mensajeConflicto, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (RestriccionDeUnicidadException ex)
        {
            throw new DomainException(mensajeConflicto, ex);
        }
    }

    /// <summary>
    /// Da de baja, a través de cada raíz <see cref="Proveedor"/> (Ley 2), las equivalencias que traducen a la
    /// categoría o la marca que se está dando de baja (M-35). No guarda: el llamador guarda todo junto con la baja
    /// del destino, en una sola transacción.
    /// </summary>
    public static async Task QuitarEquivalenciasHaciaAsync(
        IRepository<Proveedor> proveedores,
        IClasificacionQueryService consultas,
        TipoEquivalenciaEnum tipo,
        int idDestino,
        CancellationToken cancellationToken)
    {
        var idsProveedores = await consultas.ListarProveedoresConEquivalenciasHaciaAsync(tipo, idDestino, cancellationToken);

        // Un proveedor por consulta: son pocos los que traducen a un mismo destino, y cargar la raíz completa con sus
        // equivalencias es lo que permite que el agregado aplique la baja.
        foreach (var idProveedor in idsProveedores)
        {
            var proveedor = await proveedores.GetByIdWithIncludesAsync(idProveedor, cancellationToken, p => p.Equivalencias);
            if (proveedor is not null && proveedor.QuitarEquivalenciasHacia(tipo, idDestino) > 0)
            {
                await proveedores.UpdateAsync(proveedor, cancellationToken);
            }
        }
    }
}
