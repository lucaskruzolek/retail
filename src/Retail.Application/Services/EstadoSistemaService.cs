using Retail.Application.DTOs.Sistema;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Entities;
using Retail.Domain.Enums;

namespace Retail.Application.Services;

/// <summary>
/// Implementación de la verificación de arranque (RF-23): no modifica nada, solo informa si la base está lista.
/// </summary>
public class EstadoSistemaService : IEstadoSistemaService
{
    private readonly IEsquemaBaseDatos _esquema;
    private readonly IRepository<Usuario> _usuarioRepository;

    public EstadoSistemaService(IEsquemaBaseDatos esquema, IRepository<Usuario> usuarioRepository)
    {
        _esquema = esquema ?? throw new ArgumentNullException(nameof(esquema));
        _usuarioRepository = usuarioRepository ?? throw new ArgumentNullException(nameof(usuarioRepository));
    }

    public async Task<EstadoSistemaDto> ObtenerEstadoAsync(CancellationToken cancellationToken = default)
    {
        var esquema = await _esquema.ObtenerEstadoAsync(cancellationToken);

        if (!esquema.EstaAlDia)
        {
            // Con el esquema desactualizado, consultar usuarios podría fallar (tabla o columna inexistente).
            return new EstadoSistemaDto
            {
                Esquema = esquema,
                ExisteGerenteActivo = false,
                GerentesSinEmail = []
            };
        }

        var gerentes = await _usuarioRepository.FindAsync(
            u => u.IdRol == (int)RolUsuarioEnum.Gerente,
            includeDeleted: false,
            cancellationToken);

        return new EstadoSistemaDto
        {
            Esquema = esquema,
            ExisteGerenteActivo = gerentes.Count > 0,
            GerentesSinEmail = gerentes
                .Where(g => g.EmailRecuperacion is null)
                .Select(g => g.NombreUsuario)
                .OrderBy(nombre => nombre, StringComparer.Ordinal)
                .ToList()
        };
    }
}
