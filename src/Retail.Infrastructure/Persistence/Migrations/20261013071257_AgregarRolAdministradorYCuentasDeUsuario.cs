using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Agrega el email de recuperación y la obligación de cambiar la contraseña a USUARIOS, y siembra los cuatro
    /// roles con identificadores fijos iguales a <c>RolUsuarioEnum</c> (Track 6.3, Fase 1).
    /// </summary>
    /// <remarks>
    /// Los roles pasan del seeder a la migración porque son datos de referencia del esquema (D-12): el código los
    /// identifica por el valor del enum. Cada rol se inserta solo si su identificador no existe, así la misma
    /// migración sirve para una base que ya tiene Cajero, Encargado y Gerente (solo agrega Administrador) y para
    /// una base vacía (crea los cuatro). Se usa IDENTITY_INSERT porque id_rol es IDENTITY: sin él, la base
    /// asignaría el siguiente número libre y el rol no coincidiría con el enum.
    /// Las cuentas existentes quedan con debe_cambiar_password = 0: la obligación nace con las altas y blanqueos
    /// posteriores.
    /// </remarks>
    public partial class AgregarRolAdministradorYCuentasDeUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "debe_cambiar_password",
                table: "USUARIOS",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "email_recuperacion",
                table: "USUARIOS",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(
                """
                SET IDENTITY_INSERT ROLES ON;

                INSERT INTO ROLES (id_rol, nombre_rol, descripcion, created_at)
                SELECT v.id_rol, v.nombre_rol, v.descripcion, SYSUTCDATETIME()
                FROM (VALUES
                    (1, N'Cajero', N'Operador de punto de venta, mostrador y cobranzas'),
                    (2, N'Encargado', N'Encargado de inventario, compras a distribuidores y control de caja'),
                    (3, N'Gerente', N'Dueño del negocio: autoridad total, consola fiscal y respaldos de la base'),
                    (4, N'Administrador', N'Soporte técnico: gestiona Cajeros y Encargados, sin acceso al negocio ni a los respaldos')
                ) AS v (id_rol, nombre_rol, descripcion)
                WHERE NOT EXISTS (SELECT 1 FROM ROLES r WHERE r.id_rol = v.id_rol);

                SET IDENTITY_INSERT ROLES OFF;
                """);

            // La descripción sembrada antes presentaba al Gerente como "Administrador general", que ahora es otro rol.
            migrationBuilder.Sql(
                """
                UPDATE ROLES
                SET descripcion = N'Dueño del negocio: autoridad total, consola fiscal y respaldos de la base'
                WHERE id_rol = 3;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Falla por la FK si quedan usuarios con rol Administrador: revertir no debe dejarlos sin rol.
            migrationBuilder.Sql("DELETE FROM ROLES WHERE id_rol = 4;");

            migrationBuilder.Sql(
                """
                UPDATE ROLES
                SET descripcion = N'Administrador general con acceso total, consola fiscal y configuración'
                WHERE id_rol = 3;
                """);

            migrationBuilder.DropColumn(
                name: "debe_cambiar_password",
                table: "USUARIOS");

            migrationBuilder.DropColumn(
                name: "email_recuperacion",
                table: "USUARIOS");
        }
    }
}
