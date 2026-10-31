using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Fase 3b del Track 6.6: agrega a CATALOGOS_PROVEEDORES el rubro, la categoría y la marca tal como los escribe el
    /// proveedor (M-12, columnas opcionales que las filas existentes dejan en NULL) y crea EQUIVALENCIAS_CLASIFICACION,
    /// las reglas de traducción de cada proveedor a la taxonomía del comercio (M-13, M-31, M-32), con un CHECK que ata
    /// el destino al tipo y un índice único filtrado por proveedor y valor.
    /// </summary>
    public partial class AgregarEquivalenciasYTextosProveedor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "categoria_proveedor",
                table: "CATALOGOS_PROVEEDORES",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "marca_proveedor",
                table: "CATALOGOS_PROVEEDORES",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rubro_proveedor",
                table: "CATALOGOS_PROVEEDORES",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EQUIVALENCIAS_CLASIFICACION",
                columns: table => new
                {
                    id_equivalencia = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_proveedor = table.Column<int>(type: "int", nullable: false),
                    tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    texto_rubro = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    texto = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    id_categoria = table.Column<int>(type: "int", nullable: true),
                    id_marca = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EQUIVALENCIAS_CLASIFICACION", x => x.id_equivalencia);
                    table.CheckConstraint("CK_EQUIVALENCIAS_CLASIFICACION_Destino", "([tipo] = 'Categoria' AND [id_marca] IS NULL) OR ([tipo] = 'Marca' AND [id_categoria] IS NULL AND [texto_rubro] IS NULL)");
                    table.ForeignKey(
                        name: "FK_EQUIVALENCIAS_CLASIFICACION_CATEGORIAS_id_categoria",
                        column: x => x.id_categoria,
                        principalTable: "CATEGORIAS",
                        principalColumn: "id_categoria",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EQUIVALENCIAS_CLASIFICACION_MARCAS_id_marca",
                        column: x => x.id_marca,
                        principalTable: "MARCAS",
                        principalColumn: "id_marca",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EQUIVALENCIAS_CLASIFICACION_PROVEEDORES_id_proveedor",
                        column: x => x.id_proveedor,
                        principalTable: "PROVEEDORES",
                        principalColumn: "id_proveedor",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EQUIVALENCIAS_CLASIFICACION_id_categoria",
                table: "EQUIVALENCIAS_CLASIFICACION",
                column: "id_categoria");

            migrationBuilder.CreateIndex(
                name: "IX_EQUIVALENCIAS_CLASIFICACION_id_marca",
                table: "EQUIVALENCIAS_CLASIFICACION",
                column: "id_marca");

            migrationBuilder.CreateIndex(
                name: "IX_EQUIVALENCIAS_CLASIFICACION_id_proveedor_tipo_texto_rubro_texto",
                table: "EQUIVALENCIAS_CLASIFICACION",
                columns: new[] { "id_proveedor", "tipo", "texto_rubro", "texto" },
                unique: true,
                filter: "[deleted_at] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EQUIVALENCIAS_CLASIFICACION");

            migrationBuilder.DropColumn(
                name: "categoria_proveedor",
                table: "CATALOGOS_PROVEEDORES");

            migrationBuilder.DropColumn(
                name: "marca_proveedor",
                table: "CATALOGOS_PROVEEDORES");

            migrationBuilder.DropColumn(
                name: "rubro_proveedor",
                table: "CATALOGOS_PROVEEDORES");
        }
    }
}
