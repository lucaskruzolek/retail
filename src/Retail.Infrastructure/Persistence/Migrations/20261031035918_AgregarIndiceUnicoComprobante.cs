using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarIndiceUnicoComprobante : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_COMPRAS_id_proveedor",
                table: "COMPRAS");

            migrationBuilder.CreateIndex(
                name: "UX_COMPRAS_Comprobante",
                table: "COMPRAS",
                columns: new[] { "id_proveedor", "tipo_comprobante", "numero_comprobante" },
                unique: true,
                filter: "[deleted_at] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_COMPRAS_Comprobante",
                table: "COMPRAS");

            migrationBuilder.CreateIndex(
                name: "IX_COMPRAS_id_proveedor",
                table: "COMPRAS",
                column: "id_proveedor");
        }
    }
}
