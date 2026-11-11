using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarConfiguracionComercial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CONFIGURACION_COMERCIAL",
                columns: table => new
                {
                    id_configuracion_comercial = table.Column<int>(type: "int", nullable: false),
                    redondeo_precio = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CONFIGURACION_COMERCIAL", x => x.id_configuracion_comercial);
                    table.CheckConstraint("CK_CONFIGURACION_COMERCIAL_RedondeoPrecio", "[redondeo_precio] IN ('EnteroMasCercano', 'MultiploDe5Superior', 'MultiploDe5Inferior', 'MultiploDe10Superior', 'MultiploDe10Inferior', 'MultiploDe100Superior', 'MultiploDe100Inferior')");
                });

            migrationBuilder.InsertData(
                table: "CONFIGURACION_COMERCIAL",
                columns: new[] { "id_configuracion_comercial", "created_at", "deleted_at", "redondeo_precio" },
                values: new object[] { 1, new DateTime(2026, 11, 11, 0, 0, 0, 0, DateTimeKind.Utc), null, "EnteroMasCercano" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CONFIGURACION_COMERCIAL");
        }
    }
}
