using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarParesClasificacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PARES_CLASIFICACION",
                columns: table => new
                {
                    id_par = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_termino = table.Column<int>(type: "int", nullable: false),
                    segunda_palabra = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    id_categoria = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PARES_CLASIFICACION", x => x.id_par);
                    table.CheckConstraint("CK_PARES_CLASIFICACION_SegundaPalabra", "LEN([segunda_palabra]) >= 2 AND CHARINDEX(' ', [segunda_palabra]) = 0");
                    table.ForeignKey(
                        name: "FK_PARES_CLASIFICACION_CATEGORIAS_id_categoria",
                        column: x => x.id_categoria,
                        principalTable: "CATEGORIAS",
                        principalColumn: "id_categoria",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PARES_CLASIFICACION_TERMINOS_CLASIFICACION_id_termino",
                        column: x => x.id_termino,
                        principalTable: "TERMINOS_CLASIFICACION",
                        principalColumn: "id_termino",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PARES_CLASIFICACION_id_categoria",
                table: "PARES_CLASIFICACION",
                column: "id_categoria");

            migrationBuilder.CreateIndex(
                name: "IX_PARES_CLASIFICACION_id_termino_segunda_palabra",
                table: "PARES_CLASIFICACION",
                columns: new[] { "id_termino", "segunda_palabra" },
                unique: true,
                filter: "[deleted_at] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PARES_CLASIFICACION");
        }
    }
}
