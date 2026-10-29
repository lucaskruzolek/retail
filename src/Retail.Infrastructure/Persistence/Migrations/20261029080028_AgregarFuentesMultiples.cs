using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarFuentesMultiples : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "id_articulo",
                table: "CATALOGOS_PROVEEDORES",
                type: "int",
                nullable: true);

            // Todo vínculo vigente pasa a ser la fuente principal de su artículo (Track 4.3, L3a): el ítem queda
            // registrado también como fuente, así se cumple el invariante de A-16 desde el primer momento. Los
            // artículos dados de baja no se migran: sus ítems quedan libres para volver a incorporarse (J3). El índice
            // único filtrado de ARTICULOS garantiza a lo sumo un artículo activo por ítem, así que el JOIN no repite.
            migrationBuilder.Sql(
                """
                UPDATE c
                SET c.id_articulo = a.id_articulo
                FROM CATALOGOS_PROVEEDORES c
                INNER JOIN ARTICULOS a ON a.id_catalogo_proveedor = c.id_catalogo
                WHERE a.deleted_at IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CATALOGOS_PROVEEDORES_id_articulo",
                table: "CATALOGOS_PROVEEDORES",
                column: "id_articulo");

            migrationBuilder.AddForeignKey(
                name: "FK_CATALOGOS_PROVEEDORES_ARTICULOS_id_articulo",
                table: "CATALOGOS_PROVEEDORES",
                column: "id_articulo",
                principalTable: "ARTICULOS",
                principalColumn: "id_articulo",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CATALOGOS_PROVEEDORES_ARTICULOS_id_articulo",
                table: "CATALOGOS_PROVEEDORES");

            migrationBuilder.DropIndex(
                name: "IX_CATALOGOS_PROVEEDORES_id_articulo",
                table: "CATALOGOS_PROVEEDORES");

            migrationBuilder.DropColumn(
                name: "id_articulo",
                table: "CATALOGOS_PROVEEDORES");
        }
    }
}
