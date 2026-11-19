using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ListasDeArticulos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "id_lista_origen",
                table: "VENTAS",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LISTAS_ARTICULOS",
                columns: table => new
                {
                    id_lista = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Modern_Spanish_CI_AI"),
                    id_usuario_creador = table.Column<int>(type: "int", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LISTAS_ARTICULOS", x => x.id_lista);
                    table.ForeignKey(
                        name: "FK_LISTAS_ARTICULOS_USUARIOS_id_usuario_creador",
                        column: x => x.id_usuario_creador,
                        principalTable: "USUARIOS",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RENGLONES_LISTA",
                columns: table => new
                {
                    id_renglon_lista = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_lista = table.Column<int>(type: "int", nullable: false),
                    id_articulo = table.Column<int>(type: "int", nullable: false),
                    cantidad = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RENGLONES_LISTA", x => x.id_renglon_lista);
                    table.CheckConstraint("CK_RENGLONES_LISTA_Cantidad", "[cantidad] >= 1");
                    table.ForeignKey(
                        name: "FK_RENGLONES_LISTA_ARTICULOS_id_articulo",
                        column: x => x.id_articulo,
                        principalTable: "ARTICULOS",
                        principalColumn: "id_articulo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RENGLONES_LISTA_LISTAS_ARTICULOS_id_lista",
                        column: x => x.id_lista,
                        principalTable: "LISTAS_ARTICULOS",
                        principalColumn: "id_lista",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VENTAS_id_lista_origen",
                table: "VENTAS",
                column: "id_lista_origen");

            migrationBuilder.CreateIndex(
                name: "IX_LISTAS_ARTICULOS_id_usuario_creador",
                table: "LISTAS_ARTICULOS",
                column: "id_usuario_creador");

            migrationBuilder.CreateIndex(
                name: "UX_LISTAS_ARTICULOS_nombre",
                table: "LISTAS_ARTICULOS",
                column: "nombre",
                unique: true,
                filter: "[deleted_at] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RENGLONES_LISTA_id_articulo",
                table: "RENGLONES_LISTA",
                column: "id_articulo");

            migrationBuilder.CreateIndex(
                name: "UX_RENGLONES_LISTA_lista_articulo",
                table: "RENGLONES_LISTA",
                columns: new[] { "id_lista", "id_articulo" },
                unique: true,
                filter: "[deleted_at] IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_VENTAS_LISTAS_ARTICULOS_id_lista_origen",
                table: "VENTAS",
                column: "id_lista_origen",
                principalTable: "LISTAS_ARTICULOS",
                principalColumn: "id_lista",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VENTAS_LISTAS_ARTICULOS_id_lista_origen",
                table: "VENTAS");

            migrationBuilder.DropTable(
                name: "RENGLONES_LISTA");

            migrationBuilder.DropTable(
                name: "LISTAS_ARTICULOS");

            migrationBuilder.DropIndex(
                name: "IX_VENTAS_id_lista_origen",
                table: "VENTAS");

            migrationBuilder.DropColumn(
                name: "id_lista_origen",
                table: "VENTAS");
        }
    }
}
