using Retail.Application.DTOs.Sistema;

namespace Retail.App.Helpers;

/// <summary>
/// Resultado de la verificación de arranque: si se debe impedir el acceso y qué mostrar al operador.
/// </summary>
/// <param name="Bloquea">La aplicación no puede operar con la base en este estado.</param>
/// <param name="Mensaje">Explicación con la acción a tomar; <c>null</c> si no hay nada que informar.</param>
public sealed record DiagnosticoArranque(bool Bloquea, string? Mensaje);

/// <summary>
/// Traduce el estado de la base a un mensaje accionable para quien abre la aplicación (RF-23). Es presentación:
/// el estado lo calcula <c>IEstadoSistemaService</c>; acá solo se decide cómo contarlo.
/// </summary>
public static class MensajeEstadoSistema
{
    private const string AyudaDesarrollo =
        "En desarrollo, desde la raíz del repositorio: dotnet run --project src/Retail.Cli -- <comando>";

    public static DiagnosticoArranque Describir(EstadoSistemaDto estado)
    {
        ArgumentNullException.ThrowIfNull(estado);
        var esquema = estado.Esquema;

        if (!esquema.PuedeConectar)
        {
            return Bloqueo(
                "No se pudo conectar con la base de datos, o la base todavía no existe.\n\n" +
                "Si es una instalación nueva, ejecute en este orden:\n" +
                "    Retail.Cli migrar\n" +
                "    Retail.Cli inicializar\n\n" +
                "Si la base ya existía, verifique que SQL Server esté disponible.");
        }

        // Se informa antes que las pendientes: si la base es más nueva, migrar no lo resuelve.
        if (esquema.MigracionesDesconocidas.Count > 0)
        {
            return Bloqueo(
                "La base de datos es más nueva que esta versión de Retail POS " +
                $"({esquema.MigracionesDesconocidas.Count} migraciones desconocidas, la última: " +
                $"{esquema.MigracionesDesconocidas[^1]}).\n\n" +
                "Actualice la aplicación antes de continuar.");
        }

        if (esquema.MigracionesPendientes.Count > 0)
        {
            return Bloqueo(
                $"La base de datos debe actualizarse ({esquema.MigracionesPendientes.Count} migraciones pendientes).\n\n" +
                "Ejecute:\n    Retail.Cli migrar");
        }

        if (!estado.ExisteGerenteActivo)
        {
            return Bloqueo(
                "No hay ningún Gerente activo, por lo que nadie podría administrar el sistema.\n\n" +
                "Ejecute:\n    Retail.Cli inicializar");
        }

        if (estado.GerentesSinEmail.Count > 0)
        {
            return new DiagnosticoArranque(
                false,
                "Los siguientes Gerentes no tienen email de recuperación: " +
                $"{string.Join(", ", estado.GerentesSinEmail)}.\n\n" +
                "Cárguelo desde Usuarios: sin él, la cuenta no podrá recuperarse por correo.");
        }

        return new DiagnosticoArranque(false, null);
    }

    private static DiagnosticoArranque Bloqueo(string mensaje)
    {
        return new DiagnosticoArranque(true, $"{mensaje}\n\n{AyudaDesarrollo}");
    }
}
