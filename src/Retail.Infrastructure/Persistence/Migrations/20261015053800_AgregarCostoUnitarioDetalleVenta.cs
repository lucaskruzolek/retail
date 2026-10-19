using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCostoUnitarioDetalleVenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_DETALLE_VENTAS_Valores",
                table: "DETALLE_VENTAS");

            migrationBuilder.AddColumn<decimal>(
                name: "costo_unitario",
                table: "DETALLE_VENTAS",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "CK_DETALLE_VENTAS_Valores",
                table: "DETALLE_VENTAS",
                sql: "[cantidad] > 0 AND [precio_unitario] >= 0 AND [costo_unitario] >= 0 AND [subtotal_item] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_DETALLE_VENTAS_Valores",
                table: "DETALLE_VENTAS");

            migrationBuilder.DropColumn(
                name: "costo_unitario",
                table: "DETALLE_VENTAS");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DETALLE_VENTAS_Valores",
                table: "DETALLE_VENTAS",
                sql: "[cantidad] > 0 AND [precio_unitario] >= 0 AND [subtotal_item] >= 0");
        }
    }
}
