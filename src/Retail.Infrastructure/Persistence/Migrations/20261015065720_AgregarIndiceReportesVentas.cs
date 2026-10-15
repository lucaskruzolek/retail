using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarIndiceReportesVentas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_VENTAS_fecha_hora",
                table: "VENTAS",
                column: "fecha_hora",
                filter: "[deleted_at] IS NULL")
                .Annotation("SqlServer:Include", new[] { "subtotal", "descuento", "total", "id_usuario" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VENTAS_fecha_hora",
                table: "VENTAS");
        }
    }
}
