using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPedidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PEDIDOS",
                columns: table => new
                {
                    id_pedido = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_proveedor = table.Column<int>(type: "int", nullable: false),
                    id_usuario = table.Column<int>(type: "int", nullable: false),
                    estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    fecha_emision = table.Column<DateTime>(type: "datetime2", nullable: true),
                    fecha_cierre = table.Column<DateTime>(type: "datetime2", nullable: true),
                    motivo_cancelacion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PEDIDOS", x => x.id_pedido);
                    table.ForeignKey(
                        name: "FK_PEDIDOS_PROVEEDORES_id_proveedor",
                        column: x => x.id_proveedor,
                        principalTable: "PROVEEDORES",
                        principalColumn: "id_proveedor",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PEDIDOS_USUARIOS_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "USUARIOS",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DETALLE_PEDIDOS",
                columns: table => new
                {
                    id_detalle_pedido = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_pedido = table.Column<int>(type: "int", nullable: false),
                    id_articulo = table.Column<int>(type: "int", nullable: false),
                    id_catalogo_proveedor = table.Column<int>(type: "int", nullable: false),
                    codigo_proveedor = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    cantidad = table.Column<int>(type: "int", nullable: false),
                    costo_pactado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DETALLE_PEDIDOS", x => x.id_detalle_pedido);
                    table.CheckConstraint("CK_DETALLE_PEDIDOS_Cantidad", "[cantidad] >= 1");
                    table.CheckConstraint("CK_DETALLE_PEDIDOS_Costo", "[costo_pactado] > 0");
                    table.ForeignKey(
                        name: "FK_DETALLE_PEDIDOS_ARTICULOS_id_articulo",
                        column: x => x.id_articulo,
                        principalTable: "ARTICULOS",
                        principalColumn: "id_articulo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DETALLE_PEDIDOS_CATALOGOS_PROVEEDORES_id_catalogo_proveedor",
                        column: x => x.id_catalogo_proveedor,
                        principalTable: "CATALOGOS_PROVEEDORES",
                        principalColumn: "id_catalogo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DETALLE_PEDIDOS_PEDIDOS_id_pedido",
                        column: x => x.id_pedido,
                        principalTable: "PEDIDOS",
                        principalColumn: "id_pedido",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DETALLE_PEDIDOS_id_articulo",
                table: "DETALLE_PEDIDOS",
                column: "id_articulo");

            migrationBuilder.CreateIndex(
                name: "IX_DETALLE_PEDIDOS_id_catalogo_proveedor",
                table: "DETALLE_PEDIDOS",
                column: "id_catalogo_proveedor");

            migrationBuilder.CreateIndex(
                name: "UX_DETALLE_PEDIDOS_pedido_catalogo",
                table: "DETALLE_PEDIDOS",
                columns: new[] { "id_pedido", "id_catalogo_proveedor" },
                unique: true,
                filter: "[deleted_at] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PEDIDOS_id_usuario",
                table: "PEDIDOS",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "UX_PEDIDOS_borrador_por_proveedor",
                table: "PEDIDOS",
                column: "id_proveedor",
                unique: true,
                filter: "[estado] = N'Borrador' AND [deleted_at] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DETALLE_PEDIDOS");

            migrationBuilder.DropTable(
                name: "PEDIDOS");
        }
    }
}
