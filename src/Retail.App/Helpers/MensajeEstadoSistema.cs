using Retail.Application.DTOs.Sistema;

namespace Retail.App.Helpers;

/// <summary>
/// Resultado de la verificación de arranque: si se debe impedir el acceso, qué explicar al operador y qué comandos de
/// <c>Retail.Cli</c> debe ejecutar (la aplicación nunca los ejecuta: D-10/D-11).
/// </summary>
/// <param name="Bloquea">La aplicación no puede operar con la base en este estado.</param>
/// <param name="Mensaje">Explicación del estado; <c>null</c> si no hay nada que informar.</param>
/// <param name="Argumentos">Argumentos de <c>Retail.Cli</c> a ejecutar, en orden (ej. <c>migrar</c>); vacía si no hay acción posible.</param>
public sealed record DiagnosticoArranque(bool Bloquea, string? Mensaje, IReadOnlyList<string> Argumentos);

/// <summary>
/// Traduce el estado de la base a un mensaje accionable para quien abre la aplicación (RF-23). Es presentación:
/// el estado lo calcula <c>IEstadoSistemaService</c>; acá solo se decide cómo contarlo.
/// </summary>
public static class MensajeEstadoSistema
{
    private const string Migrar = "migrar";
    private const string Inicializar = "inicializar";

    public static DiagnosticoArranque Describir(EstadoSistemaDto estado)
    {
        ArgumentNullException.ThrowIfNull(estado);
        var esquema = estado.Esquema;

        if (!esquema.PuedeConectar)
        {
            return Bloqueo(
                "No se pudo conectar con la base de datos, o la base todavía no existe.\n\n" +
                "Si es una instalación nueva, ejecute en PowerShell, en este orden, los comandos de abajo.\n\n" +
                "Si la base ya existía, verifique que SQL Server esté disponible.",
                [Migrar, Inicializar]);
        }

        // Se informa antes que las pendientes: si la base es más nueva, migrar no lo resuelve.
        if (esquema.MigracionesDesconocidas.Count > 0)
        {
            return Bloqueo(
                "La base de datos es más nueva que esta versión de Retail POS " +
                $"({esquema.MigracionesDesconocidas.Count} migraciones desconocidas, la última: " +
                $"{esquema.MigracionesDesconocidas[^1]}).\n\n" +
                "Actualice la aplicación antes de continuar.",
                []);
        }

        if (esquema.MigracionesPendientes.Count > 0)
        {
            return Bloqueo(
                $"La base de datos debe actualizarse ({esquema.MigracionesPendientes.Count} migraciones pendientes).\n\n" +
                "Ejecute en PowerShell el comando de abajo.",
                [Migrar]);
        }

        if (!estado.ExisteGerenteActivo)
        {
            return Bloqueo(
                "No hay ningún Gerente activo, por lo que nadie podría administrar el sistema.\n\n" +
                "Ejecute en PowerShell el comando de abajo.",
                [Inicializar]);
        }

        if (estado.GerentesSinEmail.Count > 0)
        {
            return new DiagnosticoArranque(
                false,
                "Los siguientes Gerentes no tienen email de recuperación: " +
                $"{string.Join(", ", estado.GerentesSinEmail)}.\n\n" +
                "Cárguelo desde Usuarios: sin él, la cuenta no podrá recuperarse por correo.",
                []);
        }

        return new DiagnosticoArranque(false, null, []);
    }

    private static DiagnosticoArranque Bloqueo(string mensaje, IReadOnlyList<string> argumentos)
    {
        return new DiagnosticoArranque(true, mensaje, argumentos);
    }
}
