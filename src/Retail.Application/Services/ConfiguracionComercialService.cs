using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>Lee y cambia los parámetros comerciales globales (D-38).</summary>
public class ConfiguracionComercialService : IConfiguracionComercialService
{
    private readonly IRepository<ConfiguracionComercial> _configuraciones;
    private readonly IRepository<Usuario> _usuarios;
    private readonly IUnitOfWork _unitOfWork;

    public ConfiguracionComercialService(
        IRepository<ConfiguracionComercial> configuraciones,
        IRepository<Usuario> usuarios,
        IUnitOfWork unitOfWork)
    {
        _configuraciones = configuraciones ?? throw new ArgumentNullException(nameof(configuraciones));
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<RedondeoPrecioEnum> ObtenerRedondeoPrecioAsync(CancellationToken cancellationToken = default)
    {
        var configuracion = await _configuraciones.GetByIdAsync(ConfiguracionComercial.IdUnico, cancellationToken);

        return configuracion?.RedondeoPrecio ?? RedondeoPrecioEnum.EnteroMasCercano;
    }

    public async Task CambiarRedondeoPrecioAsync(int idActor, RedondeoPrecioEnum redondeo, CancellationToken cancellationToken = default)
    {
        var actor = idActor > 0
            ? await _usuarios.GetByIdAsync(idActor, includeDeleted: false, cancellationToken)
            : null;

        if (actor is null)
        {
            throw new OperacionNoAutorizadaException("La operación fue solicitada por un usuario inexistente o dado de baja.");
        }

        // Cambia los precios que calculará todo el sistema: es una decisión comercial del Gerente (D-38).
        if (!actor.EsGerente())
        {
            throw new OperacionNoAutorizadaException("Solo un Gerente puede cambiar el redondeo de los precios.");
        }

        var configuracion = await _configuraciones.GetByIdAsync(ConfiguracionComercial.IdUnico, cancellationToken);

        if (configuracion is null)
        {
            // La siembra la migración; si una base la perdió, se recrea en lugar de fallar.
            configuracion = new ConfiguracionComercial { Id = ConfiguracionComercial.IdUnico };
            configuracion.CambiarRedondeoPrecio(redondeo);
            await _configuraciones.AddAsync(configuracion, cancellationToken);
        }
        else
        {
            configuracion.CambiarRedondeoPrecio(redondeo);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
