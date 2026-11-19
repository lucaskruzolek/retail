using Retail.Domain.Entities;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.IntegrationTests.TestData;

/// <summary>
/// Siembra terminales para las pruebas: todo turno de caja pertenece a una terminal (FK NOT NULL), así que cada prueba
/// que inserta turnos necesita al menos una fila en TERMINALES.
/// </summary>
internal static class TerminalesDePrueba
{
    private static int _secuencia;

    /// <summary>
    /// Inserta una terminal (con nombre único si no se indica uno) y devuelve su Id.
    /// </summary>
    public static async Task<int> SembrarAsync(RetailDbContext contexto, string? nombre = null)
    {
        var numero = Interlocked.Increment(ref _secuencia);
        var terminal = Terminal.Crear(nombre ?? $"Terminal de prueba {numero}", $"EQUIPO-PRUEBA-{numero}");
        contexto.Terminales.Add(terminal);
        await contexto.SaveChangesAsync();

        return terminal.Id;
    }
}
