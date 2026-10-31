using FluentValidation;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Casos de uso de las equivalencias de un proveedor (Track 6.6, M-13). Las equivalencias son hijas de
/// <see cref="Proveedor"/> (Ley 2): se cargan y se modifican solo a través de esa raíz. Es un servicio aparte de
/// <c>ProveedorService</c> porque sigue la autorización de la clasificación (D-5) y no la del alta de proveedores.
/// </summary>
public class EquivalenciaClasificacionService : IEquivalenciaClasificacionService
{
    private readonly IRepository<Proveedor> _proveedores;
    private readonly IRepository<Categoria> _categorias;
    private readonly IRepository<Marca> _marcas;
    private readonly IRepository<Usuario> _usuarios;
    private readonly IClasificacionQueryService _consultas;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<AsignarEquivalenciaDto> _asignarValidator;
    private readonly IValidator<QuitarEquivalenciaDto> _quitarValidator;

    public EquivalenciaClasificacionService(
        IRepository<Proveedor> proveedores,
        IRepository<Categoria> categorias,
        IRepository<Marca> marcas,
        IRepository<Usuario> usuarios,
        IClasificacionQueryService consultas,
        IUnitOfWork unitOfWork,
        IValidator<AsignarEquivalenciaDto> asignarValidator,
        IValidator<QuitarEquivalenciaDto> quitarValidator)
    {
        _proveedores = proveedores ?? throw new ArgumentNullException(nameof(proveedores));
        _categorias = categorias ?? throw new ArgumentNullException(nameof(categorias));
        _marcas = marcas ?? throw new ArgumentNullException(nameof(marcas));
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
        _consultas = consultas ?? throw new ArgumentNullException(nameof(consultas));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _asignarValidator = asignarValidator ?? throw new ArgumentNullException(nameof(asignarValidator));
        _quitarValidator = quitarValidator ?? throw new ArgumentNullException(nameof(quitarValidator));
    }

    public async Task<IReadOnlyList<EquivalenciaDto>> ListarAsync(
        int idProveedor,
        TipoEquivalenciaEnum? tipo,
        CancellationToken cancellationToken = default)
    {
        return await _consultas.ListarEquivalenciasAsync(idProveedor, tipo, cancellationToken);
    }

    public async Task AsignarAsync(int idActor, AsignarEquivalenciaDto dto, CancellationToken cancellationToken = default)
    {
        await AutorizacionClasificacion.ExigirAsync(_usuarios, idActor, cancellationToken);
        await _asignarValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var proveedor = await ObtenerProveedorAsync(dto.IdProveedor, cancellationToken);

        if (dto.IdDestino.HasValue)
        {
            await ExigirDestinoActivoAsync(dto.Tipo, dto.IdDestino.Value, cancellationToken);
        }

        var equivalencia = dto.Tipo == TipoEquivalenciaEnum.Categoria
            ? proveedor.AsignarEquivalenciaCategoria(dto.TextoRubro, dto.Texto, dto.IdDestino)
            : proveedor.AsignarEquivalenciaMarca(dto.Texto, dto.IdDestino);

        await _proveedores.UpdateAsync(proveedor, cancellationToken);
        await PersistenciaDiccionarios.GuardarAsync(
            _unitOfWork,
            $"Otro usuario registró la equivalencia de '{equivalencia.Texto}' al mismo tiempo. Actualice la lista y " +
            "vuelva a intentarlo.",
            cancellationToken);
    }

    public async Task QuitarAsync(int idActor, QuitarEquivalenciaDto dto, CancellationToken cancellationToken = default)
    {
        await AutorizacionClasificacion.ExigirAsync(_usuarios, idActor, cancellationToken);
        await _quitarValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var proveedor = await ObtenerProveedorAsync(dto.IdProveedor, cancellationToken);
        proveedor.QuitarEquivalencia(dto.Tipo, dto.TextoRubro, dto.Texto);

        await _proveedores.UpdateAsync(proveedor, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // La raíz se carga con sus equivalencias: el agregado necesita verlas para decidir si crea o reasigna (M-32).
    private async Task<Proveedor> ObtenerProveedorAsync(int idProveedor, CancellationToken cancellationToken)
    {
        var proveedor = await _proveedores.GetByIdWithIncludesAsync(idProveedor, cancellationToken, p => p.Equivalencias);
        if (proveedor is null)
        {
            throw new DomainException($"No se encontró el proveedor con ID {idProveedor}.");
        }

        return proveedor;
    }

    // D-23: el destino debe existir y estar activo; da un mensaje claro en lugar de un error de la FK.
    private async Task ExigirDestinoActivoAsync(TipoEquivalenciaEnum tipo, int idDestino, CancellationToken cancellationToken)
    {
        if (tipo == TipoEquivalenciaEnum.Categoria)
        {
            var categoria = await _categorias.GetByIdAsync(idDestino, includeDeleted: false, cancellationToken);
            if (categoria is null)
            {
                throw new DomainException($"No se encontró la categoría con ID {idDestino}.");
            }
        }
        else
        {
            var marca = await _marcas.GetByIdAsync(idDestino, includeDeleted: false, cancellationToken);
            if (marca is null)
            {
                throw new DomainException($"No se encontró la marca con ID {idDestino}.");
            }
        }
    }
}
