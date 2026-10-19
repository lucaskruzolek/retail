using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarIndicesPaginacionCatalogos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CATALOGOS_PROVEEDORES_id_proveedor_descripcion_proveedor",
                table: "CATALOGOS_PROVEEDORES",
                columns: new[] { "id_proveedor", "descripcion_proveedor" },
                filter: "[deleted_at] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ARTICULOS_descripcion",
                table: "ARTICULOS",
                column: "descripcion",
                filter: "[deleted_at] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CATALOGOS_PROVEEDORES_id_proveedor_descripcion_proveedor",
                table: "CATALOGOS_PROVEEDORES");

            migrationBuilder.DropIndex(
                name: "IX_ARTICULOS_descripcion",
                table: "ARTICULOS");
        }
    }
}
