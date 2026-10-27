using Retail.Application.Interfaces.Services;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Initialization;

namespace Retail.Cli.Comandos;

/// <summary>
/// <c>cargar-diccionarios</c>: carga la taxonomía y los diccionarios de clasificación de la semilla que viaja junto
/// al ejecutable (Track 6.6, M-59, M-60). Se puede repetir: solo agrega lo que falta y nunca modifica lo que el
/// comercio editó o dio de baja.
/// </summary>
internal sealed class ComandoCargarDiccionarios
{
    private const string CarpetaSemilla = "Semilla";

    private readonly IEstadoSistemaService _estadoSistema;
    private readonly RetailDbContext _context;

    public ComandoCargarDiccionarios(IEstadoSistemaService estadoSistema, RetailDbContext context)
    {
        _estadoSistema = estadoSistema ?? throw new ArgumentNullException(nameof(estadoSistema));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<int> EjecutarAsync()
    {
        var estado = await _estadoSistema.ObtenerEstadoAsync();
        if (!estado.Esquema.EstaAlDia)
        {
            Console.Error.WriteLine("La base no está al día. Ejecute primero: Retail.Cli migrar");
            return CodigoSalida.Fallo;
        }

        SemillaDiccionarios semilla;
        try
        {
            semilla = LectorSemillaDiccionarios.Leer(Path.Combine(AppContext.BaseDirectory, CarpetaSemilla));
        }
        catch (InvalidDataException ex)
        {
            // Un defecto de los archivos de la semilla (S-1): no se cargó nada.
            Console.Error.WriteLine($"La semilla de diccionarios no es válida y no se cargó nada. {ex.Message}");
            return CodigoSalida.Fallo;
        }

        var resultado = await SemilleroDiccionarios.CargarAsync(_context, semilla);
        MostrarResumen(resultado);
        return CodigoSalida.Exito;
    }

    private static void MostrarResumen(ResultadoCargaDiccionarios resultado)
    {
        Console.WriteLine(resultado.TotalCreados == 0
            ? "Los diccionarios ya estaban cargados. No se agregó nada."
            : $"Se agregaron {resultado.TotalCreados} registros de clasificación.");
        Console.WriteLine();
        Console.WriteLine($"{"",-12}{"Creados",10}{"Existentes",12}{"De baja",10}{"Omitidos",10}");
        MostrarFila("Rubros", resultado.Rubros);
        MostrarFila("Categorías", resultado.Categorias);
        MostrarFila("Marcas", resultado.Marcas);
        MostrarFila("Alias", resultado.Alias);
        MostrarFila("Prefijos", resultado.Prefijos);
        MostrarFila("Términos", resultado.Terminos);
        MostrarFila("Pares", resultado.Pares);

        if (resultado.Conflictos.Count == 0)
        {
            return;
        }

        // Los dados de baja no se listan: son decisiones del comercio, no conflictos (S-2).
        Console.WriteLine();
        Console.WriteLine("Se dejó lo que había cargado el comercio en estos casos:");
        foreach (var conflicto in resultado.Conflictos)
        {
            Console.WriteLine($"  - {conflicto}");
        }
    }

    private static void MostrarFila(string tipo, ConteoCarga conteo)
    {
        Console.WriteLine(
            $"{tipo,-12}{conteo.Creados,10}{conteo.Existentes,12}{conteo.DadosDeBaja,10}{conteo.Omitidos,10}");
    }
}
