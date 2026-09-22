using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCodigoBarrasToCatalogoProveedorAndFilteredIndexToProveedoresCuit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PROVEEDORES_cuit",
                table: "PROVEEDORES");

            migrationBuilder.DropIndex(
                name: "IX_CATALOGOS_PROVEEDORES_id_proveedor",
                table: "CATALOGOS_PROVEEDORES");

            migrationBuilder.AddColumn<string>(
                name: "codigo_barras",
                table: "CATALOGOS_PROVEEDORES",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PROVEEDORES_cuit",
                table: "PROVEEDORES",
                column: "cuit",
                unique: true,
                filter: "[deleted_at] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CATALOGOS_PROVEEDORES_id_proveedor_codigo_proveedor",
                table: "CATALOGOS_PROVEEDORES",
                columns: new[] { "id_proveedor", "codigo_proveedor" },
                unique: true,
                filter: "[deleted_at] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PROVEEDORES_cuit",
                table: "PROVEEDORES");

            migrationBuilder.DropIndex(
                name: "IX_CATALOGOS_PROVEEDORES_id_proveedor_codigo_proveedor",
                table: "CATALOGOS_PROVEEDORES");

            migrationBuilder.DropColumn(
                name: "codigo_barras",
                table: "CATALOGOS_PROVEEDORES");

            migrationBuilder.CreateIndex(
                name: "IX_PROVEEDORES_cuit",
                table: "PROVEEDORES",
                column: "cuit",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CATALOGOS_PROVEEDORES_id_proveedor",
                table: "CATALOGOS_PROVEEDORES",
                column: "id_proveedor");
        }
    }
}
