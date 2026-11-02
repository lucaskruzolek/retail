using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.App.UnitTests.Helpers;
using Retail.App.ViewModels.Articulos;
using Retail.App.ViewModels.Auth;
using Retail.App.ViewModels.Backups;
using Retail.App.ViewModels.Caja;
using Retail.App.ViewModels.Clientes;
using Retail.App.ViewModels.Proveedores;
using Retail.App.ViewModels.Usuarios;
using Retail.App.ViewModels.Ventas;
using Retail.App.Views.Auth;
using Retail.App.Views.Dialogs;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Backups;
using Retail.Application.DTOs.Clientes;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.DTOs.Usuarios;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.App.UnitTests.Snapshots;

/// <summary>
/// "Bucle de capturas" de los diálogos de formulario, de caja y de sistema: renderiza cada uno, con datos de ejemplo,
/// a PNG en <c>artifacts/snapshots/Light</c> y <c>artifacts/snapshots/Dark</c>. Se ejecuta a pedido con
/// <c>--filter "Category=Snapshot"</c>. Ninguna prueba abre el diálogo con <c>ShowDialog</c>: se usa solo
/// <see cref="SnapshotRenderer"/>.
/// </summary>
[Trait("Category", "Snapshot")]
public class DialogosSnapshotTests
{
    private const int TamanoMinimoPng = 1000;

    // ---------- Formularios ----------

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void ClienteFormDialog_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var viewModel = new ClienteFormViewModel();
            viewModel.ConfigurarEdicion(new ClienteDto
            {
                IdCliente = 12,
                RazonSocialONombre = "Papelera San Martín S.R.L.",
                TipoDocumento = TipoDocumentoEnum.Cuit,
                NumeroDocumento = "30712345678",
                CondicionIva = CondicionIvaEnum.ResponsableInscripto,
                DomicilioFiscal = "Av. San Martín 1450, Mendoza",
                Telefono = "261 4235567",
                Email = "compras@papelerasanmartin.com.ar",
                TieneCuentaCorriente = true,
                LimiteCredito = 150000m,
                SaldoCuentaCorriente = 42500m
            });

            var dialogo = new ClienteFormDialog(viewModel);
            return SnapshotRenderer.Guardar(dialogo, 540, 664, carpeta + "/ClienteFormDialog");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void ProveedorFormDialog_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var viewModel = new ProveedorFormViewModel(Substitute.For<IProveedorService>());
            viewModel.ConfigurarEdicion(new ProveedorDto
            {
                IdProveedor = 3,
                RazonSocial = "Distribuidora Cuyo Papel S.A.",
                Cuit = "30-70123456-8",
                Telefono = "261 4290011",
                Email = "ventas@cuyopapel.com.ar"
            });

            var dialogo = new ProveedorFormDialog { DataContext = viewModel };
            return SnapshotRenderer.Guardar(dialogo, 480, 484, carpeta + "/ProveedorFormDialog");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void UsuarioFormDialog_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var viewModel = new UsuarioFormViewModel();
            viewModel.ConfigurarAlta(Enum.GetValues<RolUsuarioEnum>());
            viewModel.NombreUsuario = "mgarcia";
            viewModel.Nombre = "María";
            viewModel.Apellido = "García";
            viewModel.EmailRecuperacion = "maria.garcia@libreria.com.ar";

            var dialogo = new UsuarioFormDialog(viewModel);
            return SnapshotRenderer.Guardar(dialogo, 520, 644, carpeta + "/UsuarioFormDialog");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void PresentacionFormDialog_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var viewModel = new PresentacionFormViewModel();
            viewModel.Configurar(ArticuloDeEjemplo(idArticulo: 7, descripcion: "Sobre manila (pack x100)", costo: 1000m, precio: 1400m, stock: 18));
            viewModel.Descripcion = "Sobre manila (unidad)";
            viewModel.CodigoBarras = "7790001000042";
            viewModel.UnidadesPorOrigen = 100;
            viewModel.StockMinimo = 20;

            var dialogo = new PresentacionFormDialog(viewModel);
            return SnapshotRenderer.Guardar(dialogo, 540, 664, carpeta + "/PresentacionFormDialog");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void ClasificacionFormDialog_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var viewModel = new ClasificacionFormViewModel(
                Substitute.For<IRubroService>(),
                Substitute.For<ICategoriaService>(),
                Substitute.For<IMarcaService>(),
                Substitute.For<ICurrentUserSession>());
            viewModel.ConfigurarAltaCategoria(
                [
                    new RubroDto { IdRubro = 1, NombreRubro = "Librería" },
                    new RubroDto { IdRubro = 2, NombreRubro = "Papelería" }
                ],
                idRubroPreseleccionado: 1);
            viewModel.Nombre = "Cuadernos universitarios";

            // El XAML declara Width="460" y SizeToContent="Height": el alto lo da el contenido.
            var dialogo = new ClasificacionFormDialog { DataContext = viewModel };
            return SnapshotRenderer.Guardar(dialogo, 460, 480, carpeta + "/ClasificacionFormDialog");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void DiccionarioFormDialog_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var viewModel = new DiccionarioFormViewModel(
                "Nuevo término",
                "Término *",
                "Cuaderno",
                textoEditable: true,
                "Categoría",
                [new OpcionDiccionario(null, "Ambiguo (sin categoría)"), new OpcionDiccionario(10, "Cuadernos"), new OpcionDiccionario(11, "Carpetas")],
                idOpcionInicial: 10,
                opcionObligatoria: false);

            var dialogo = new DiccionarioFormDialog { DataContext = viewModel };
            return SnapshotRenderer.Guardar(dialogo, 460, 480, carpeta + "/DiccionarioFormDialog");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void CambiarPasswordDialog_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var viewModel = new CambiarPasswordViewModel();

            // El cambio propio es el modo más completo: además de la nueva contraseña pide la actual.
            viewModel.ConfigurarCambioPropio(UsuarioDeEjemplo());

            var dialogo = new CambiarPasswordDialog(viewModel);
            return SnapshotRenderer.Guardar(dialogo, 460, 414, carpeta + "/CambiarPasswordDialog");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void FraccionarDialog_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var viewModel = new FraccionarViewModel();
            viewModel.Configurar(
                ArticuloDeEjemplo(idArticulo: 8, descripcion: "Sobre manila (unidad)", costo: 10m, precio: 14m, stock: 35, idArticuloOrigen: 7, unidadesPorOrigen: 100),
                ArticuloDeEjemplo(idArticulo: 7, descripcion: "Sobre manila (pack x100)", costo: 1000m, precio: 1400m, stock: 6));
            viewModel.CantidadOrigen = 2;

            var dialogo = new FraccionarDialog(viewModel);
            return SnapshotRenderer.Guardar(dialogo, 480, 564, carpeta + "/FraccionarDialog");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void VincularArticuloModalDialog_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            // No se invoca InicializarAsync (consulta al servicio): se cargan a mano el ítem y los resultados de la búsqueda.
            var viewModel = new VincularArticuloModalViewModel(Substitute.For<IInventarioService>())
            {
                ItemCatalogo = new CatalogoProveedorDto
                {
                    Id = 41,
                    IdProveedor = 3,
                    ProveedorRazonSocial = "Distribuidora Cuyo Papel S.A.",
                    CodigoProveedor = "CP-0512",
                    CodigoBarras = "7790001000011",
                    DescripcionProveedor = "Resma A4 75 g x 500 hojas",
                    CostoReposicion = 7200m
                }
            };
            viewModel.ArticulosEncontrados.Add(new ArticuloVentaDto
            {
                IdArticulo = 1,
                CodigoBarras = "7790001000011",
                Descripcion = "Hoja A4 Autor 75 g x 500",
                PrecioVenta = 9800m,
                StockActual = 120,
                EsServicio = false
            });
            viewModel.ArticulosEncontrados.Add(new ArticuloVentaDto
            {
                IdArticulo = 2,
                CodigoBarras = "7790001000028",
                Descripcion = "Hoja A4 Ledesma 80 g x 500",
                PrecioVenta = 11200m,
                StockActual = 45,
                EsServicio = false
            });
            viewModel.ArticuloSeleccionado = viewModel.ArticulosEncontrados[0];

            var dialogo = new VincularArticuloModalDialog { DataContext = viewModel };
            return SnapshotRenderer.Guardar(dialogo, 580, 624, carpeta + "/VincularArticuloModalDialog");
        });

        VerificarCaptura(ruta, error);
    }

    // ---------- Caja ----------

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void AperturaTurnoDialog_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var dialogo = new AperturaTurnoDialog();
            dialogo.ViewModel.SaldoInicial = 25000m;

            // El XAML declara Width="420" y SizeToContent="Height": el alto lo da el contenido.
            return SnapshotRenderer.Guardar(dialogo, 420, 480, carpeta + "/AperturaTurnoDialog");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void ArqueoCiegoDialog_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var dialogo = new ArqueoCiegoDialog(idTurno: 1);
            var viewModel = (ArqueoCiegoViewModel)dialogo.DataContext;
            viewModel.SaldoDeclaradoEfectivo = 187350m;
            viewModel.MontoRetenidoEnCaja = 25000m;

            return SnapshotRenderer.Guardar(dialogo, 460, 560, carpeta + "/ArqueoCiegoDialog");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void MovimientoCajaDialog_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var dialogo = new MovimientoCajaDialog(idTurno: 1);
            dialogo.ViewModel.TipoMovimiento = TipoMovimientoCajaEnum.RetiroExtraordinario;
            dialogo.ViewModel.Monto = 3500m;
            dialogo.ViewModel.Concepto = "Pago de flete de mercadería";

            return SnapshotRenderer.Guardar(dialogo, 480, 520, carpeta + "/MovimientoCajaDialog");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void CobroModalDialog_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var viewModel = new CobroModalViewModel(18650m, ClienteConCuentaCorriente());

            // Pago mixto: primero 10.000 en efectivo (con vuelto) y queda un saldo pendiente para el segundo medio.
            viewModel.MontoImputar = 10000m;
            viewModel.EfectivoRecibido = 12000m;
            viewModel.ImputarPago();
            viewModel.MedioPagoSeleccionado = MedioPagoEnum.TransferenciaQr;

            var dialogo = new CobroModalDialog(viewModel);
            return SnapshotRenderer.Guardar(dialogo, 560, 704, carpeta + "/CobroModalDialog");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void CobranzaModalDialog_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var viewModel = new CobranzaModalViewModel(
                Substitute.For<IClienteService>(),
                Substitute.For<ICajaService>(),
                ClienteConCuentaCorriente());
            viewModel.MontoCobro = 8000m;
            viewModel.EfectivoRecibido = 10000m;

            var dialogo = new CobranzaModalDialog(viewModel);
            return SnapshotRenderer.Guardar(dialogo, 520, 644, carpeta + "/CobranzaModalDialog");
        });

        VerificarCaptura(ruta, error);
    }

    // ---------- Sistema ----------

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void ConfirmarCredencialDialog_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            Func<string, CredencialAutorizanteDto, IProgress<int>, Task> operacion = (_, _, _) => Task.CompletedTask;

            // Restauración autorizada: el modo más completo (aviso fiscal, frase de confirmación y Gerente autorizante).
            var viewModel = ConfirmarCredencialViewModel.ParaRestauracionAutorizada(
                operacion,
                "01/10/2026 10:00",
                "Conciliar con ARCA los comprobantes emitidos después de esa fecha.");
            viewModel.NombreUsuarioAutorizante = "glopez";
            viewModel.Frase = ConfirmarCredencialViewModel.FraseRestauracion;
            viewModel.MensajeError = "La contraseña del Gerente es incorrecta.";

            // El XAML declara Width="500" y SizeToContent="Height": el alto lo da el contenido.
            var dialogo = new ConfirmarCredencialDialog(viewModel);
            return SnapshotRenderer.Guardar(dialogo, 500, 720, carpeta + "/ConfirmarCredencialDialog");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void DiagnosticoArranqueDialog_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var diagnostico = new DiagnosticoArranque(
                true,
                "La base de datos no está actualizada: faltan migraciones y datos iniciales.",
                ["migrar", "inicializar"]);

            var dialogo = new DiagnosticoArranqueDialog(diagnostico);
            return SnapshotRenderer.Guardar(dialogo, 680, 544, carpeta + "/DiagnosticoArranqueDialog");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void UnhandledExceptionDialog_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var dialogo = new UnhandledExceptionDialog(
                ExcepcionConPila(),
                isFatal: false,
                @"C:\Retail\logs\retail-20261031.log");

            return SnapshotRenderer.Guardar(dialogo, 660, 564, carpeta + "/UnhandledExceptionDialog");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void LoginWindow_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var viewModel = new LoginViewModel(
                Substitute.For<IAuthService>(),
                Substitute.For<IUsuarioService>(),
                Substitute.For<IUsuarioDialogService>(),
                Substitute.For<ICurrentUserSession>(),
                Substitute.For<ILogger<LoginViewModel>>());
            viewModel.NombreUsuario = "mgarcia";
            viewModel.MensajeError = "Usuario o contraseña incorrectos.";
            viewModel.TieneError = true;

            var ventana = new LoginWindow(viewModel);
            return SnapshotRenderer.Guardar(ventana, 440, 540, carpeta + "/LoginWindow");
        });

        VerificarCaptura(ruta, error);
    }

    // ---------- Datos de muestra ----------

    private static ArticuloDto ArticuloDeEjemplo(
        int idArticulo,
        string descripcion,
        decimal costo,
        decimal precio,
        int stock,
        int? idArticuloOrigen = null,
        int? unidadesPorOrigen = null)
    {
        return new ArticuloDto
        {
            IdArticulo = idArticulo,
            Descripcion = descripcion,
            IdArticuloOrigen = idArticuloOrigen,
            UnidadesPorOrigen = unidadesPorOrigen,
            CostoReposicion = costo,
            PorcentajeGanancia = 40m,
            PrecioVenta = precio,
            StockActual = stock,
            StockMinimo = 5,
            EsServicio = false
        };
    }

    private static UsuarioDto UsuarioDeEjemplo()
    {
        return new UsuarioDto
        {
            IdUsuario = 2,
            NombreUsuario = "jperez",
            Nombre = "Juan",
            Apellido = "Pérez",
            Rol = RolUsuarioEnum.Cajero,
            Activo = true,
            CreatedAt = new DateTime(2026, 3, 15, 9, 0, 0, DateTimeKind.Utc)
        };
    }

    private static ClienteDto ClienteConCuentaCorriente()
    {
        return new ClienteDto
        {
            IdCliente = 10,
            RazonSocialONombre = "Colegio San Martín",
            TipoDocumento = TipoDocumentoEnum.Cuit,
            NumeroDocumento = "30711223344",
            CondicionIva = CondicionIvaEnum.ResponsableInscripto,
            TieneCuentaCorriente = true,
            LimiteCredito = 150000m,
            SaldoCuentaCorriente = 42500m
        };
    }

    /// <summary>
    /// Lanza y atrapa una excepción real para que lleve pila de llamadas (una recién creada no la tiene).
    /// </summary>
    private static InvalidOperationException ExcepcionConPila()
    {
        try
        {
            throw new InvalidOperationException("No se pudo confirmar la venta: el turno de caja ya fue cerrado.");
        }
        catch (InvalidOperationException ex)
        {
            return ex;
        }
    }

    // ---------- Soporte de capturas (mismo contrato que VistaSnapshotTests) ----------

    /// <summary>
    /// Aplica el tema pedido, ejecuta la captura en el hilo STA y SIEMPRE restaura el tema claro: la
    /// <c>Application</c> se comparte con el resto de la suite.
    /// </summary>
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
