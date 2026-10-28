using Retail.Domain.Enums;

namespace Retail.App.ViewModels.Proveedores;

/// <summary>Opción del combo de tipo de equivalencia (categoría o marca) del formulario del proveedor.</summary>
public sealed record OpcionTipoEquivalencia(TipoEquivalenciaEnum Tipo, string Nombre);
