using FluentAssertions;
using Retail.Domain.Common;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.Domain.UnitTests.Common;

public class ReglasOrigenClasificacionTests
{
    [Theory]
    [InlineData(null, 5, OrigenClasificacionEnum.Manual)]
    [InlineData(null, null, OrigenClasificacionEnum.Manual)]
    [InlineData(5, 5, OrigenClasificacionEnum.SugerenciaAceptada)]
    [InlineData(5, 8, OrigenClasificacionEnum.SugerenciaCorregida)]
    [InlineData(5, null, OrigenClasificacionEnum.SugerenciaCorregida)]
    public void Derivar_SegunSugerenciaYConfirmacion_DevuelveElOrigen(int? sugerido, int? confirmado, OrigenClasificacionEnum esperado)
    {
        // (5, null): se sugirió una marca y la persona confirmó "Sin marca": corrigió la sugerencia.
        ReglasOrigenClasificacion.Derivar(sugerido, confirmado).Should().Be(esperado);
    }
}
