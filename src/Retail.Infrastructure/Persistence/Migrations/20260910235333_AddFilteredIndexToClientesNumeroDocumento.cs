using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFilteredIndexToClientesNumeroDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CLIENTES_numero_documento",
                table: "CLIENTES");

            migrationBuilder.CreateIndex(
                name: "IX_CLIENTES_numero_documento",
                table: "CLIENTES",
                column: "numero_documento",
                unique: true,
                filter: "[deleted_at] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CLIENTES_numero_documento",
                table: "CLIENTES");

            migrationBuilder.CreateIndex(
                name: "IX_CLIENTES_numero_documento",
                table: "CLIENTES",
                column: "numero_documento",
                unique: true);
        }
    }
}
