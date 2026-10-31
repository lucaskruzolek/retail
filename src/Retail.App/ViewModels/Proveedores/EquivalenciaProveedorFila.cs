using Retail.Application.DTOs.Clasificacion;
using Retail.Domain.Enums;

namespace Retail.App.ViewModels.Proveedores;

/// <summary>
/// Fila de la grilla de equivalencias de un proveedor: adapta el <see cref="EquivalenciaDto"/> a los textos que
/// muestra la pantalla (Track 6.6, M-13, M-31, M-32).
/// </summary>
public sealed class EquivalenciaProveedorFila
{
    public EquivalenciaProveedorFila(EquivalenciaDto equivalencia)
    {
        Equivalencia = equivalencia ?? throw new ArgumentNullException(nameof(equivalencia));
    }

    public EquivalenciaDto Equivalencia { get; }

    public string TipoTexto => Equivalencia.Tipo == TipoEquivalenciaEnum.Categoria ? "Categoría" : "Marca";

    /// <summary>Lo que escribe el proveedor: "Rubro › Categoría" o, si no hay rubro, solo el texto.</summary>
    public string TextoProveedor => string.IsNullOrWhiteSpace(Equivalencia.TextoRubro)
        ? Equivalencia.Texto
        : $"{Equivalencia.TextoRubro} › {Equivalencia.Texto}";

    /// <summary>Nombre del destino del comercio, o "Ignorada" cuando el valor se descarta a propósito (M-32).</summary>
    public string DestinoTexto => Equivalencia.EsIgnorada
        ? "Ignorada"
        : Equivalencia.NombreCategoria ?? Equivalencia.NombreMarca ?? string.Empty;
}
