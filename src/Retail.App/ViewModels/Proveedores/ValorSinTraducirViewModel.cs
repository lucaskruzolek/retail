using CommunityToolkit.Mvvm.ComponentModel;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;
using Retail.Domain.Enums;

namespace Retail.App.ViewModels.Proveedores;

/// <summary>
/// Fila del paso "Asociar categorías y marcas" (antes "Traducir valores del proveedor"): un valor de la clasificación del proveedor sin equivalencia (M-13)
/// con el destino que elige la persona (una categoría o una marca del comercio) o su descarte.
/// </summary>
public partial class ValorSinTraducirViewModel : ObservableObject
{
    public ValorSinTraducirDto Valor { get; }

    public bool EsCategoria => Valor.Tipo == TipoEquivalenciaEnum.Categoria;

    public string TipoTexto => EsCategoria ? "Categoría" : "Marca";

    public string Texto => EsCategoria && !string.IsNullOrWhiteSpace(Valor.TextoRubro)
        ? $"{Valor.TextoRubro} / {Valor.Texto}"
        : Valor.Texto;

    public int CantidadDeItems => Valor.CantidadDeItems;

    [ObservableProperty]
    private CategoriaDto? _categoriaDestino;

    [ObservableProperty]
    private MarcaDto? _marcaDestino;

    /// <summary>
    /// Ya se tradujo o se ignoró en este paso: la fila deja de ofrecer sus acciones.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Pendiente))]
    private bool _resuelto;

    [ObservableProperty]
    private string? _resultado;

    public bool Pendiente => !Resuelto;

    public ValorSinTraducirViewModel(ValorSinTraducirDto valor)
    {
        Valor = valor ?? throw new ArgumentNullException(nameof(valor));
    }

    /// <summary>
    /// Identificador del destino elegido según el tipo del valor; nulo si todavía no se eligió.
    /// </summary>
    public int? IdDestinoElegido => EsCategoria ? CategoriaDestino?.IdCategoria : MarcaDestino?.IdMarca;
}
