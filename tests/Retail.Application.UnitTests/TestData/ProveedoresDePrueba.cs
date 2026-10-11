using Retail.Domain.Entities;

namespace Retail.Application.UnitTests.TestData;

/// <summary>
/// Desde que <see cref="Proveedor"/> solo se crea con <see cref="Proveedor.Crear"/>, el Id que en producción asigna
/// la base se fija acá para los tests que simulan un proveedor ya persistido.
/// </summary>
internal static class ProveedoresDePrueba
{
    public static Proveedor ConId(int id, Proveedor proveedor)
    {
        proveedor.Id = id;
        return proveedor;
    }
}
