using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Services;
using Retail.App.ViewModels.Proveedores;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

/// <summary>
/// Pruebas del modal de incorporación con sugerencias por ítem y los pasos de traducción de valores y términos nuevos
/// (Track 6.6, Fase 6, pasos 6.5 y 6.6).
/// </summary>
public class IncorporarArticulosModalSugerenciasTests : IDisposable
{
    private static readonly CategoriaDto Libreria = new() { IdCategoria = 1, NombreCategoria = "Librería" };
    private static readonly CategoriaDto Utiles = new() { IdCategoria = 2, NombreCategoria = "Útiles" };
    private static readonly MarcaDto Bic = new() { IdMarca = 10, NombreMarca = "Bic" };
    private static readonly MarcaDto Faber = new() { IdMarca = 11, NombreMarca = "Faber" };

    // Las selecciones de la grilla son opciones (el record compara por valor): las mismas que arma el modal.
    private static readonly OpcionDiccionario OpLibreria = new(Libreria.IdCategoria, Libreria.NombreCategoria);
    private static readonly OpcionDiccionario OpUtiles = new(Utiles.IdCategoria, Utiles.NombreCategoria);
    private static readonly OpcionDiccionario OpBic = new(Bic.IdMarca, Bic.NombreMarca);
    private static readonly OpcionDiccionario OpFaber = new(Faber.IdMarca, Faber.NombreMarca);

    private readonly IClasificacionService _clasificacion = Substitute.For<IClasificacionService>();
    private readonly ITerminoClasificacionService _terminos = Substitute.For<ITerminoClasificacionService>();
    private readonly IEquivalenciaClasificacionService _equivalencias = Substitute.For<IEquivalenciaClasificacionService>();
    private readonly IRubroService _rubros = Substitute.For<IRubroService>();
    private readonly ICurrentUserSession _session = Substitute.For<ICurrentUserSession>();
    private readonly IncorporarArticulosModalViewModel _sut;

    public IncorporarArticulosModalSugerenciasTests()
    {
        _session.IdUsuario.Returns(7);
        _session.EsGerente.Returns(true);

        _sut = new IncorporarArticulosModalViewModel(
            _clasificacion,
            _terminos,
            _equivalencias,
            _rubros,
            _session,
            NullLogger<IncorporarArticulosModalViewModel>.Instance);
    }

    public void Dispose()
    {
        _sut.Dispose();
        GC.SuppressFinalize(this);
    }

    private static CatalogoProveedorDto Catalogo(int id, string descripcion)
    {
        return new CatalogoProveedorDto
        {
            Id = id,
            IdProveedor = 3,
            CodigoProveedor = $"COD-{id}",
            DescripcionProveedor = descripcion,
            CostoReposicion = 1000m
        };
    }

    private static SugerenciaItemDto Sugerencia(int idCatalogo, SugerenciaDto? categoria, SugerenciaDto? marca)
    {
        return new SugerenciaItemDto
        {
            IdCatalogo = idCatalogo,
            Descripcion = $"Ítem {idCatalogo}",
            Termino = "TERMINO",
            Categoria = categoria,
            Marca = marca
        };
    }

    private static SugerenciaDto DeCategoria(CategoriaDto categoria, MotivoSugerenciaEnum motivo = MotivoSugerenciaEnum.Termino)
    {
        return new SugerenciaDto { Id = categoria.IdCategoria, Nombre = categoria.NombreCategoria, Motivo = motivo };
    }

    private static SugerenciaDto DeMarca(MarcaDto marca, MotivoSugerenciaEnum motivo = MotivoSugerenciaEnum.MarcaEnLaDescripcion)
    {
        return new SugerenciaDto { Id = marca.IdMarca, Nombre = marca.NombreMarca, Motivo = motivo };
    }

    private static SugerenciasClasificacionDto Respuesta(
        IReadOnlyList<SugerenciaItemDto>? items = null,
        IReadOnlyList<BloqueTerminoNuevoDto>? bloques = null,
        IReadOnlyList<ValorSinTraducirDto>? valores = null)
    {
        return new SugerenciasClasificacionDto
        {
            Items = items ?? Array.Empty<SugerenciaItemDto>(),
            Bloques = bloques ?? Array.Empty<BloqueTerminoNuevoDto>(),
            ValoresSinTraducir = valores ?? Array.Empty<ValorSinTraducirDto>()
        };
    }

    private static BloqueTerminoNuevoDto UnBloque(string termino = "PELOTERO")
    {
        return new BloqueTerminoNuevoDto
        {
            Termino = termino,
            IdsCatalogo = new[] { 1, 2 },
            Ejemplos = new[] { "PELOTERO INFLABLE", "PELOTERO GRANDE" }
        };
    }

    private static ValorSinTraducirDto UnValor(TipoEquivalenciaEnum tipo = TipoEquivalenciaEnum.Categoria)
    {
        return new ValorSinTraducirDto
        {
            Tipo = tipo,
            TextoRubro = tipo == TipoEquivalenciaEnum.Categoria ? "LIBRERIA" : null,
            Texto = "ESCOLAR",
            CantidadDeItems = 4
        };
    }

    private void Abrir(SugerenciasClasificacionDto respuesta)
    {
        _clasificacion.SugerirAsync(Arg.Any<SugerirClasificacionDto>(), Arg.Any<CancellationToken>()).Returns(respuesta);
        _sut.Inicializar(
            new[] { Libreria, Utiles },
            new[] { Bic, Faber },
            new[] { Catalogo(1, "Bolígrafo Bic"), Catalogo(2, "Cuaderno") });
    }

    // ------------------------------------------------------------------ //
    // 6.5: sugerencias por ítem                                          //
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task IniciarAsync_ConSugerencias_PrecargaCategoriaYMarcaDeCadaItem()
    {
        // Arrange
        Abrir(Respuesta(new[]
        {
            Sugerencia(1, DeCategoria(Libreria), DeMarca(Bic)),
            Sugerencia(2, DeCategoria(Utiles), null)
        }));

        // Act
        await _sut.IniciarAsync();

        // Assert
        _sut.Items[0].CategoriaSeleccionada.Should().Be(OpLibreria);
        _sut.Items[0].MarcaSeleccionada.Should().Be(OpBic);
        _sut.Items[0].Estado.Should().Be(EstadoSugerenciaItemEnum.Sugerida);
        _sut.Items[0].MotivoCategoriaTexto.Should().Be("Término del diccionario");
        _sut.Items[1].CategoriaSeleccionada.Should().Be(OpUtiles);
        _sut.Items[1].MarcaSeleccionada.Should().Be(OpcionDiccionario.SinClasificar);
        _sut.CargandoSugerencias.Should().BeFalse();
        _sut.PasoActual.Should().Be(PasoIncorporacionEnum.RevisarArticulos);
        await _clasificacion.Received(1).SugerirAsync(
            Arg.Is<SugerirClasificacionDto>(d => d.IdProveedor == 3 && d.IdsCatalogo.Count == 2 && d.IdsCatalogo[0] == 1 && d.IdsCatalogo[1] == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IniciarAsync_ItemSinSugerencia_QuedaSinSugerenciaYSinCategoria()
    {
        // Arrange
        Abrir(Respuesta(new[] { Sugerencia(1, null, null), Sugerencia(2, DeCategoria(Utiles), null) }));

        // Act
        await _sut.IniciarAsync();

        // Assert
        _sut.Items[0].Estado.Should().Be(EstadoSugerenciaItemEnum.SinSugerencia);
        _sut.ArticulosSinCategoria.Should().Be(1);
        _sut.AvisoSinCategoria.Should().Be("1 artículo se incorporará sin categoría.");
        _sut.HayArticulosSinCategoria.Should().BeTrue();
    }

    [Fact]
    public async Task Estado_AlCorregirLaCategoria_PasaACorregidaYAlVolverASugerirNoSePisa()
    {
        // Arrange
        Abrir(Respuesta(new[] { Sugerencia(1, DeCategoria(Libreria), null), Sugerencia(2, null, null) }));
        await _sut.IniciarAsync();

        // Act
        _sut.Items[0].CategoriaSeleccionada = OpUtiles;

        // Assert
        _sut.Items[0].Estado.Should().Be(EstadoSugerenciaItemEnum.Corregida);
        _sut.Items[0].EstadoTexto.Should().Be("Corregida");
        _sut.ResumenClasificacion.Should().Be("0 sugeridas · 1 corregidas");

        // Act: una nueva respuesta del motor no pisa la corrección de la persona
        _sut.Items[0].AplicarSugerencia(Sugerencia(1, DeCategoria(Libreria), null));
        _sut.Items[0].CategoriaSeleccionada.Should().Be(OpUtiles);
    }

    [Fact]
    public async Task Estado_ConSinMarcaYCategoriaSugerida_EsSinMarcaYExcluyeLaMarcaElegida()
    {
        // Arrange
        Abrir(Respuesta(new[] { Sugerencia(1, DeCategoria(Libreria), null), Sugerencia(2, null, null) }));
        await _sut.IniciarAsync();
        _sut.Items[0].MarcaSeleccionada = OpBic;

        // Act
        _sut.Items[0].MarcaSeleccionada = OpcionDiccionario.SinMarca;

        // Assert
        _sut.Items[0].SinMarca.Should().BeTrue();
        _sut.Items[0].Estado.Should().Be(EstadoSugerenciaItemEnum.SinMarca);

        // Act: elegir una marca después anula "Sin marca"
        _sut.Items[0].MarcaSeleccionada = OpFaber;

        // Assert
        _sut.Items[0].SinMarca.Should().BeFalse();
        _sut.Items[0].Estado.Should().Be(EstadoSugerenciaItemEnum.Corregida);
    }

    [Fact]
    public void Inicializar_ArmaLasOpcionesConSinClasificarPrimeroYSinMarcaSoloEnMarcas()
    {
        // Act
        Abrir(Respuesta());

        // Assert: la categoría vacía no tiene un estado confirmado, así que no hay "Sin categoría" (M-70, C1)
        _sut.OpcionesCategoria.Select(o => o.Nombre).Should().Equal("Sin clasificar", "Librería", "Útiles");
        _sut.OpcionesMarca.Select(o => o.Nombre).Should().Equal("Sin clasificar", "Sin marca", "Bic", "Faber");
        _sut.OpcionesMarca.Take(2).Should().OnlyContain(o => o.Id == null);
    }

    [Fact]
    public void Inicializar_CadaFilaEmpiezaSinClasificarYSinSugerencia()
    {
        // Act
        Abrir(Respuesta());

        // Assert
        _sut.Items.Should().OnlyContain(i =>
            i.CategoriaSeleccionada == OpcionDiccionario.SinClasificar
            && i.MarcaSeleccionada == OpcionDiccionario.SinClasificar
            && !i.SinMarca
            && i.Estado == EstadoSugerenciaItemEnum.SinSugerencia);
    }

    [Fact]
    public void Inicializar_ReabiertoOCategoriaNueva_MantieneLasOpcionesAlDiaSinDuplicar()
    {
        // Arrange
        Abrir(Respuesta());
        Abrir(Respuesta());

        // Act: una categoría creada durante el recorrido se suma a las opciones
        _sut.CategoriasDisponibles.Add(new CategoriaDto { IdCategoria = 99, NombreCategoria = "Pelotas" });

        // Assert
        _sut.OpcionesCategoria.Select(o => o.Nombre).Should().Equal("Sin clasificar", "Librería", "Útiles", "Pelotas");
        _sut.OpcionesMarca.Should().HaveCount(4);
    }

    [Fact]
    public async Task ObtenerDto_ConOpcionSinMarca_ConfirmaSinMarcaYNoEnviaIdMarca()
    {
        // Arrange
        Abrir(Respuesta(new[] { Sugerencia(1, DeCategoria(Libreria), DeMarca(Bic)), Sugerencia(2, null, null) }));
        await _sut.IniciarAsync();

        // Act
        _sut.Items[0].MarcaSeleccionada = OpcionDiccionario.SinMarca;
        var dto = _sut.ObtenerDto();

        // Assert
        dto.Items[0].SinMarcaConfirmada.Should().BeTrue();
        dto.Items[0].IdMarca.Should().BeNull();
        dto.Items[0].IdMarcaSugerida.Should().Be(Bic.IdMarca);
    }

    [Fact]
    public async Task ObtenerDto_ConMarcaSinElegir_NoConfirmaSinMarca()
    {
        // Arrange
        Abrir(Respuesta(new[] { Sugerencia(1, DeCategoria(Libreria), null), Sugerencia(2, null, null) }));
        await _sut.IniciarAsync();

        // Act
        var dto = _sut.ObtenerDto();

        // Assert
        _sut.Items[1].MarcaSeleccionada.Should().Be(OpcionDiccionario.SinClasificar);
        dto.Items.Should().OnlyContain(i => !i.SinMarcaConfirmada && i.IdMarca == null);
    }

    [Fact]
    public async Task ObtenerDto_ConSinClasificarEnCategoria_EnviaIdCategoriaNuloYCuentaComoSinCategoria()
    {
        // Arrange
        Abrir(Respuesta(new[] { Sugerencia(1, DeCategoria(Libreria), null), Sugerencia(2, DeCategoria(Utiles), null) }));
        await _sut.IniciarAsync();

        // Act
        _sut.Items[0].CategoriaSeleccionada = _sut.OpcionesCategoria[0];
        var dto = _sut.ObtenerDto();

        // Assert
        _sut.Items[0].SinCategoria.Should().BeTrue();
        _sut.Items[0].Estado.Should().Be(EstadoSugerenciaItemEnum.Corregida);
        _sut.ArticulosSinCategoria.Should().Be(1);
        _sut.AvisoSinCategoria.Should().Be("1 artículo se incorporará sin categoría.");
        dto.Items[0].IdCategoria.Should().BeNull();
        dto.Items[0].IdCategoriaSugerida.Should().Be(Libreria.IdCategoria);
        dto.Items[1].IdCategoria.Should().Be(Utiles.IdCategoria);
    }

    [Fact]
    public async Task ObtenerDto_ConSugerencias_LlevaConfirmadasYSugeridasPorItem()
    {
        // Arrange
        Abrir(Respuesta(new[]
        {
            Sugerencia(1, DeCategoria(Libreria), DeMarca(Bic)),
            Sugerencia(2, DeCategoria(Utiles), null)
        }));
        await _sut.IniciarAsync();
        _sut.Items[0].CategoriaSeleccionada = OpUtiles; // corrige la categoría
        _sut.Items[1].MarcaSeleccionada = OpcionDiccionario.SinMarca; // confirma que no tiene marca
        _sut.Items[1].PorcentajeGanancia = 25m;

        // Act
        var dto = _sut.ObtenerDto();

        // Assert
        dto.Items.Should().HaveCount(2);
        dto.Items[0].Should().BeEquivalentTo(new ItemIncorporacionArticuloDto
        {
            IdCatalogo = 1,
            PorcentajeGanancia = 40m,
            IdCategoria = Utiles.IdCategoria,
            IdCategoriaSugerida = Libreria.IdCategoria,
            IdMarca = Bic.IdMarca,
            IdMarcaSugerida = Bic.IdMarca
        });
        dto.Items[1].Should().BeEquivalentTo(new ItemIncorporacionArticuloDto
        {
            IdCatalogo = 2,
            PorcentajeGanancia = 25m,
            IdCategoria = Utiles.IdCategoria,
            IdCategoriaSugerida = Utiles.IdCategoria,
            IdMarca = null,
            SinMarcaConfirmada = true,
            IdMarcaSugerida = null
        });
    }

    [Fact]
    public async Task ObtenerDto_ConInterruptorApagado_AplicaCategoriaYMarcaDelLoteSinSugerencias()
    {
        // Arrange
        Abrir(Respuesta(new[] { Sugerencia(1, DeCategoria(Libreria), DeMarca(Bic)), Sugerencia(2, null, null) }));
        await _sut.IniciarAsync();
        _sut.SugerirClasificacion = false;
        _sut.CategoriaDelLote = OpUtiles;
        _sut.MarcaDelLote = OpFaber;

        // Act
        var dto = _sut.ObtenerDto();

        // Assert: se ignora lo sugerido y lo elegido por ítem, como antes de la Fase 6
        dto.Items.Should().OnlyContain(i =>
            i.IdCategoria == 2 && i.IdMarca == 11 && i.IdCategoriaSugerida == null && i.IdMarcaSugerida == null && !i.SinMarcaConfirmada);
        _sut.MostrarGrillaClasica.Should().BeTrue();
        _sut.MostrarClasificacionDelLote.Should().BeTrue();
    }

    [Fact]
    public void ObtenerDto_ConInterruptorApagadoYSinMarcaDelLote_ConfirmaSinMarcaEnTodos()
    {
        // Arrange
        Abrir(Respuesta());
        _sut.SugerirClasificacion = false;

        // Act
        _sut.MarcaDelLote = OpcionDiccionario.SinMarca;
        var dto = _sut.ObtenerDto();

        // Assert
        dto.Items.Should().OnlyContain(i => i.IdMarca == null && i.SinMarcaConfirmada);
    }

    [Fact]
    public void ObtenerDto_ConInterruptorApagadoYVueltaASinClasificar_NoAsignaNiConfirma()
    {
        // Arrange: se elige una categoría y una marca por error y se vuelve atrás (M-70)
        Abrir(Respuesta());
        _sut.SugerirClasificacion = false;
        _sut.CategoriaDelLote = OpUtiles;
        _sut.MarcaDelLote = OpFaber;

        // Act
        _sut.CategoriaDelLote = OpcionDiccionario.SinClasificar;
        _sut.MarcaDelLote = OpcionDiccionario.SinClasificar;
        var dto = _sut.ObtenerDto();

        // Assert
        dto.Items.Should().OnlyContain(i => i.IdCategoria == null && i.IdMarca == null && !i.SinMarcaConfirmada);
        _sut.ArticulosSinCategoria.Should().Be(2);
    }

    [Fact]
    public async Task IniciarAsync_ConInterruptorApagadoAlAbrir_NoPideSugerencias()
    {
        // Arrange
        Abrir(Respuesta());
        _sut.SugerirClasificacion = false;

        // Act
        await _sut.IniciarAsync();

        // Assert
        await _clasificacion.DidNotReceiveWithAnyArgs().SugerirAsync(default!, default);
    }

    [Fact]
    public async Task SugerirClasificacion_ActivadoDespuesDeAbrirApagado_PideLasSugerencias()
    {
        // Arrange
        Abrir(Respuesta(new[] { Sugerencia(1, DeCategoria(Libreria), null), Sugerencia(2, null, null) }));
        _sut.SugerirClasificacion = false;
        await _sut.IniciarAsync();

        // Act
        _sut.SugerirClasificacion = true;
        await EsperarAsync(() => _sut.Items[0].CategoriaSeleccionada != OpcionDiccionario.SinClasificar);

        // Assert
        _sut.Items[0].CategoriaSeleccionada.Should().Be(OpLibreria);
    }

    [Fact]
    public async Task IniciarAsync_SiElMotorFalla_InformaElErrorYPermiteClasificarAMano()
    {
        // Arrange
        Abrir(Respuesta());
        _clasificacion.SugerirAsync(Arg.Any<SugerirClasificacionDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("falla"));

        // Act
        await _sut.IniciarAsync();

        // Assert
        _sut.TieneError.Should().BeTrue();
        _sut.CargandoSugerencias.Should().BeFalse();
        _sut.PasoActual.Should().Be(PasoIncorporacionEnum.RevisarArticulos);
        _sut.MostrarGrillaConSugerencias.Should().BeTrue();
    }

    [Fact]
    public async Task RestablecerSugerencias_ConItemsCorregidos_VuelveALaSugerenciaEnTodos()
    {
        // Arrange
        Abrir(Respuesta(new[] { Sugerencia(1, DeCategoria(Libreria), DeMarca(Bic)), Sugerencia(2, DeCategoria(Utiles), null) }));
        await _sut.IniciarAsync();
        _sut.Items[0].CategoriaSeleccionada = OpUtiles;
        _sut.Items[1].MarcaSeleccionada = OpcionDiccionario.SinMarca;

        // Act
        _sut.RestablecerSugerenciasCommand.Execute(null);

        // Assert
        _sut.Items[0].CategoriaSeleccionada.Should().Be(OpLibreria);
        _sut.Items[0].Estado.Should().Be(EstadoSugerenciaItemEnum.Sugerida);
        _sut.Items[1].SinMarca.Should().BeFalse();
        _sut.Items[1].MarcaSeleccionada.Should().Be(OpcionDiccionario.SinClasificar);
        _sut.Items[1].Estado.Should().Be(EstadoSugerenciaItemEnum.Sugerida);
    }

    [Fact]
    public async Task VolverALaSugerenciaCommand_SoloSePuedeEjecutarEnFilasCorregidas()
    {
        // Arrange
        Abrir(Respuesta(new[] { Sugerencia(1, DeCategoria(Libreria), null), Sugerencia(2, DeCategoria(Utiles), null) }));
        await _sut.IniciarAsync();
        var fila = _sut.Items[0];

        // Assert: recién sugerida no hay nada que deshacer
        fila.PuedeVolverALaSugerencia.Should().BeFalse();
        fila.VolverALaSugerenciaCommand.CanExecute(null).Should().BeFalse();

        // Act: la persona corrige
        fila.CategoriaSeleccionada = OpUtiles;

        // Assert
        fila.PuedeVolverALaSugerencia.Should().BeTrue();
        fila.VolverALaSugerenciaCommand.CanExecute(null).Should().BeTrue();

        // Act
        fila.VolverALaSugerenciaCommand.Execute(null);

        // Assert: restaura solo esa fila y el comando vuelve a quedar deshabilitado
        fila.CategoriaSeleccionada.Should().Be(OpLibreria);
        fila.Estado.Should().Be(EstadoSugerenciaItemEnum.Sugerida);
        fila.VolverALaSugerenciaCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task VolverALaSugerenciaCommand_SoloRestauraLaFilaIndicada()
    {
        // Arrange
        Abrir(Respuesta(new[] { Sugerencia(1, DeCategoria(Libreria), null), Sugerencia(2, DeCategoria(Utiles), null) }));
        await _sut.IniciarAsync();
        _sut.Items[0].CategoriaSeleccionada = OpUtiles;
        _sut.Items[1].CategoriaSeleccionada = OpLibreria;

        // Act
        _sut.Items[0].VolverALaSugerenciaCommand.Execute(null);

        // Assert
        _sut.Items[0].CategoriaSeleccionada.Should().Be(OpLibreria);
        _sut.Items[1].CategoriaSeleccionada.Should().Be(OpLibreria); // sigue corregida
    }

    // ------------------------------------------------------------------ //
    // 6.6: pasos                                                         //
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task IniciarAsync_SinValoresNiBloques_SaltaLosPasosYVaALaRevision()
    {
        // Arrange
        Abrir(Respuesta(new[] { Sugerencia(1, DeCategoria(Libreria), null), Sugerencia(2, null, null) }));

        // Act
        await _sut.IniciarAsync();

        // Assert
        _sut.PasoActual.Should().Be(PasoIncorporacionEnum.RevisarArticulos);
        _sut.MostrarEncabezadoDePasos.Should().BeFalse();
    }

    [Fact]
    public async Task IniciarAsync_SoloConBloques_SaltaElPasoDeValores()
    {
        // Arrange
        Abrir(Respuesta(bloques: new[] { UnBloque() }));

        // Act
        await _sut.IniciarAsync();

        // Assert
        _sut.PasoActual.Should().Be(PasoIncorporacionEnum.TerminosNuevos);
        _sut.TienePasoTraducirValores.Should().BeFalse();
        _sut.MostrarEncabezadoDePasos.Should().BeTrue();
        _sut.PasoTerminosNuevos.Bloques.Should().ContainSingle(b => b.Termino == "PELOTERO" && b.CantidadTexto == "2 productos");
    }

    [Fact]
    public async Task Pasos_ConValoresYBloques_NumeraLosTresPasosYMarcaElActual()
    {
        // Arrange
        Abrir(Respuesta(bloques: new[] { UnBloque() }, valores: new[] { UnValor() }));

        // Act
        await _sut.IniciarAsync();

        // Assert
        _sut.Pasos.Should().Equal(
            new PasoDelRecorrido(1, "Asociar categorías y marcas", EsActual: true, EsCompletado: false, EsUltimo: false),
            new PasoDelRecorrido(2, "Términos nuevos", EsActual: false, EsCompletado: false, EsUltimo: false),
            new PasoDelRecorrido(3, "Revisar artículos", EsActual: false, EsCompletado: false, EsUltimo: true));
        _sut.TextoSiguiente.Should().Be("Siguiente: términos nuevos");
    }

    [Fact]
    public async Task Pasos_AlAvanzarDesdeValoresSinBloques_MarcaElPrimeroCompletadoYNombraLaRevision()
    {
        // Arrange
        Abrir(Respuesta(valores: new[] { UnValor() }));
        await _sut.IniciarAsync();
        _sut.TextoSiguiente.Should().Be("Siguiente: revisar artículos");

        // Act
        await _sut.SiguienteCommand.ExecuteAsync(null);

        // Assert
        _sut.Pasos.Should().Equal(
            new PasoDelRecorrido(1, "Asociar categorías y marcas", EsActual: false, EsCompletado: true, EsUltimo: false),
            new PasoDelRecorrido(2, "Revisar artículos", EsActual: true, EsCompletado: false, EsUltimo: true));
    }

    [Fact]
    public void CodigosTexto_SinCodigoDeBarras_NoMuestraElSeparador()
    {
        // Arrange
        var sinBarras = new ItemIncorporarArticuloViewModel(1, "789", null, "Cuaderno", 100m, 40m, [], []);
        var conBarras = new ItemIncorporarArticuloViewModel(2, "789", "7791234567011", "Cuaderno", 100m, 40m, [], []);

        // Act y Assert
        sinBarras.CodigosTexto.Should().Be("789");
        conBarras.CodigosTexto.Should().Be("789 · 7791234567011");
    }

    [Fact]
    public async Task SiguienteAsync_DesdeValoresConBloques_VuelveASugerirYPasaAlPasoDeTerminos()
    {
        // Arrange
        Abrir(Respuesta(bloques: new[] { UnBloque() }, valores: new[] { UnValor() }));
        await _sut.IniciarAsync();
        _sut.PasoActual.Should().Be(PasoIncorporacionEnum.TraducirValores);

        // Act
        await _sut.SiguienteCommand.ExecuteAsync(null);

        // Assert
        _sut.PasoActual.Should().Be(PasoIncorporacionEnum.TerminosNuevos);
        await _clasificacion.ReceivedWithAnyArgs(2).SugerirAsync(default!, default);
    }

    [Fact]
    public async Task SiguienteAsync_DesdeTerminos_VuelveASugerirYPasaALaRevision()
    {
        // Arrange
        Abrir(Respuesta(bloques: new[] { UnBloque() }));
        await _sut.IniciarAsync();

        // Act
        await _sut.SiguienteCommand.ExecuteAsync(null);

        // Assert
        _sut.PasoActual.Should().Be(PasoIncorporacionEnum.RevisarArticulos);
        await _clasificacion.ReceivedWithAnyArgs(2).SugerirAsync(default!, default);
    }

    [Fact]
    public async Task SiguienteAsync_DespuesDeDecidirUnBloque_LasSugerenciasNuevasSePrecargan()
    {
        // Arrange
        _clasificacion.SugerirAsync(Arg.Any<SugerirClasificacionDto>(), Arg.Any<CancellationToken>()).Returns(
            Respuesta(new[] { Sugerencia(1, null, null), Sugerencia(2, null, null) }, bloques: new[] { UnBloque() }),
            Respuesta(new[] { Sugerencia(1, DeCategoria(Utiles), null), Sugerencia(2, DeCategoria(Utiles), null) }));
        _sut.Inicializar(new[] { Libreria, Utiles }, new[] { Bic }, new[] { Catalogo(1, "A"), Catalogo(2, "B") });
        await _sut.IniciarAsync();
        _sut.Items[0].CategoriaSeleccionada.Should().Be(OpcionDiccionario.SinClasificar);

        // Act
        await _sut.SiguienteCommand.ExecuteAsync(null);

        // Assert
        _sut.Items.Should().OnlyContain(i => i.CategoriaSeleccionada == OpUtiles && i.Estado == EstadoSugerenciaItemEnum.Sugerida);
        _sut.PasoTerminosNuevos.Bloques.Should().BeEmpty();
    }

    [Fact]
    public async Task TraducirCommand_ConDestinoElegido_AsignaLaEquivalenciaDelProveedor()
    {
        // Arrange
        Abrir(Respuesta(valores: new[] { UnValor() }));
        await _sut.IniciarAsync();
        var fila = _sut.PasoTraducirValores.Valores.Single();
        fila.CategoriaDestino = Utiles;

        // Act
        await _sut.PasoTraducirValores.TraducirCommand.ExecuteAsync(fila);

        // Assert
        await _equivalencias.Received(1).AsignarAsync(
            7,
            Arg.Is<AsignarEquivalenciaDto>(d =>
                d.IdProveedor == 3 && d.Tipo == TipoEquivalenciaEnum.Categoria && d.TextoRubro == "LIBRERIA" &&
                d.Texto == "ESCOLAR" && d.IdDestino == Utiles.IdCategoria),
            Arg.Any<CancellationToken>());
        fila.Resuelto.Should().BeTrue();
        fila.Resultado.Should().Be("Traducido a Útiles");
    }

    [Fact]
    public async Task IgnorarCommand_AsignaLaEquivalenciaSinDestino()
    {
        // Arrange
        Abrir(Respuesta(valores: new[] { UnValor(TipoEquivalenciaEnum.Marca) }));
        await _sut.IniciarAsync();
        var fila = _sut.PasoTraducirValores.Valores.Single();

        // Act
        await _sut.PasoTraducirValores.IgnorarCommand.ExecuteAsync(fila);

        // Assert
        await _equivalencias.Received(1).AsignarAsync(
            7,
            Arg.Is<AsignarEquivalenciaDto>(d => d.Tipo == TipoEquivalenciaEnum.Marca && d.IdDestino == null),
            Arg.Any<CancellationToken>());
        fila.Resultado.Should().Be("Ignorado");
    }

    [Fact]
    public async Task TraducirCommand_SinDestinoElegido_PideElegirYNoLlamaAlServicio()
    {
        // Arrange
        Abrir(Respuesta(valores: new[] { UnValor() }));
        await _sut.IniciarAsync();

        // Act
        await _sut.PasoTraducirValores.TraducirCommand.ExecuteAsync(_sut.PasoTraducirValores.Valores.Single());

        // Assert
        _sut.PasoTraducirValores.TieneError.Should().BeTrue();
        await _equivalencias.DidNotReceiveWithAnyArgs().AsignarAsync(default, default!, default);
    }

    [Fact]
    public async Task AsignarCategoriaExistenteCommand_RegistraElTerminoEnLaCategoria()
    {
        // Arrange
        Abrir(Respuesta(bloques: new[] { UnBloque() }));
        await _sut.IniciarAsync();
        var bloque = _sut.PasoTerminosNuevos.Bloques.Single();
        bloque.CategoriaSeleccionada = Utiles;

        // Act
        await _sut.PasoTerminosNuevos.AsignarCategoriaExistenteCommand.ExecuteAsync(bloque);

        // Assert
        await _terminos.Received(1).RegistrarAsync(
            7,
            Arg.Is<RegistrarTerminoDto>(d => d.Termino == "PELOTERO" && d.IdCategoria == Utiles.IdCategoria),
            Arg.Any<CancellationToken>());
        bloque.Resuelto.Should().BeTrue();
    }

    [Fact]
    public async Task CrearCategoriaYAsignarCommand_LlamaAlComandoAtomicoYSumaLaCategoriaALosCombos()
    {
        // Arrange
        var nueva = new CategoriaDto { IdCategoria = 99, NombreCategoria = "Pelotas", IdRubro = 5 };
        _rubros.ListarAsync(null, Arg.Any<CancellationToken>())
            .Returns(new[] { new RubroDto { IdRubro = 5, NombreRubro = "Juguetería" } });
        _terminos.RegistrarConCategoriaNuevaAsync(7, Arg.Any<RegistrarTerminoConCategoriaNuevaDto>(), Arg.Any<CancellationToken>())
            .Returns(nueva);
        Abrir(Respuesta(bloques: new[] { UnBloque() }));
        await _sut.IniciarAsync();
        var bloque = _sut.PasoTerminosNuevos.Bloques.Single();
        bloque.CrearCategoriaNueva = true;
        bloque.NombreCategoriaNueva = "  Pelotas ";
        bloque.RubroSeleccionado = _sut.PasoTerminosNuevos.Rubros.Single();

        // Act
        await _sut.PasoTerminosNuevos.CrearCategoriaYAsignarCommand.ExecuteAsync(bloque);

        // Assert
        await _terminos.Received(1).RegistrarConCategoriaNuevaAsync(
            7,
            Arg.Is<RegistrarTerminoConCategoriaNuevaDto>(d => d.Termino == "PELOTERO" && d.NombreCategoria == "Pelotas" && d.IdRubro == 5),
            Arg.Any<CancellationToken>());
        _sut.CategoriasDisponibles.Should().Contain(nueva);
        bloque.Resuelto.Should().BeTrue();
        await _terminos.DidNotReceiveWithAnyArgs().RegistrarAsync(default, default!, default);
    }

    [Fact]
    public async Task CrearCategoriaYAsignarCommand_SinNombre_NoLlamaAlServicio()
    {
        // Arrange
        Abrir(Respuesta(bloques: new[] { UnBloque() }));
        await _sut.IniciarAsync();
        var bloque = _sut.PasoTerminosNuevos.Bloques.Single();
        bloque.CrearCategoriaNueva = true;

        // Act
        await _sut.PasoTerminosNuevos.CrearCategoriaYAsignarCommand.ExecuteAsync(bloque);

        // Assert
        _sut.PasoTerminosNuevos.TieneError.Should().BeTrue();
        bloque.Resuelto.Should().BeFalse();
        await _terminos.DidNotReceiveWithAnyArgs().RegistrarConCategoriaNuevaAsync(default, default!, default);
    }

    [Fact]
    public async Task MarcarAmbiguoCommand_RegistraElTerminoSinCategoria()
    {
        // Arrange
        Abrir(Respuesta(bloques: new[] { UnBloque() }));
        await _sut.IniciarAsync();
        var bloque = _sut.PasoTerminosNuevos.Bloques.Single();

        // Act
        await _sut.PasoTerminosNuevos.MarcarAmbiguoCommand.ExecuteAsync(bloque);

        // Assert
        await _terminos.Received(1).RegistrarAsync(
            7,
            Arg.Is<RegistrarTerminoDto>(d => d.Termino == "PELOTERO" && d.IdCategoria == null),
            Arg.Any<CancellationToken>());
        bloque.Resultado.Should().Be("Marcado como ambiguo");
    }

    [Fact]
    public async Task AsignarCategoriaExistenteCommand_SiElServicioRechaza_MuestraElMensajeYNoResuelve()
    {
        // Arrange
        _terminos.RegistrarAsync(Arg.Any<int>(), Arg.Any<RegistrarTerminoDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Retail.Domain.Exceptions.DomainException("Otro usuario registró el término."));
        Abrir(Respuesta(bloques: new[] { UnBloque() }));
        await _sut.IniciarAsync();
        var bloque = _sut.PasoTerminosNuevos.Bloques.Single();
        bloque.CategoriaSeleccionada = Libreria;

        // Act
        await _sut.PasoTerminosNuevos.AsignarCategoriaExistenteCommand.ExecuteAsync(bloque);

        // Assert
        _sut.PasoTerminosNuevos.MensajeError.Should().Be("Otro usuario registró el término.");
        bloque.Resuelto.Should().BeFalse();
    }

    [Fact]
    public async Task Pasos_SinPermisoDeAdministrarClasificacion_SonDeSoloLecturaYNoEscriben()
    {
        // Arrange
        _session.EsGerente.Returns(false);
        _session.EsEncargado.Returns(false);
        Abrir(Respuesta(
            items: new[] { Sugerencia(1, DeCategoria(Libreria), null), Sugerencia(2, null, null) },
            bloques: new[] { UnBloque() },
            valores: new[] { UnValor() }));
        await _sut.IniciarAsync();
        var valor = _sut.PasoTraducirValores.Valores.Single();
        valor.CategoriaDestino = Utiles;
        var bloque = _sut.PasoTerminosNuevos.Bloques.Single();
        bloque.CategoriaSeleccionada = Utiles;

        // Act
        await _sut.PasoTraducirValores.TraducirCommand.ExecuteAsync(valor);
        await _sut.PasoTerminosNuevos.AsignarCategoriaExistenteCommand.ExecuteAsync(bloque);

        // Assert
        _sut.PuedeAdministrarClasificacion.Should().BeFalse();
        _sut.PasoTraducirValores.SoloLectura.Should().BeTrue();
        _sut.PasoTerminosNuevos.SoloLectura.Should().BeTrue();
        await _equivalencias.DidNotReceiveWithAnyArgs().AsignarAsync(default, default!, default);
        await _terminos.DidNotReceiveWithAnyArgs().RegistrarAsync(default, default!, default);
        await _rubros.DidNotReceiveWithAnyArgs().ListarAsync(default, default);

        // Act: la grilla funciona igual
        await _sut.SiguienteCommand.ExecuteAsync(null);
        _sut.Items[0].CategoriaSeleccionada = OpUtiles;

        // Assert
        _sut.ObtenerDto().Items[0].IdCategoria.Should().Be(Utiles.IdCategoria);
    }

    [Fact]
    public async Task PuedeAdministrarClasificacion_ConEncargado_EsVerdadero()
    {
        // Arrange
        _session.EsGerente.Returns(false);
        _session.EsEncargado.Returns(true);
        Abrir(Respuesta(valores: new[] { UnValor() }));

        // Act
        await _sut.IniciarAsync();

        // Assert
        _sut.PasoTraducirValores.PuedeAdministrar.Should().BeTrue();
    }

    // ------------------------------------------------------------------ //
    // M-70: navegación del recorrido y confirmación                      //
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task AnteriorCommand_DesdeLaRevision_VuelveAlPasoPrevioSinPedirSugerencias()
    {
        // Arrange
        Abrir(Respuesta(bloques: new[] { UnBloque() }, valores: new[] { UnValor() }));
        await _sut.IniciarAsync();
        await _sut.SiguienteCommand.ExecuteAsync(null);
        await _sut.SiguienteCommand.ExecuteAsync(null);
        _sut.PasoActual.Should().Be(PasoIncorporacionEnum.RevisarArticulos);

        // Act
        _sut.AnteriorCommand.Execute(null);

        // Assert
        _sut.PasoActual.Should().Be(PasoIncorporacionEnum.TerminosNuevos);
        await _clasificacion.ReceivedWithAnyArgs(3).SugerirAsync(default!, default);

        // Act: y de ahí al primero, donde ya no hay paso anterior
        _sut.AnteriorCommand.Execute(null);

        // Assert
        _sut.PasoActual.Should().Be(PasoIncorporacionEnum.TraducirValores);
        _sut.MostrarBotonAnterior.Should().BeFalse();
        _sut.AnteriorCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task AnteriorCommand_SinRecorrido_NoSePuedeEjecutar()
    {
        // Arrange
        Abrir(Respuesta(new[] { Sugerencia(1, DeCategoria(Libreria), null), Sugerencia(2, null, null) }));

        // Act
        await _sut.IniciarAsync();

        // Assert
        _sut.MostrarBotonAnterior.Should().BeFalse();
        _sut.AnteriorCommand.CanExecute(null).Should().BeFalse();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task AceptarCommand_EnUnPasoPrevioALaRevision_SoloSeHabilitaSinSugerencias(bool sugerir, bool esperado)
    {
        // Arrange
        Abrir(Respuesta(valores: new[] { UnValor() }));
        await _sut.IniciarAsync();
        _sut.PasoActual.Should().Be(PasoIncorporacionEnum.TraducirValores);

        // Act
        _sut.SugerirClasificacion = sugerir;

        // Assert
        _sut.AceptarCommand.CanExecute(null).Should().Be(esperado);
        _sut.MostrarBotonConfirmar.Should().Be(esperado);
        _sut.MostrarBotonSiguiente.Should().Be(!esperado);
    }

    [Fact]
    public async Task AceptarCommand_AlLlegarALaRevision_SeHabilita()
    {
        // Arrange
        Abrir(Respuesta(valores: new[] { UnValor() }));
        await _sut.IniciarAsync();
        _sut.AceptarCommand.CanExecute(null).Should().BeFalse();

        // Act
        await _sut.SiguienteCommand.ExecuteAsync(null);

        // Assert
        _sut.AceptarCommand.CanExecute(null).Should().BeTrue();
        _sut.MostrarBotonConfirmar.Should().BeTrue();
        _sut.MostrarBotonSiguiente.Should().BeFalse();
    }

    [Fact]
    public async Task AceptarCommand_SiElMotorFalla_SeHabilitaEnLaRevision()
    {
        // Arrange
        Abrir(Respuesta());
        _clasificacion.SugerirAsync(Arg.Any<SugerirClasificacionDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("falla"));

        // Act
        await _sut.IniciarAsync();

        // Assert
        _sut.AceptarCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task VolverALaSugerencia_SinSugerenciaDeMarca_VuelveASinClasificarYNoASinMarca()
    {
        // Arrange
        Abrir(Respuesta(new[] { Sugerencia(1, DeCategoria(Libreria), null), Sugerencia(2, null, null) }));
        await _sut.IniciarAsync();
        var fila = _sut.Items[0];
        fila.MarcaSeleccionada = OpBic;

        // Act
        fila.VolverALaSugerenciaCommand.Execute(null);

        // Assert
        fila.MarcaSeleccionada.Should().Be(OpcionDiccionario.SinClasificar);
        fila.SinMarca.Should().BeFalse();
        fila.Estado.Should().Be(EstadoSugerenciaItemEnum.Sugerida);
    }

    // ------------------------------------------------------------------ //
    // 6.8: aviso de reglas propuestas                                    //
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task ObtenerAvisoReglasPropuestasAsync_ConPropuestas_InformaLaCantidadYLosTerminos()
    {
        // Arrange
        var par = new ParPropuestoDto
        {
            IdTermino = 1,
            Termino = "SET",
            SegundaPalabra = "GEOMETRIA",
            IdCategoria = 1,
            NombreCategoria = "Geometría",
            Coincidencias = 8,
            Total = 8,
            Ejemplos = Array.Empty<string>()
        };
        _terminos.ListarParesPropuestosAsync(null, Arg.Any<CancellationToken>()).Returns(new[] { par, par, par });

        // Act
        var aviso = await _sut.ObtenerAvisoReglasPropuestasAsync();

        // Assert
        aviso.Should().Be("Hay 3 pares propuestos para revisar en Artículos → Clasificación → Términos: SET.");
    }

    [Fact]
    public async Task ObtenerAvisoReglasPropuestasAsync_SinPropuestas_NoAvisa()
    {
        // Arrange
        _terminos.ListarParesPropuestosAsync(null, Arg.Any<CancellationToken>()).Returns(Array.Empty<ParPropuestoDto>());

        // Act
        var aviso = await _sut.ObtenerAvisoReglasPropuestasAsync();

        // Assert
        aviso.Should().BeNull();
    }

    [Fact]
    public async Task ObtenerAvisoReglasPropuestasAsync_SiLaConsultaFalla_NoLanzaYNoAvisa()
    {
        // Arrange
        _terminos.ListarParesPropuestosAsync(null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("sin conexión"));

        // Act
        var aviso = await _sut.ObtenerAvisoReglasPropuestasAsync();

        // Assert
        aviso.Should().BeNull();
    }

    private static async Task EsperarAsync(Func<bool> condicion)
    {
        var limite = DateTime.UtcNow.AddSeconds(5);
        while (!condicion() && DateTime.UtcNow < limite)
        {
            await Task.Delay(10);
        }
    }
}
