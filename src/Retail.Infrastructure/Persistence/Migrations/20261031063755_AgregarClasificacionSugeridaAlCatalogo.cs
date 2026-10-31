using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarClasificacionSugeridaAlCatalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "clasifica_catalogo",
                table: "PROVEEDORES",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "fecha_clasificacion",
                table: "CATALOGOS_PROVEEDORES",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "id_categoria_sugerida",
                table: "CATALOGOS_PROVEEDORES",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "id_marca_sugerida",
                table: "CATALOGOS_PROVEEDORES",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CATALOGOS_PROVEEDORES_id_categoria_sugerida",
                table: "CATALOGOS_PROVEEDORES",
                column: "id_categoria_sugerida");

            migrationBuilder.CreateIndex(
                name: "IX_CATALOGOS_PROVEEDORES_id_marca_sugerida",
                table: "CATALOGOS_PROVEEDORES",
                column: "id_marca_sugerida");

            migrationBuilder.CreateIndex(
                name: "IX_CATALOGOS_PROVEEDORES_id_proveedor_id_categoria_sugerida",
                table: "CATALOGOS_PROVEEDORES",
                columns: new[] { "id_proveedor", "id_categoria_sugerida" },
                filter: "[deleted_at] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CATALOGOS_PROVEEDORES_id_proveedor_id_marca_sugerida",
                table: "CATALOGOS_PROVEEDORES",
                columns: new[] { "id_proveedor", "id_marca_sugerida" },
                filter: "[deleted_at] IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_CATALOGOS_PROVEEDORES_CATEGORIAS_id_categoria_sugerida",
                table: "CATALOGOS_PROVEEDORES",
                column: "id_categoria_sugerida",
                principalTable: "CATEGORIAS",
                principalColumn: "id_categoria",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CATALOGOS_PROVEEDORES_MARCAS_id_marca_sugerida",
                table: "CATALOGOS_PROVEEDORES",
                column: "id_marca_sugerida",
                principalTable: "MARCAS",
                principalColumn: "id_marca",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CATALOGOS_PROVEEDORES_CATEGORIAS_id_categoria_sugerida",
                table: "CATALOGOS_PROVEEDORES");

            migrationBuilder.DropForeignKey(
                name: "FK_CATALOGOS_PROVEEDORES_MARCAS_id_marca_sugerida",
                table: "CATALOGOS_PROVEEDORES");

            migrationBuilder.DropIndex(
                name: "IX_CATALOGOS_PROVEEDORES_id_categoria_sugerida",
                table: "CATALOGOS_PROVEEDORES");

            migrationBuilder.DropIndex(
                name: "IX_CATALOGOS_PROVEEDORES_id_marca_sugerida",
                table: "CATALOGOS_PROVEEDORES");

            migrationBuilder.DropIndex(
                name: "IX_CATALOGOS_PROVEEDORES_id_proveedor_id_categoria_sugerida",
                table: "CATALOGOS_PROVEEDORES");

            migrationBuilder.DropIndex(
                name: "IX_CATALOGOS_PROVEEDORES_id_proveedor_id_marca_sugerida",
                table: "CATALOGOS_PROVEEDORES");

            migrationBuilder.DropColumn(
                name: "clasifica_catalogo",
                table: "PROVEEDORES");

            migrationBuilder.DropColumn(
                name: "fecha_clasificacion",
                table: "CATALOGOS_PROVEEDORES");

            migrationBuilder.DropColumn(
                name: "id_categoria_sugerida",
                table: "CATALOGOS_PROVEEDORES");

            migrationBuilder.DropColumn(
                name: "id_marca_sugerida",
                table: "CATALOGOS_PROVEEDORES");
        }
    }
}
