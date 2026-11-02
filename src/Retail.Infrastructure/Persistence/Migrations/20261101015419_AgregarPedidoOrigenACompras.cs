using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPedidoOrigenACompras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "id_pedido_origen",
                table: "COMPRAS",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_COMPRAS_PedidoOrigen",
                table: "COMPRAS",
                column: "id_pedido_origen",
                unique: true,
                filter: "[id_pedido_origen] IS NOT NULL AND [deleted_at] IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_COMPRAS_PEDIDOS_id_pedido_origen",
                table: "COMPRAS",
                column: "id_pedido_origen",
                principalTable: "PEDIDOS",
                principalColumn: "id_pedido",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_COMPRAS_PEDIDOS_id_pedido_origen",
                table: "COMPRAS");

            migrationBuilder.DropIndex(
                name: "UX_COMPRAS_PedidoOrigen",
                table: "COMPRAS");

            migrationBuilder.DropColumn(
                name: "id_pedido_origen",
                table: "COMPRAS");
        }
    }
}
