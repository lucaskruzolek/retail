using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPresentacionesDeVenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ARTICULOS_id_catalogo_proveedor",
                table: "ARTICULOS");

            migrationBuilder.AddColumn<int>(
                name: "id_articulo_origen",
                table: "ARTICULOS",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "unidades_por_origen",
                table: "ARTICULOS",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ARTICULOS_id_articulo_origen",
                table: "ARTICULOS",
                column: "id_articulo_origen");

            migrationBuilder.CreateIndex(
                name: "IX_ARTICULOS_id_catalogo_proveedor",
                table: "ARTICULOS",
                column: "id_catalogo_proveedor",
                unique: true,
                filter: "[id_catalogo_proveedor] IS NOT NULL AND [deleted_at] IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ARTICULOS_Presentacion",
                table: "ARTICULOS",
                sql: "([id_articulo_origen] IS NULL AND [unidades_por_origen] IS NULL) OR ([id_articulo_origen] IS NOT NULL AND [unidades_por_origen] IS NOT NULL AND [unidades_por_origen] >= 1 AND [id_catalogo_proveedor] IS NULL AND [es_servicio] = 0 AND [id_articulo_origen] <> [id_articulo])");

            migrationBuilder.AddForeignKey(
                name: "FK_ARTICULOS_ARTICULOS_id_articulo_origen",
                table: "ARTICULOS",
                column: "id_articulo_origen",
                principalTable: "ARTICULOS",
                principalColumn: "id_articulo",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ARTICULOS_ARTICULOS_id_articulo_origen",
                table: "ARTICULOS");

            migrationBuilder.DropIndex(
                name: "IX_ARTICULOS_id_articulo_origen",
                table: "ARTICULOS");

            migrationBuilder.DropIndex(
                name: "IX_ARTICULOS_id_catalogo_proveedor",
                table: "ARTICULOS");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ARTICULOS_Presentacion",
                table: "ARTICULOS");

            migrationBuilder.DropColumn(
                name: "id_articulo_origen",
                table: "ARTICULOS");

            migrationBuilder.DropColumn(
                name: "unidades_por_origen",
                table: "ARTICULOS");

            migrationBuilder.CreateIndex(
                name: "IX_ARTICULOS_id_catalogo_proveedor",
                table: "ARTICULOS",
                column: "id_catalogo_proveedor");
        }
    }
}
