using FluentValidation;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Common;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Casos de uso de la tabla de términos (Track 6.6, M-16, M-27) y de sus pares (Fase 5, M-48). Lo usan la pestaña de
/// términos, la decisión en bloque de la incorporación, que registra el término nuevo con la categoría que elige el
/// usuario, y las propuestas de pares (M-49). Los pares son hijos de <see cref="TerminoClasificacion"/> (Ley 2): se
/// modifican solo a través de esa raíz.
/// </summary>
public class TerminoClasificacionService : ITerminoClasificacionService
{
    /// <summary>
    /// Artículos con el par necesarios para proponerlo (P-5): con uno solo, la propuesta sería ruido.
    /// </summary>
    public const int SoporteMinimo = 2;

    /// <summary>
    /// Fracción mínima de esos artículos que debe tener la categoría propuesta (P-5, M-5). Al ser mayor que 0,5, dos
    /// categorías no pueden empatar.
    /// </summary>
    public const double PurezaMinima = 0.8;

    private const int EjemplosPorPropuesta = 3;

    private readonly IRepository<TerminoClasificacion> _repository;
    private readonly IRepository<Categoria> _categorias;
    private readonly IRepository<Rubro> _rubros;
    private readonly IRepository<Usuario> _usuarios;
    private readonly IClasificacionQueryService _consultas;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<RegistrarTerminoDto> _registrarValidator;
    private readonly IValidator<RegistrarTerminoConCategoriaNuevaDto> _registrarConCategoriaNuevaValidator;
    private readonly IValidator<AsignarParesDto> _asignarParesValidator;
    private readonly IValidator<QuitarParDto> _quitarParValidator;

    public TerminoClasificacionService(
        IRepository<TerminoClasificacion> repository,
        IRepository<Categoria> categorias,
        IRepository<Rubro> rubros,
        IRepository<Usuario> usuarios,
        IClasificacionQueryService consultas,
        IUnitOfWork unitOfWork,
        IValidator<RegistrarTerminoDto> registrarValidator,
        IValidator<RegistrarTerminoConCategoriaNuevaDto> registrarConCategoriaNuevaValidator,
        IValidator<AsignarParesDto> asignarParesValidator,
        IValidator<QuitarParDto> quitarParValidator)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _categorias = categorias ?? throw new ArgumentNullException(nameof(categorias));
        _rubros = rubros ?? throw new ArgumentNullException(nameof(rubros));
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
        _consultas = consultas ?? throw new ArgumentNullException(nameof(consultas));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _registrarValidator = registrarValidator ?? throw new ArgumentNullException(nameof(registrarValidator));
        _registrarConCategoriaNuevaValidator = registrarConCategoriaNuevaValidator
            ?? throw new ArgumentNullException(nameof(registrarConCategoriaNuevaValidator));
        _asignarParesValidator = asignarParesValidator ?? throw new ArgumentNullException(nameof(asignarParesValidator));
        _quitarParValidator = quitarParValidator ?? throw new ArgumentNullException(nameof(quitarParValidator));
    }

    public async Task<IReadOnlyList<TerminoDto>> ListarAsync(
        string? filtro,
        int? idCategoria,
        bool soloAmbiguos,
        CancellationToken cancellationToken = default)
    {
        return await _consultas.ListarTerminosAsync(filtro, idCategoria, soloAmbiguos, cancellationToken);
    }

    public async Task RegistrarAsync(int idActor, RegistrarTerminoDto dto, CancellationToken cancellationToken = default)
    {
        // Primero la autorización: quien no tiene permiso no recibe detalles de validación.
        await AutorizacionClasificacion.ExigirAsync(_usuarios, idActor, cancellationToken);
        await _registrarValidator.ValidateAndThrowAsync(dto, cancellationToken);

        // D-23: la categoría de destino debe existir y estar activa; da un mensaje claro en lugar de un error de la FK.
        if (dto.IdCategoria.HasValue)
        {
            var categoria = await _categorias.GetByIdAsync(dto.IdCategoria.Value, includeDeleted: false, cancellationToken);
            if (categoria is null)
            {
                throw new DomainException($"No se encontró la categoría con ID {dto.IdCategoria.Value}.");
            }
        }

        string termino = await RegistrarEnMemoriaAsync(dto.Termino, dto.IdCategoria, cancellationToken);
        await PersistenciaDiccionarios.GuardarAsync(_unitOfWork, MensajeTerminoSimultaneo(termino), cancellationToken);
    }

    public async Task<CategoriaDto> RegistrarConCategoriaNuevaAsync(
        int idActor,
        RegistrarTerminoConCategoriaNuevaDto dto,
        CancellationToken cancellationToken = default)
    {
        await AutorizacionClasificacion.ExigirAsync(_usuarios, idActor, cancellationToken);
        await _registrarConCategoriaNuevaValidator.ValidateAndThrowAsync(dto, cancellationToken);

        // T-1: el término guarda el Id de su categoría (un agregado referencia a otro por Id, no por objeto) y la
        // categoría nueva recién lo tiene después de guardarse. Son dos guardados, así que la atomicidad la da una
        // transacción explícita: si el término falla, la categoría tampoco queda.
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var (categoria, rubro) = await AltaCategoria.PrepararAsync(
                _categorias,
                _rubros,
                dto.NombreCategoria,
                dto.IdRubro,
                cancellationToken);
            await PersistenciaDiccionarios.GuardarAsync(
                _unitOfWork,
                $"Otro usuario creó la categoría '{categoria.NombreCategoria}' al mismo tiempo. Actualice la lista y " +
                "vuelva a intentarlo.",
                cancellationToken);

            string termino = await RegistrarEnMemoriaAsync(dto.Termino, categoria.Id, cancellationToken);
            await PersistenciaDiccionarios.GuardarAsync(_unitOfWork, MensajeTerminoSimultaneo(termino), cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new CategoriaDto
            {
                IdCategoria = categoria.Id,
                NombreCategoria = categoria.NombreCategoria,
                IdRubro = rubro.Id,
                NombreRubro = rubro.NombreRubro,
                CantidadEnUso = 0
            };
        }
        catch
        {
            // Sin cancelación: deshacer no debe depender de que el usuario no haya cancelado. El contexto vive
            // mientras dura la pantalla (D-15): la categoría ya guardada en la transacción sigue en el Change
            // Tracker aunque la base la haya revertido, y sin descartarla se guardaría con la próxima operación.
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            _unitOfWork.DescartarCambios();
            throw;
        }
    }

    /// <summary>
    /// Crea el término o reasigna el existente, sin guardar; devuelve su forma de comparación. Unicidad entre
    /// agregados: cada término es su propia raíz, así que "un término en una sola categoría" se resuelve buscándolo
    /// antes de crear. Si existe, se reasigna (como la equivalencia, M-32).
    /// </summary>
    private async Task<string> RegistrarEnMemoriaAsync(string texto, int? idCategoria, CancellationToken cancellationToken)
    {
        string termino = ReglasClasificacion.ExigirTermino(texto);
        var existentes = await _repository.FindAsync(x => x.Termino == termino, includeDeleted: false, cancellationToken);
        var entidad = existentes.Count > 0 ? existentes[0] : null;

        if (entidad is null)
        {
            entidad = idCategoria.HasValue
                ? TerminoClasificacion.CrearConCategoria(termino, idCategoria.Value)
                : TerminoClasificacion.CrearAmbiguo(termino);
            await _repository.AddAsync(entidad, cancellationToken);
        }
        else
        {
            if (idCategoria.HasValue)
            {
                entidad.AsignarCategoria(idCategoria.Value);
            }
            else
            {
                entidad.MarcarAmbiguo();
            }

            await _repository.UpdateAsync(entidad, cancellationToken);
        }

        return termino;
    }

    private static string MensajeTerminoSimultaneo(string termino)
    {
        return $"Otro usuario registró el término '{termino}' al mismo tiempo. Actualice la lista y vuelva a intentarlo.";
    }

    public async Task BajaAsync(int idActor, int idTermino, CancellationToken cancellationToken = default)
    {
        await AutorizacionClasificacion.ExigirAsync(_usuarios, idActor, cancellationToken);

        var entidad = await _repository.GetByIdAsync(idTermino, includeDeleted: false, cancellationToken);
        if (entidad is null)
        {
            throw new DomainException($"No se encontró el término con ID {idTermino}.");
        }

        entidad.MarkAsDeleted();

        await _repository.UpdateAsync(entidad, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ParDto>> ListarParesAsync(
        int? idTermino,
        string? filtro,
        bool incluirIgnorados,
        CancellationToken cancellationToken = default)
    {
        return await _consultas.ListarParesAsync(idTermino, filtro, incluirIgnorados, cancellationToken);
    }

    public async Task<IReadOnlyList<ParPropuestoDto>> ListarParesPropuestosAsync(
        int? idTermino,
        CancellationToken cancellationToken = default)
    {
        var ambiguos = (await _consultas.ListarTerminosAsync(null, null, soloAmbiguos: true, cancellationToken))
            .Where(t => idTermino is null || t.IdTermino == idTermino)
            .ToDictionary(t => t.Termino, t => t.IdTermino, StringComparer.Ordinal);
        if (ambiguos.Count == 0)
        {
            return [];
        }

        // Lo descartado no vuelve a proponerse (P-4): se excluyen los pares con categoría y también los ignorados.
        var enElDiccionario = (await _consultas.ListarParesAsync(idTermino, null, incluirIgnorados: true, cancellationToken))
            .Select(p => (p.Termino, p.SegundaPalabra))
            .ToHashSet();

        var articulos = await _consultas.ListarArticulosConTerminosAsync(ambiguos.Keys, cancellationToken);

        // El conteo se hace acá y no en SQL: la segunda palabra es una regla del Dominio con paridad contra Python
        // (Ley 6). Es la excepción acotada a la Ley 8 de SISTEMA_DE_PERSISTENCIA.md: corre a pedido, nunca al sugerir.
        var porPar = new Dictionary<(string Termino, string SegundaPalabra), List<DescripcionClasificadaDto>>();
        foreach (var articulo in articulos)
        {
            // El pre-filtro de la consulta trae de más ("CASETE" contiene "SET"): decide la regla exacta.
            var termino = ReglasClasificacion.TerminoDeDescripcion(articulo.Descripcion);
            var segundaPalabra = ReglasClasificacion.SegundaPalabra(articulo.Descripcion);
            if (!ambiguos.ContainsKey(termino) || segundaPalabra.Length == 0 || enElDiccionario.Contains((termino, segundaPalabra)))
            {
                continue;
            }

            if (!porPar.TryGetValue((termino, segundaPalabra), out var grupo))
            {
                grupo = [];
                porPar[(termino, segundaPalabra)] = grupo;
            }

            grupo.Add(articulo);
        }

        return porPar
            .Select(par => Proponer(ambiguos[par.Key.Termino], par.Key.Termino, par.Key.SegundaPalabra, par.Value))
            .OfType<ParPropuestoDto>()
            .OrderByDescending(p => p.Total)
            .ThenBy(p => p.Termino, StringComparer.Ordinal)
            .ThenBy(p => p.SegundaPalabra, StringComparer.Ordinal)
            .ToList();
    }

    public Task AsignarParAsync(int idActor, AsignarParDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return AsignarParesAsync(idActor, new AsignarParesDto { Pares = [dto] }, cancellationToken);
    }

    public async Task AsignarParesAsync(int idActor, AsignarParesDto dto, CancellationToken cancellationToken = default)
    {
        await AutorizacionClasificacion.ExigirAsync(_usuarios, idActor, cancellationToken);
        await _asignarParesValidator.ValidateAndThrowAsync(dto, cancellationToken);

        // Todo lo que puede fallar se comprueba antes de modificar el primer término: si un par fallara a mitad de la
        // lista, los anteriores quedarían en el contexto y se guardarían con la próxima operación de la pantalla
        // (Ley 9 de persistencia). Con estas comprobaciones y el validador, AsignarPar ya no puede fallar.
        var terminos = await CargarTerminosAmbiguosAsync(dto.Pares.Select(p => p.IdTermino), cancellationToken);
        await ExigirCategoriasActivasAsync(
            dto.Pares.Where(p => p.IdCategoria.HasValue).Select(p => p.IdCategoria!.Value),
            cancellationToken);

        foreach (var par in dto.Pares)
        {
            terminos[par.IdTermino].AsignarPar(par.SegundaPalabra, par.IdCategoria);
        }

        foreach (var termino in terminos.Values)
        {
            await _repository.UpdateAsync(termino, cancellationToken);
        }

        await PersistenciaDiccionarios.GuardarAsync(
            _unitOfWork,
            "Otro usuario registró uno de estos pares al mismo tiempo. Actualice la lista y vuelva a intentarlo.",
            cancellationToken);
    }

    public async Task QuitarParAsync(int idActor, QuitarParDto dto, CancellationToken cancellationToken = default)
    {
        await AutorizacionClasificacion.ExigirAsync(_usuarios, idActor, cancellationToken);
        await _quitarParValidator.ValidateAndThrowAsync(dto, cancellationToken);

        // Los pares llegan con el término (AutoInclude, M-57).
        var termino = await _repository.GetByIdAsync(dto.IdTermino, includeDeleted: false, cancellationToken);
        if (termino is null)
        {
            throw new DomainException($"No se encontró el término con ID {dto.IdTermino}.");
        }

        termino.QuitarPar(dto.SegundaPalabra);

        await _repository.UpdateAsync(termino, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Criterio de P-5 sobre los artículos de un par: la categoría con más artículos (a igualdad, el menor Id, solo
    /// por determinismo) si alcanzan el soporte y la pureza mínimos; si no, ninguna propuesta.
    /// </summary>
    private static ParPropuestoDto? Proponer(
        int idTermino,
        string termino,
        string segundaPalabra,
        List<DescripcionClasificadaDto> articulos)
    {
        var mayoria = articulos
            .GroupBy(a => a.IdCategoria)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .First();

        if (articulos.Count < SoporteMinimo || (double)mayoria.Count() / articulos.Count < PurezaMinima)
        {
            return null;
        }

        return new ParPropuestoDto
        {
            IdTermino = idTermino,
            Termino = termino,
            SegundaPalabra = segundaPalabra,
            IdCategoria = mayoria.Key,
            NombreCategoria = mayoria.First().NombreCategoria,
            Coincidencias = mayoria.Count(),
            Total = articulos.Count,
            Ejemplos = mayoria.Take(EjemplosPorPropuesta).Select(a => a.Descripcion).ToList()
        };
    }

    // Una sola consulta para todos los términos del lote; los pares llegan con cada uno (AutoInclude, M-57).
    private async Task<Dictionary<int, TerminoClasificacion>> CargarTerminosAmbiguosAsync(
        IEnumerable<int> idsTerminos,
        CancellationToken cancellationToken)
    {
        var ids = idsTerminos.Distinct().ToList();
        var encontrados = await _repository.FindAsync(t => ids.Contains(t.Id), includeDeleted: false, cancellationToken);
        var terminos = encontrados.ToDictionary(t => t.Id);

        foreach (var id in ids)
        {
            if (!terminos.TryGetValue(id, out var termino))
            {
                throw new DomainException($"No se encontró el término con ID {id}.");
            }

            // P-3: el Dominio también lo rechaza, pero acá se rechaza antes de modificar nada.
            if (!termino.EsAmbiguo)
            {
                throw new DomainException(
                    $"El término '{termino.Termino}' ya sugiere una categoría: solo un término ambiguo puede tener pares.");
            }
        }

        return terminos;
    }

    // D-23: las categorías de destino deben existir y estar activas; da un mensaje claro en lugar de un error de la FK.
    private async Task ExigirCategoriasActivasAsync(IEnumerable<int> idsCategorias, CancellationToken cancellationToken)
    {
        var ids = idsCategorias.Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var activas = await _categorias.FindAsync(c => ids.Contains(c.Id), includeDeleted: false, cancellationToken);
        var idsActivos = activas.Select(c => c.Id).ToHashSet();
        foreach (var id in ids)
        {
            if (!idsActivos.Contains(id))
            {
                throw new DomainException($"No se encontró la categoría con ID {id}.");
            }
        }
    }
}
