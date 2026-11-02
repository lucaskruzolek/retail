using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Retail.App.Services;
using Retail.App.UnitTests.Helpers;
using Retail.App.ViewModels.Articulos;
using Retail.App.ViewModels.Pedidos;
using Retail.App.ViewModels.Proveedores;
using Retail.App.ViewModels.Ventas;
using Retail.App.Views.Dialogs;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.App.UnitTests.Snapshots;

/// <summary>
/// "Bucle de capturas" de los diálogos grandes con grilla (administrar clasificación, incorporar artículos, importar
/// planilla, pedir artículos, equivalencias del proveedor y los selectores de catálogo y de cliente). Cada prueba
/// renderiza el diálogo con datos de ejemplo de <see cref="DatosDeMuestraDialogosGrandes"/> a
/// <c>artifacts/snapshots/Light</c> y <c>artifacts/snapshots/Dark</c>, sin abrir el diálogo con ShowDialog. Se ejecuta a
/// pedido con <c>--filter "Category=Snapshot"</c>.
/// </summary>
[Trait("Category", "Snapshot")]
public class DialogosGrandesSnapshotTests
{
    private const int TamanoMinimoPng = 1000;

    // ------------------------------------------------------------------ AdministrarClasificacionDialog

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void AdministrarClasificacionDialog_SeccionRubros_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            // Rubros y categorías es la sección inicial: el Loaded del diálogo carga ambas grillas desde los servicios.
            using var viewModel = CrearAdministrarClasificacionViewModel();
            var dialogo = new AdministrarClasificacionDialog { DataContext = viewModel };

            return SnapshotRenderer.Guardar(
                dialogo,
                960,
                664,
                carpeta + "/AdministrarClasificacionDialog_Rubros",
                () => viewModel.Rubros.Count > 0 && viewModel.Categorias.Count > 0);
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void AdministrarClasificacionDialog_SeccionMarcas_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            using var viewModel = CrearAdministrarClasificacionViewModel();
            viewModel.MostrandoMarcas = true;
            var dialogo = new AdministrarClasificacionDialog { DataContext = viewModel };

            // Al cargar las marcas se selecciona la primera (Rivadavia) y se leen sus alias y prefijos EAN.
            return SnapshotRenderer.Guardar(
                dialogo,
                960,
                664,
                carpeta + "/AdministrarClasificacionDialog_Marcas",
                () => viewModel.Marcas.Count > 0
                    && viewModel.DetalleMarca.Alias.Count > 0
                    && viewModel.DetalleMarca.Prefijos.Count > 0);
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void AdministrarClasificacionDialog_SeccionTerminos_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            using var viewModel = CrearAdministrarClasificacionViewModel();

            // Se preselecciona el término ambiguo para que se lean sus pares apenas se cargue la sección.
            viewModel.Terminos.TerminoSeleccionado = DatosDeMuestraDialogosGrandes.TerminoAmbiguo();
            viewModel.MostrandoTerminos = true;
            var dialogo = new AdministrarClasificacionDialog { DataContext = viewModel };

            return SnapshotRenderer.Guardar(
                dialogo,
                960,
                664,
                carpeta + "/AdministrarClasificacionDialog_Terminos",
                () => viewModel.Terminos.Terminos.Count > 0 && viewModel.Terminos.Pares.Count > 0);
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void AdministrarClasificacionDialog_SeccionParesPropuestos_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            using var viewModel = CrearAdministrarClasificacionViewModel();

            // Pestaña "Pares propuestos" del término ambiguo, dentro de la sección Términos.
            viewModel.Terminos.TerminoSeleccionado = DatosDeMuestraDialogosGrandes.TerminoAmbiguo();
            viewModel.Terminos.MostrandoPropuestos = true;
            viewModel.MostrandoTerminos = true;
            var dialogo = new AdministrarClasificacionDialog { DataContext = viewModel };

            return SnapshotRenderer.Guardar(
                dialogo,
                960,
                664,
                carpeta + "/AdministrarClasificacionDialog_ParesPropuestos",
                () => viewModel.Terminos.Propuestos.Propuestas.Count > 0);
        });

        VerificarCaptura(ruta, error);
    }

    // ------------------------------------------------------------------ IncorporarArticulosModalDialog

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void IncorporarArticulosModalDialog_Paso1AsociarCategoriasYMarcas_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            using var viewModel = CrearIncorporarArticulosViewModel(PasoIncorporacionEnum.TraducirValores);
            var dialogo = new IncorporarArticulosModalDialog { DataContext = viewModel };

            return SnapshotRenderer.Guardar(dialogo, 1040, 704, carpeta + "/IncorporarArticulosModalDialog_Paso1");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void IncorporarArticulosModalDialog_Paso2TerminosNuevos_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            using var viewModel = CrearIncorporarArticulosViewModel(PasoIncorporacionEnum.TerminosNuevos);
            var dialogo = new IncorporarArticulosModalDialog { DataContext = viewModel };

            return SnapshotRenderer.Guardar(dialogo, 1040, 704, carpeta + "/IncorporarArticulosModalDialog_Paso2");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void IncorporarArticulosModalDialog_Paso3RevisarArticulos_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            using var viewModel = CrearIncorporarArticulosViewModel(PasoIncorporacionEnum.RevisarArticulos);
            var dialogo = new IncorporarArticulosModalDialog { DataContext = viewModel };

            return SnapshotRenderer.Guardar(dialogo, 1040, 704, carpeta + "/IncorporarArticulosModalDialog_Paso3");
        });

        VerificarCaptura(ruta, error);
    }

    // ------------------------------------------------------------------ ImportarPlanillaDialog

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void ImportarPlanillaDialog_ConArchivoElegido_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var viewModel = CrearImportarPlanillaViewModel();
            var dialogo = new ImportarPlanillaDialog { DataContext = viewModel };

            return SnapshotRenderer.Guardar(dialogo, 580, 684, carpeta + "/ImportarPlanillaDialog_ArchivoElegido");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void ImportarPlanillaDialog_Importando_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var viewModel = CrearImportarPlanillaViewModel();
            viewModel.IsBusy = true;
            viewModel.FilasLeidas = 8450;
            viewModel.MensajeEstado = "Leyendo planilla... (8.450 filas leídas)";
            var dialogo = new ImportarPlanillaDialog { DataContext = viewModel };

            return SnapshotRenderer.Guardar(dialogo, 580, 684, carpeta + "/ImportarPlanillaDialog_Importando");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void ImportarPlanillaDialog_ResultadoConObservaciones_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var viewModel = CrearImportarPlanillaViewModel();
            viewModel.UltimoResultado = DatosDeMuestraDialogosGrandes.ResultadoDeImportacionConObservaciones();
            viewModel.ImportacionCompletada = true;
            viewModel.MensajeEstado = "Importación finalizada con éxito en 3,4s.";
            viewModel.MensajeExito = "Se importaron 318 artículos nuevos y se actualizaron 962 precios en 3,4s. "
                + "Se omitieron 4 filas con error.";
            var dialogo = new ImportarPlanillaDialog { DataContext = viewModel };

            return SnapshotRenderer.Guardar(dialogo, 580, 684, carpeta + "/ImportarPlanillaDialog_Resultado");
        });

        VerificarCaptura(ruta, error);
    }

    // ------------------------------------------------------------------ PedirArticulosDialog

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void PedirArticulosDialog_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            // Tres proveedores con renglones, un ítem omitido por no estar incorporado y un aviso previo de la pantalla.
            var viewModel = new PedirArticulosViewModel(
                DatosDeMuestraDialogosGrandes.VistaPreviaDePedido(),
                ["Señalador fluorescente: no tiene proveedor vinculado"],
                ofrecerIncorporar: true);
            var dialogo = new PedirArticulosDialog(viewModel);

            return SnapshotRenderer.Guardar(dialogo, 580, 684, carpeta + "/PedirArticulosDialog");
        });

        VerificarCaptura(ruta, error);
    }

    // ------------------------------------------------------------------ EquivalenciasProveedorDialog

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void EquivalenciasProveedorDialog_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var equivalenciaService = Substitute.For<IEquivalenciaClasificacionService>();
            equivalenciaService.ListarAsync(Arg.Any<int>(), Arg.Any<TipoEquivalenciaEnum?>(), Arg.Any<CancellationToken>())
                .Returns(_ => Lista(DatosDeMuestraDialogosGrandes.Equivalencias()));
            var categoriaService = Substitute.For<ICategoriaService>();
            categoriaService.ListarAsync(Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
                .Returns(_ => Lista(DatosDeMuestraDialogosGrandes.Categorias()));
            var marcaService = Substitute.For<IMarcaService>();
            marcaService.ListarAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(_ => Lista(DatosDeMuestraDialogosGrandes.Marcas()));

            using var viewModel = new EquivalenciasProveedorViewModel(
                equivalenciaService,
                categoriaService,
                marcaService,
                Substitute.For<IClasificacionDialogService>(),
                CrearSesionDeGerente());
            viewModel.Configurar(DatosDeMuestraDialogosGrandes.Proveedor());

            // El Loaded del diálogo llama a InicializarAsync y trae las equivalencias desde los servicios.
            var dialogo = new EquivalenciasProveedorDialog { DataContext = viewModel };

            return SnapshotRenderer.Guardar(
                dialogo,
                580,
                684,
                carpeta + "/EquivalenciasProveedorDialog",
                () => viewModel.Equivalencias.Count > 0);
        });

        VerificarCaptura(ruta, error);
    }

    // ------------------------------------------------------------------ SeleccionarCatalogoProveedorModalDialog

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public async Task SeleccionarCatalogoProveedorModalDialog_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        // La inicialización del ViewModel es asíncrona: se completa antes de entrar al hilo STA, como en AppSmokeTests.
        var proveedorService = Substitute.For<IProveedorService>();
        proveedorService.ListarProveedoresAsync(Arg.Any<CancellationToken>())
            .Returns(DatosDeMuestraDialogosGrandes.Proveedores());
        var items = DatosDeMuestraDialogosGrandes.ItemsParaSelectorDeCatalogo();
        proveedorService.ListarItemsCatalogoAsync(Arg.Any<ConsultaCatalogoProveedorDto>(), Arg.Any<CancellationToken>())
            .Returns(new CatalogoPaginadoDto { Items = items, TotalRegistros = items.Count, PaginaActual = 1, TamanoPagina = 50 });
        var viewModel = new SeleccionarCatalogoProveedorModalViewModel(proveedorService);
        await viewModel.InicializarAsync(new ContextoSelectorFuente(7, "Cuaderno A4 rayado", "7790001000011", []));

        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var dialogo = new SeleccionarCatalogoProveedorModalDialog(viewModel);

            return SnapshotRenderer.Guardar(dialogo, 760, 584, carpeta + "/SeleccionarCatalogoProveedorModalDialog");
        });

        VerificarCaptura(ruta, error);
    }

    // ------------------------------------------------------------------ SeleccionarClienteModalDialog

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void SeleccionarClienteModalDialog_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            using var viewModel = new SeleccionarClienteModalViewModel(Substitute.For<IClienteService>());
            foreach (var cliente in DatosDeMuestraDialogosGrandes.Clientes())
            {
                viewModel.Clientes.Add(cliente);
            }

            viewModel.ClienteSeleccionado = viewModel.Clientes[1];
            var dialogo = new SeleccionarClienteModalDialog(viewModel);

            return SnapshotRenderer.Guardar(dialogo, 520, 604, carpeta + "/SeleccionarClienteModalDialog");
        });

        VerificarCaptura(ruta, error);
    }

    // ------------------------------------------------------------------ Armado de ViewModels

    private static ICurrentUserSession CrearSesionDeGerente()
    {
        // Gerente: habilita las acciones de escritura (alta, edición y baja) que las capturas deben mostrar.
        var sesion = Substitute.For<ICurrentUserSession>();
        sesion.EsGerente.Returns(true);
        return sesion;
    }

    private static Task<IReadOnlyList<T>> Lista<T>(IEnumerable<T> elementos)
    {
        return Task.FromResult<IReadOnlyList<T>>(elementos.ToList());
    }

    private static AdministrarClasificacionViewModel CrearAdministrarClasificacionViewModel()
    {
        var rubroService = Substitute.For<IRubroService>();
        rubroService.ListarAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Lista(DatosDeMuestraDialogosGrandes.Rubros()));

        // Las categorías se filtran por rubro como lo hace el servicio real.
        var categoriaService = Substitute.For<ICategoriaService>();
        categoriaService.ListarAsync(Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(llamada =>
            {
                var idRubro = llamada.ArgAt<int?>(1);
                return Lista(DatosDeMuestraDialogosGrandes.Categorias().Where(c => idRubro is null || c.IdRubro == idRubro));
            });

        // Los alias y prefijos solo existen para la primera marca (Rivadavia), la que queda seleccionada.
        var marcaService = Substitute.For<IMarcaService>();
        marcaService.ListarAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Lista(DatosDeMuestraDialogosGrandes.Marcas()));
        marcaService.ListarAliasAsync(Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(llamada => Lista(DatosDeMuestraDialogosGrandes.AliasDeRivadavia().Where(a => a.IdMarca == llamada.ArgAt<int?>(1))));
        marcaService.ListarPrefijosAsync(Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(llamada => Lista(DatosDeMuestraDialogosGrandes.PrefijosDeRivadavia().Where(p => p.IdMarca == llamada.ArgAt<int?>(1))));

        // Los pares y los pares propuestos pertenecen al término ambiguo "CUADERNO" (IdTermino = 1).
        var terminoService = Substitute.For<ITerminoClasificacionService>();
        terminoService.ListarAsync(Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(llamada => Lista(DatosDeMuestraDialogosGrandes.Terminos().Where(t => !llamada.ArgAt<bool>(2) || t.EsAmbiguo)));
        terminoService.ListarParesAsync(Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(_ => Lista(DatosDeMuestraDialogosGrandes.ParesDeCuaderno()));
        terminoService.ListarParesPropuestosAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Lista(DatosDeMuestraDialogosGrandes.ParesPropuestosDeCuaderno()));

        return new AdministrarClasificacionViewModel(
            rubroService,
            categoriaService,
            marcaService,
            terminoService,
            Substitute.For<IClasificacionDialogService>(),
            CrearSesionDeGerente());
    }

    private static IncorporarArticulosModalViewModel CrearIncorporarArticulosViewModel(PasoIncorporacionEnum paso)
    {
        var viewModel = new IncorporarArticulosModalViewModel(
            Substitute.For<IClasificacionService>(),
            Substitute.For<ITerminoClasificacionService>(),
            Substitute.For<IEquivalenciaClasificacionService>(),
            Substitute.For<IRubroService>(),
            CrearSesionDeGerente(),
            Substitute.For<ILogger<IncorporarArticulosModalViewModel>>());

        // Paso 3: ocho ítems del proveedor, seis con sugerencia del motor y uno corregido a mano.
        viewModel.Inicializar(
            DatosDeMuestraDialogosGrandes.Categorias(),
            DatosDeMuestraDialogosGrandes.Marcas(),
            DatosDeMuestraDialogosGrandes.ItemsDeCatalogo());
        foreach (var sugerencia in DatosDeMuestraDialogosGrandes.Sugerencias())
        {
            viewModel.Items.Single(i => i.IdCatalogo == sugerencia.IdCatalogo).AplicarSugerencia(sugerencia);
        }

        viewModel.Items[2].CategoriaSeleccionada = viewModel.OpcionesCategoria.First(o => o.Id == 5);

        // Paso 1: valores del proveedor sin traducir, con dos destinos ya elegidos y un valor resuelto.
        viewModel.PasoTraducirValores.Cargar(1, DatosDeMuestraDialogosGrandes.ValoresSinTraducir(), puedeAdministrar: true);
        viewModel.PasoTraducirValores.Valores[0].CategoriaDestino = viewModel.CategoriasDisponibles[0];
        viewModel.PasoTraducirValores.Valores[3].MarcaDestino = viewModel.MarcasDisponibles[1];
        viewModel.PasoTraducirValores.Valores[4].MarcaDestino = viewModel.MarcasDisponibles[2];
        viewModel.PasoTraducirValores.Valores[4].Resultado = "Traducido a Bic";
        viewModel.PasoTraducirValores.Valores[4].Resuelto = true;

        // Paso 2: términos nuevos del lote, uno ya resuelto. Se cargan a mano porque CargarAsync es asíncrono.
        viewModel.PasoTerminosNuevos.PuedeAdministrar = true;
        foreach (var rubro in DatosDeMuestraDialogosGrandes.Rubros())
        {
            viewModel.PasoTerminosNuevos.Rubros.Add(rubro);
        }

        foreach (var bloque in DatosDeMuestraDialogosGrandes.BloquesDeTerminosNuevos())
        {
            viewModel.PasoTerminosNuevos.Bloques.Add(new BloqueTerminoNuevoViewModel(bloque));
        }

        viewModel.PasoTerminosNuevos.Bloques[0].CategoriaSeleccionada = viewModel.CategoriasDisponibles[0];
        viewModel.PasoTerminosNuevos.Bloques[3].Resultado = "Marcado como ambiguo";
        viewModel.PasoTerminosNuevos.Bloques[3].Resuelto = true;

        viewModel.TienePasoTraducirValores = true;
        viewModel.TienePasoTerminosNuevos = true;
        viewModel.PasoActual = paso;
        return viewModel;
    }

    private static ImportarPlanillaViewModel CrearImportarPlanillaViewModel()
    {
        var viewModel = new ImportarPlanillaViewModel(
            Substitute.For<IProveedorService>(),
            Substitute.For<ILogger<ImportarPlanillaViewModel>>());
        viewModel.Inicializar(DatosDeMuestraDialogosGrandes.Proveedor());

        // Cambiar la ruta descarta el estado anterior, así que se asigna antes que el mensaje.
        viewModel.RutaArchivo = @"C:\Planillas\Distribuidora Norte\lista-precios-octubre-2026.xlsx";
        viewModel.MensajeEstado = "Archivo seleccionado: lista-precios-octubre-2026.xlsx";
        return viewModel;
    }

    // ------------------------------------------------------------------ Soporte (mismo patrón que VistaSnapshotTests)

    private static (string? Ruta, Exception? Error) Capturar(TemaPrueba tema, Func<string, string> accion)
    {
        string? ruta = null;
        Exception? error = null;
        var carpeta = tema == TemaPrueba.Oscuro ? "Dark" : "Light";

        WpfTestHelper.Run(() =>
        {
            try
            {
                WpfTestHelper.AplicarTema(tema);
                ruta = accion(carpeta);
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                WpfTestHelper.AplicarTema(TemaPrueba.Claro);
            }
        });

        return (ruta, error);
    }

    private static void VerificarCaptura(string? ruta, Exception? error)
    {
        error.Should().BeNull("el diálogo debe poder renderizarse a PNG sin excepciones");
        ruta.Should().NotBeNull();
        File.Exists(ruta).Should().BeTrue();
        new FileInfo(ruta!).Length.Should().BeGreaterThan(TamanoMinimoPng);
    }
}
