using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTurnoCajaAndMovimientosConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TURNOS_CAJA_TurnoAbierto",
                table: "TURNOS_CAJA",
                column: "estado",
                unique: true,
                filter: "[estado] = 'Abierto' AND [deleted_at] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TURNOS_CAJA_TurnoAbierto",
                table: "TURNOS_CAJA");
        }
    }
}
