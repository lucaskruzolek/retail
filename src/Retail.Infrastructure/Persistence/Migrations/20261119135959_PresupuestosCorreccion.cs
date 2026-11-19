using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PresupuestosCorreccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "id_presupuesto_reemplazado",
                table: "PRESUPUESTOS",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_PRESUPUESTOS_id_presupuesto_reemplazado",
                table: "PRESUPUESTOS",
                column: "id_presupuesto_reemplazado",
                unique: true,
                filter: "[id_presupuesto_reemplazado] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_PRESUPUESTOS_PRESUPUESTOS_id_presupuesto_reemplazado",
                table: "PRESUPUESTOS",
                column: "id_presupuesto_reemplazado",
                principalTable: "PRESUPUESTOS",
                principalColumn: "id_presupuesto",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PRESUPUESTOS_PRESUPUESTOS_id_presupuesto_reemplazado",
                table: "PRESUPUESTOS");

            migrationBuilder.DropIndex(
                name: "UX_PRESUPUESTOS_id_presupuesto_reemplazado",
                table: "PRESUPUESTOS");

            migrationBuilder.DropColumn(
                name: "id_presupuesto_reemplazado",
                table: "PRESUPUESTOS");
        }
    }
}
