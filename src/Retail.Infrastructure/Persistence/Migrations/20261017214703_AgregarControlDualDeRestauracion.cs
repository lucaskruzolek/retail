using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarControlDualDeRestauracion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "intentos_autorizacion_fallidos",
                table: "USUARIOS",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "id_operador",
                table: "REGISTROS_RESTAURACION",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nombre_operador",
                table: "REGISTROS_RESTAURACION",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_USUARIOS_IntentosAutorizacionFallidos",
                table: "USUARIOS",
                sql: "[intentos_autorizacion_fallidos] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_REGISTROS_RESTAURACION_Operador",
                table: "REGISTROS_RESTAURACION",
                sql: "([id_operador] IS NULL AND [nombre_operador] IS NULL) OR ([id_operador] > 0 AND [nombre_operador] IS NOT NULL AND [id_operador] <> [id_usuario])");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_USUARIOS_IntentosAutorizacionFallidos",
                table: "USUARIOS");

            migrationBuilder.DropCheckConstraint(
                name: "CK_REGISTROS_RESTAURACION_Operador",
                table: "REGISTROS_RESTAURACION");

            migrationBuilder.DropColumn(
                name: "intentos_autorizacion_fallidos",
                table: "USUARIOS");

            migrationBuilder.DropColumn(
                name: "id_operador",
                table: "REGISTROS_RESTAURACION");

            migrationBuilder.DropColumn(
                name: "nombre_operador",
                table: "REGISTROS_RESTAURACION");
        }
    }
}
