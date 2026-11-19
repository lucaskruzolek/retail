using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TerminalesYTurnoPorTerminal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TURNOS_CAJA_id_usuario",
                table: "TURNOS_CAJA");

            migrationBuilder.DropIndex(
                name: "IX_TURNOS_CAJA_TurnoAbierto",
                table: "TURNOS_CAJA");

            migrationBuilder.CreateTable(
                name: "TERMINALES",
                columns: table => new
                {
                    id_terminal = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    nombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false, collation: "Modern_Spanish_CI_AI"),
                    nombre_equipo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TERMINALES", x => x.id_terminal);
                });

            migrationBuilder.CreateIndex(
                name: "UX_TERMINALES_nombre",
                table: "TERMINALES",
                column: "nombre",
                unique: true,
                filter: "[deleted_at] IS NULL");

            // Relleno de los turnos existentes (MT-1). La columna nace NULL para poder cargarle un valor a los turnos
            // que ya existen, y recién después se pasa a NOT NULL.
            migrationBuilder.AddColumn<int>(
                name: "id_terminal",
                table: "TURNOS_CAJA",
                type: "int",
                nullable: true);

            // Si la base ya tiene turnos, todos se atribuyen a una única terminal histórica, "Terminal 1". Su nombre de
            // equipo queda vacío a propósito: la primera PC que se active "reemplazando" esa terminal le escribe su
            // nombre real y hereda el historial. Si no hay turnos (base nueva) no se inserta nada: la primera
            // activación creará la terminal con el nombre que elija el Gerente.
            migrationBuilder.Sql(
                """
                INSERT INTO TERMINALES (nombre, nombre_equipo, created_at)
                SELECT 'Terminal 1', '', SYSUTCDATETIME()
                WHERE EXISTS (SELECT 1 FROM TURNOS_CAJA);
                """);

            migrationBuilder.Sql(
                """
                UPDATE TURNOS_CAJA
                SET id_terminal = (SELECT MIN(id_terminal) FROM TERMINALES);
                """);

            migrationBuilder.AlterColumn<int>(
                name: "id_terminal",
                table: "TURNOS_CAJA",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // Con los datos ya completos se crean los índices únicos filtrados. Antes de esta migración solo podía haber
            // un turno abierto en toda la base, así que el relleno no puede violarlos.
            migrationBuilder.CreateIndex(
                name: "UX_TURNOS_CAJA_AbiertoPorTerminal",
                table: "TURNOS_CAJA",
                column: "id_terminal",
                unique: true,
                filter: "[estado] = 'Abierto' AND [deleted_at] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_TURNOS_CAJA_AbiertoPorUsuario",
                table: "TURNOS_CAJA",
                column: "id_usuario",
                unique: true,
                filter: "[estado] = 'Abierto' AND [deleted_at] IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_TURNOS_CAJA_TERMINALES_id_terminal",
                table: "TURNOS_CAJA",
                column: "id_terminal",
                principalTable: "TERMINALES",
                principalColumn: "id_terminal",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TURNOS_CAJA_TERMINALES_id_terminal",
                table: "TURNOS_CAJA");

            migrationBuilder.DropTable(
                name: "TERMINALES");

            migrationBuilder.DropIndex(
                name: "UX_TURNOS_CAJA_AbiertoPorTerminal",
                table: "TURNOS_CAJA");

            migrationBuilder.DropIndex(
                name: "UX_TURNOS_CAJA_AbiertoPorUsuario",
                table: "TURNOS_CAJA");

            migrationBuilder.DropColumn(
                name: "id_terminal",
                table: "TURNOS_CAJA");

            migrationBuilder.CreateIndex(
                name: "IX_TURNOS_CAJA_id_usuario",
                table: "TURNOS_CAJA",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "IX_TURNOS_CAJA_TurnoAbierto",
                table: "TURNOS_CAJA",
                column: "estado",
                unique: true,
                filter: "[estado] = 'Abierto' AND [deleted_at] IS NULL");
        }
    }
}
