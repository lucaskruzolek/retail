using FluentAssertions;
using Retail.App.Helpers;
using Xunit;

namespace Retail.App.UnitTests.Helpers;

public class ComandoCliTests
{
    private const string CarpetaConEspacios = @"C:\Program Files\Retail POS\";

    [Fact]
    public void Formatear_EjecutablePresente_UsaRutaCompletaEntreComillasConOperadorDeLlamada()
    {
        var comando = ComandoCli.Formatear("migrar", CarpetaConEspacios, ruta => ruta.EndsWith("Retail.Cli.exe", StringComparison.Ordinal));

        comando.Linea.Should().Be(@"& ""C:\Program Files\Retail POS\Retail.Cli.exe"" migrar");
        comando.RequiereRaizDelRepositorio.Should().BeFalse();
    }

    [Fact]
    public void Formatear_EjecutablePresenteSinBarraFinal_ArmaLaRutaIgualmente()
    {
        var comando = ComandoCli.Formatear("inicializar", @"C:\Retail", _ => true);

        comando.Linea.Should().Be(@"& ""C:\Retail\Retail.Cli.exe"" inicializar");
    }

    [Fact]
    public void Formatear_EjecutableAusente_CaeADotnetRunYAvisaQueHayQueEstarEnLaRaiz()
    {
        var comando = ComandoCli.Formatear("migrar", CarpetaConEspacios, _ => false);

        comando.Linea.Should().Be("dotnet run --project src/Retail.Cli -- migrar");
        comando.RequiereRaizDelRepositorio.Should().BeTrue();
    }

    [Fact]
    public void Formatear_ConsultaElEjecutableEnLaCarpetaBaseIndicada()
    {
        string? consultada = null;

        ComandoCli.Formatear("migrar", CarpetaConEspacios, ruta =>
        {
            consultada = ruta;
            return false;
        });

        consultada.Should().Be(@"C:\Program Files\Retail POS\Retail.Cli.exe");
    }

    [Fact]
    public void Formatear_ArgumentosVacios_LanzaExcepcion()
    {
        var accion = () => ComandoCli.Formatear(" ", CarpetaConEspacios, _ => true);

        accion.Should().Throw<ArgumentException>();
    }
}
