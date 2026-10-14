using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarRegistrosRestauracion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "REGISTROS_RESTAURACION",
                columns: table => new
                {
                    id_registro_restauracion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    referencia = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    origen = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    id_usuario = table.Column<int>(type: "int", nullable: false),
                    nombre_usuario = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    referencia_copia = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ruta_archivo = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    migracion_copia = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    referencia_copia_previa = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    base_anterior = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REGISTROS_RESTAURACION", x => x.id_registro_restauracion);
                    table.CheckConstraint("CK_REGISTROS_RESTAURACION_IdUsuario", "[id_usuario] > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_REGISTROS_RESTAURACION_fecha",
                table: "REGISTROS_RESTAURACION",
                column: "fecha",
                descending: Array.Empty<bool>());

            migrationBuilder.CreateIndex(
                name: "IX_REGISTROS_RESTAURACION_referencia",
                table: "REGISTROS_RESTAURACION",
                column: "referencia",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "REGISTROS_RESTAURACION");
        }
    }
}
