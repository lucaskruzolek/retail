using FluentValidation;
using Retail.Application.DTOs.Listas;
using Retail.Application.DTOs.Ventas;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Common;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Implementación de los casos de uso de las listas de artículos (RF-30). Toda modificación pasa por la raíz
/// <see cref="ListaArticulos"/> (Ley 2); las lecturas se delegan en <see cref="IListaQueryService"/> (Ley 8).
/// La venta de una lista no está acá: el POS cobra una venta común a precio de catálogo (L-08).
/// </summary>
public class ListaService : IListaService
{
    private readonly IRepository<ListaArticulos> _listaRepository;
    private readonly IRepository<Articulo> _articuloRepository;
    private readonly IListaQueryService _consultas;
    private readonly IArticuloQueryService _articuloQueryService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITicketPrinterService _ticketPrinterService;
    private readonly IExportadorListaPdf _exportadorPdf;
    private readonly TimeProvider _reloj;
    private readonly IValidator<CrearListaDto> _crearValidator;
    private readonly IValidator<ActualizarListaDto> _actualizarValidator;
    private readonly IValidator<ConsultaListasDto> _consultaValidator;

    public ListaService(
        IRepository<ListaArticulos> listaRepository,
        IRepository<Articulo> articuloRepository,
        IListaQueryService consultas,
        IArticuloQueryService articuloQueryService,
        IUnitOfWork unitOfWork,
        ITicketPrinterService ticketPrinterService,
        IExportadorListaPdf exportadorPdf,
        TimeProvider reloj,
        IValidator<CrearListaDto> crearValidator,
        IValidator<ActualizarListaDto> actualizarValidator,
        IValidator<ConsultaListasDto> consultaValidator)
    {
        _listaRepository = listaRepository ?? throw new ArgumentNullException(nameof(listaRepository));
        _articuloRepository = articuloRepository ?? throw new ArgumentNullException(nameof(articuloRepository));
        _consultas = consultas ?? throw new ArgumentNullException(nameof(consultas));
        _articuloQueryService = articuloQueryService ?? throw new ArgumentNullException(nameof(articuloQueryService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _ticketPrinterService = ticketPrinterService ?? throw new ArgumentNullException(nameof(ticketPrinterService));
        _exportadorPdf = exportadorPdf ?? throw new ArgumentNullException(nameof(exportadorPdf));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _crearValidator = crearValidator ?? throw new ArgumentNullException(nameof(crearValidator));
        _actualizarValidator = actualizarValidator ?? throw new ArgumentNullException(nameof(actualizarValidator));
        _consultaValidator = consultaValidator ?? throw new ArgumentNullException(nameof(consultaValidator));
    }

    public async Task<ListaDto> CrearAsync(CrearListaDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await _crearValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var nombreNormalizado = ReglasTexto.NormalizarEspacios(dto.Nombre);
        int idLista;

        try
        {
            await ExigirArticulosActivosAsync(dto.Renglones.Select(r => r.IdArticulo), cancellationToken);

            var lista = ListaArticulos.Crear(
                dto.Nombre,
                dto.IdUsuario,
                dto.Renglones.Select(r => (r.IdArticulo, r.Cantidad)));

            await _listaRepository.AddAsync(lista, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            idLista = lista.Id;
        }
        catch (RestriccionDeUnicidadException ex)
        {
            // El índice único filtrado por nombre (L-10) es la garantía final, también frente a dos terminales.
            throw new DomainException($"Ya existe una lista llamada «{nombreNormalizado}».", ex);
        }
        finally
        {
            // El contexto vive lo que la pantalla: lo leído con tracking no debe quedar retenido (D-15).
            _unitOfWork.DescartarCambios();
        }

        return await LeerGuardadaAsync(idLista, cancellationToken);
    }

    public async Task<ListaDto> ActualizarAsync(int idLista, ActualizarListaDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await _actualizarValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var nombreNormalizado = ReglasTexto.NormalizarEspacios(dto.Nombre);

        try
        {
            var lista = await _listaRepository.GetByIdWithIncludesAsync(idLista, cancellationToken, l => l.Renglones);
            if (lista is null || lista.IsDeleted)
            {
                throw new DomainException($"No se encontró la lista {idLista}.");
            }

            if (lista.FechaModificacion != dto.FechaModificacionLeida)
            {
                throw CrearExcepcionDeModificacionConcurrente(lista.Nombre);
            }

            // Un artículo que ya figuraba en la lista se conserva aunque haya sido dado de baja desde entonces (por
            // ejemplo, para poder renombrar); solo los artículos que se agregan ahora deben estar activos.
            var idsPresentes = lista.Renglones.Select(r => r.IdArticulo).ToHashSet();
            await ExigirArticulosActivosAsync(
                dto.Renglones.Select(r => r.IdArticulo).Where(id => !idsPresentes.Contains(id)),
                cancellationToken);

            lista.Renombrar(dto.Nombre);
            lista.ReemplazarRenglones(dto.Renglones.Select(r => (r.IdArticulo, r.Cantidad)));

            // El row_version vive en la lista: UpdateAsync la marca como modificada para que SaveChanges lo verifique.
            await _listaRepository.UpdateAsync(lista, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (RestriccionDeUnicidadException ex)
        {
            throw new DomainException($"Ya existe una lista llamada «{nombreNormalizado}».", ex);
        }
        catch (ConflictoDeConcurrenciaException ex)
        {
            throw CrearExcepcionDeModificacionConcurrente(nombreNormalizado, ex);
        }
        finally
        {
            _unitOfWork.DescartarCambios();
        }

        return await LeerGuardadaAsync(idLista, cancellationToken);
    }

    public async Task EliminarAsync(int idLista, CancellationToken cancellationToken = default)
    {
        try
        {
            var lista = await _listaRepository.GetByIdAsync(idLista, cancellationToken)
                ?? throw new DomainException($"No se encontró la lista {idLista}.");

            // Baja lógica (Ley 3): el repositorio la marca con MarkAsDeleted y la deja modificada para el SaveChanges.
            await _listaRepository.DeleteAsync(lista, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _unitOfWork.DescartarCambios();
        }
    }

    public async Task<ListasPaginadosDto> ListarAsync(ConsultaListasDto consulta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        await _consultaValidator.ValidateAndThrowAsync(consulta, cancellationToken);

        return await _consultas.ListarAsync(consulta, cancellationToken);
    }

    public Task<ListaDto?> ObtenerAsync(int idLista, CancellationToken cancellationToken = default)
    {
        return _consultas.ObtenerAsync(idLista, cancellationToken);
    }

    public async Task<ListaParaVentaDto> ObtenerParaVentaAsync(int idLista, CancellationToken cancellationToken = default)
    {
        // Lectura sin tracking: recuperar no modifica nada; el origen se guarda recién al cobrar (L-09).
        var lista = await ObtenerExistenteAsync(idLista, cancellationToken);

        var noDisponibles = lista.Renglones
            .Where(r => !r.EstaDisponible)
            .Select(r => r.Descripcion)
            .ToList();

        var idsDisponibles = lista.Renglones
            .Where(r => r.EstaDisponible)
            .Select(r => r.IdArticulo)
            .Distinct()
            .ToList();

        var disponibilidades = await _articuloQueryService.ObtenerDisponibilidadAsync(idsDisponibles, cancellationToken);
        var disponibilidadPorId = disponibilidades.ToDictionary(d => d.IdArticulo);

        var renglones = new List<RenglonListaParaVentaDto>();
        var faltantes = new List<FaltanteStockDto>();

        foreach (var renglon in lista.Renglones.Where(r => r.EstaDisponible))
        {
            // Un artículo dado de baja entre la lectura de la lista y la de disponibilidad también queda fuera.
            if (!disponibilidadPorId.TryGetValue(renglon.IdArticulo, out var disponible))
            {
                noDisponibles.Add(renglon.Descripcion);
                continue;
            }

            // El precio es siempre el de catálogo de hoy (L-08): la lista no guarda precios.
            renglones.Add(new RenglonListaParaVentaDto
            {
                IdArticulo = renglon.IdArticulo,
                CodigoBarras = disponible.CodigoBarras,
                Descripcion = renglon.Descripcion,
                Cantidad = renglon.Cantidad,
                PrecioActual = disponible.PrecioVenta,
                EsServicio = disponible.EsServicio,
                StockActual = disponible.StockActual
            });

            if (!disponible.EsServicio && renglon.Cantidad > disponible.StockActual)
            {
                faltantes.Add(FaltantesStock.Crear(disponible, renglon.Cantidad));
            }
        }

        return new ListaParaVentaDto
        {
            IdLista = lista.IdLista,
            Nombre = lista.Nombre,
            Renglones = renglones,
            Faltantes = faltantes,
            ArticulosNoDisponibles = noDisponibles
        };
    }

    public async Task ImprimirAsync(int idLista, CancellationToken cancellationToken = default)
    {
        var lista = await ObtenerExistenteAsync(idLista, cancellationToken);

        await _ticketPrinterService.ImprimirListaAsync(lista, ObtenerHoy(), cancellationToken);
    }

    public async Task ExportarPdfAsync(int idLista, Stream destino, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destino);

        var lista = await ObtenerExistenteAsync(idLista, cancellationToken);

        await _exportadorPdf.ExportarAsync(lista, ObtenerHoy(), destino, cancellationToken);
    }

    /// <summary>
    /// Exige que cada artículo exista y esté activo. Un artículo dado de baja (o inexistente) no se puede agregar a una
    /// lista.
    /// </summary>
    private async Task ExigirArticulosActivosAsync(IEnumerable<int> idsArticulos, CancellationToken cancellationToken)
    {
        var ids = idsArticulos.Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var activos = await _articuloRepository.FindAsync(
            a => ids.Contains(a.Id),
            includeDeleted: false,
            cancellationToken);
        var idsActivos = activos.Select(a => a.Id).ToHashSet();

        foreach (var id in ids)
        {
            if (!idsActivos.Contains(id))
            {
                throw new DomainException($"El artículo ID {id} ya no está disponible: fue dado de baja.");
            }
        }
    }

    private async Task<ListaDto> LeerGuardadaAsync(int idLista, CancellationToken cancellationToken)
    {
        return await _consultas.ObtenerAsync(idLista, cancellationToken)
            ?? throw new InvalidOperationException($"La lista {idLista} recién guardada no se pudo leer.");
    }

    private async Task<ListaDto> ObtenerExistenteAsync(int idLista, CancellationToken cancellationToken)
    {
        return await _consultas.ObtenerAsync(idLista, cancellationToken)
            ?? throw new DomainException($"No se encontró la lista {idLista}.");
    }

    private static DomainException CrearExcepcionDeModificacionConcurrente(string nombre, Exception? causa = null)
    {
        var mensaje = $"Otro usuario modificó la lista «{nombre}» mientras la editaba. Vuelva a abrirla para ver los cambios.";

        return causa is null ? new DomainException(mensaje) : new DomainException(mensaje, causa);
    }

    /// <summary>
    /// Fecha calendario local de hoy: la de la leyenda "Precios al dd/mm/aaaa". Sale del <see cref="TimeProvider"/> y
    /// no de <c>DateTime.Now</c>, así es testeable.
    /// </summary>
    private DateOnly ObtenerHoy()
    {
        return DateOnly.FromDateTime(_reloj.GetLocalNow().DateTime);
    }
}
