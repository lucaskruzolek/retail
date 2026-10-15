namespace Retail.Cli;

internal static class Ayuda
{
    public static void Mostrar()
    {
        Console.WriteLine(
            """
            Retail.Cli - Administración de la base de datos de Retail POS (RF-23)

            Uso: Retail.Cli <comando> [argumento]

            Comandos:
              estado        Informa si la base está al día y si hay un Gerente activo.
              migrar        Actualiza el esquema de la base (la crea si no existe), con copia previa.
              inicializar   Crea la cuenta del primer Gerente (solo si no hay ninguno).
              datos-demo    Carga un proveedor, categorías y marcas de demostración.
              backup        Copia completa y verificada de la base. La tarea programada diaria (21:00, como SYSTEM)
                            la ejecuta; se registra con scripts\registrar-tarea-backup.ps1 como administrador.
              restaurar <ruta.bak>
                            Reemplaza la base por una copia; exige usuario y contraseña de un Gerente de esa copia.
              ayuda         Muestra este mensaje.

            Instalación nueva: migrar, inicializar y, opcionalmente, datos-demo.
            En desarrollo: dotnet run --project src/Retail.Cli -- <comando>
            """);
    }
}
