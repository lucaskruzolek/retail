using FluentAssertions;
using Retail.App.Helpers;
using Xunit;

namespace Retail.App.UnitTests.Helpers;

public class CargaSerializadaTests
{
    private static readonly TimeSpan LimiteEspera = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task EjecutarAsync_ConDosCargas_LaSegundaNoConsultaHastaQueTermineLaPrimera()
    {
        // Arrange: la primera consulta "ocupa el DbContext" hasta que se libera a mano
        using var carga = new CargaSerializada();
        var primeraEnCurso = new TaskCompletionSource<string>();
        var segundaConsulto = false;

        // Act
        var primera = carga.EjecutarAsync(_ => primeraEnCurso.Task, _ => { }, _ => { });
        var segunda = carga.EjecutarAsync(
            _ =>
            {
                segundaConsulto = true;
                return Task.FromResult("B");
            },
            _ => { },
            _ => { });

        // Assert: mientras la primera no termine, la segunda no toca la base
        await Task.Delay(100);
        segundaConsulto.Should().BeFalse("EF Core no admite dos operaciones simultáneas sobre el mismo contexto");

        primeraEnCurso.SetResult("A");
        await Task.WhenAll(primera, segunda).WaitAsync(LimiteEspera);
        segundaConsulto.Should().BeTrue();
    }

    [Fact]
    public async Task EjecutarAsync_ConCargaReemplazada_NoAplicaSuResultadoNiSuError()
    {
        // Arrange
        using var carga = new CargaSerializada();
        var primeraEnCurso = new TaskCompletionSource<string>();
        var aplicados = new List<string>();
        var errores = new List<Exception>();

        // Act: la primera queda obsoleta antes de responder, y además termina con error
        var primera = carga.EjecutarAsync(_ => primeraEnCurso.Task, aplicados.Add, errores.Add);
        var segunda = carga.EjecutarAsync(_ => Task.FromResult("B"), aplicados.Add, errores.Add);
        primeraEnCurso.SetException(new InvalidOperationException("Operation cancelled by user"));
        await Task.WhenAll(primera, segunda).WaitAsync(LimiteEspera);

        // Assert
        aplicados.Should().Equal("B");
        errores.Should().BeEmpty("el error de una carga obsoleta no le interesa al usuario");
        carga.EnCurso.Should().BeFalse();
    }

    [Fact]
    public async Task EjecutarAsync_ConErrorEnLaCargaVigente_InformaElError()
    {
        // Arrange
        using var carga = new CargaSerializada();
        Exception? informado = null;

        // Act
        await carga.EjecutarAsync<string>(
            _ => throw new InvalidOperationException("Sin conexión"),
            _ => { },
            ex => informado = ex).WaitAsync(LimiteEspera);

        // Assert
        informado.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("Sin conexión");
    }

    [Fact]
    public async Task EjecutarAsync_ConEsperaPreviaYOtraCargaAntesDeQueVenza_NoConsulta()
    {
        // Arrange
        using var carga = new CargaSerializada();
        var consultas = new List<string>();

        // Act: dos "teclas" seguidas con debounce
        var primera = carga.EjecutarAsync(
            _ =>
            {
                consultas.Add("bi");
                return Task.FromResult(0);
            },
            _ => { },
            _ => { },
            espera: TimeSpan.FromMilliseconds(200));
        var segunda = carga.EjecutarAsync(
            _ =>
            {
                consultas.Add("bic");
                return Task.FromResult(0);
            },
            _ => { },
            _ => { },
            espera: TimeSpan.FromMilliseconds(200));
        await Task.WhenAll(primera, segunda).WaitAsync(LimiteEspera);

        // Assert
        consultas.Should().Equal("bic");
    }

    [Fact]
    public async Task Dispose_ConCargaEnCurso_LaCancelaYNoAplicaNiInformaNada()
    {
        // Arrange
        var carga = new CargaSerializada();
        var enCurso = new TaskCompletionSource<string>();
        var tokenRecibido = CancellationToken.None;
        var aplicados = new List<string>();
        var errores = new List<Exception>();

        var pendiente = carga.EjecutarAsync(
            token =>
            {
                tokenRecibido = token;
                return enCurso.Task;
            },
            aplicados.Add,
            errores.Add);

        // Act: se descarta la pantalla y la consulta termina con el DbContext ya descartado
        carga.Dispose();
        enCurso.SetException(new ObjectDisposedException("RetailDbContext"));
        await pendiente.WaitAsync(LimiteEspera);

        // Assert
        tokenRecibido.IsCancellationRequested.Should().BeTrue("la consulta en vuelo recibe la cancelación");
        aplicados.Should().BeEmpty();
        errores.Should().BeEmpty("no debe aparecer un error de una pantalla que ya no está");
    }

    [Fact]
    public async Task EjecutarAsync_DespuesDeDispose_NoEjecutaLaConsulta()
    {
        // Arrange
        var carga = new CargaSerializada();
        carga.Dispose();
        var consulto = false;

        // Act
        await carga.EjecutarAsync(
            _ =>
            {
                consulto = true;
                return Task.FromResult(0);
            },
            _ => { },
            _ => { }).WaitAsync(LimiteEspera);

        // Assert
        consulto.Should().BeFalse();
    }

    [Fact]
    public async Task EnCurso_MientrasLaCargaVigenteNoTermina_EsVerdadero()
    {
        // Arrange
        using var carga = new CargaSerializada();
        var enCurso = new TaskCompletionSource<int>();

        // Act
        var pendiente = carga.EjecutarAsync(_ => enCurso.Task, _ => { }, _ => { });
        var durante = carga.EnCurso;
        enCurso.SetResult(1);
        await pendiente.WaitAsync(LimiteEspera);

        // Assert
        durante.Should().BeTrue();
        carga.EnCurso.Should().BeFalse();
    }

    [Fact]
    public async Task EjecutarOperacionAsync_ConCargaNuevaPosterior_NoSeCancelaYAplicaSuResultado()
    {
        // Arrange: por ejemplo, el estado de la caja todavía se está leyendo cuando el usuario empieza a buscar
        using var carga = new CargaSerializada();
        var estadoCaja = new TaskCompletionSource<string>();
        CancellationToken tokenOperacion = default;
        var aplicados = new List<string>();
        var busquedaConsulto = false;

        // Act
        var operacion = carga.EjecutarOperacionAsync(
            token =>
            {
                tokenOperacion = token;
                return estadoCaja.Task;
            },
            aplicados.Add,
            _ => { });
        var busqueda = carga.EjecutarAsync(
            _ =>
            {
                busquedaConsulto = true;
                return Task.FromResult("busqueda");
            },
            aplicados.Add,
            _ => { });

        await Task.Delay(100);
        var busquedaConsultoAntes = busquedaConsulto;
        estadoCaja.SetResult("caja abierta");
        await Task.WhenAll(operacion, busqueda).WaitAsync(LimiteEspera);

        // Assert
        tokenOperacion.IsCancellationRequested.Should().BeFalse("una carga nueva no cancela una operación");
        busquedaConsultoAntes.Should().BeFalse("la búsqueda espera a que la operación libere el DbContext");
        aplicados.Should().Equal("caja abierta", "busqueda");
    }

    [Fact]
    public async Task EjecutarOperacionAsync_ConCargaEnCurso_EsperaSuTurnoAntesDeConsultar()
    {
        // Arrange: el lector de códigos manda Enter mientras la búsqueda predictiva todavía consulta
        using var carga = new CargaSerializada();
        var busquedaEnCurso = new TaskCompletionSource<string>();
        var operacionConsulto = false;
        var aplicados = new List<string>();

        // Act
        var busqueda = carga.EjecutarAsync(_ => busquedaEnCurso.Task, aplicados.Add, _ => { });
        var operacion = carga.EjecutarOperacionAsync(
            _ =>
            {
                operacionConsulto = true;
                return Task.FromResult("articulo escaneado");
            },
            aplicados.Add,
            _ => { });

        await Task.Delay(100);
        var operacionConsultoAntes = operacionConsulto;
        busquedaEnCurso.SetResult("sugerencias");
        await Task.WhenAll(busqueda, operacion).WaitAsync(LimiteEspera);

        // Assert
        operacionConsultoAntes.Should().BeFalse("EF Core no admite dos operaciones simultáneas sobre el mismo contexto");
        aplicados.Should().Equal("sugerencias", "articulo escaneado");
    }
}
