using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFilteredIndexToArticulosCodigoBarrasSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ARTICULOS_codigo_barras",
                table: "ARTICULOS");

            migrationBuilder.CreateIndex(
                name: "IX_ARTICULOS_codigo_barras",
                table: "ARTICULOS",
                column: "codigo_barras",
                unique: true,
                filter: "[codigo_barras] IS NOT NULL AND [deleted_at] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ARTICULOS_codigo_barras",
                table: "ARTICULOS");

            migrationBuilder.CreateIndex(
                name: "IX_ARTICULOS_codigo_barras",
                table: "ARTICULOS",
                column: "codigo_barras",
                unique: true,
                filter: "[codigo_barras] IS NOT NULL");
        }
    }
}
