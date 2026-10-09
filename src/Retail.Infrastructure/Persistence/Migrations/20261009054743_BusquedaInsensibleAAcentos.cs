using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BusquedaInsensibleAAcentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "razon_social_o_nombre",
                table: "CLIENTES",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                collation: "Modern_Spanish_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "descripcion_proveedor",
                table: "CATALOGOS_PROVEEDORES",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                collation: "Modern_Spanish_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "descripcion",
                table: "ARTICULOS",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                collation: "Modern_Spanish_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "razon_social_o_nombre",
                table: "CLIENTES",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150,
                oldCollation: "Modern_Spanish_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "descripcion_proveedor",
                table: "CATALOGOS_PROVEEDORES",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldCollation: "Modern_Spanish_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "descripcion",
                table: "ARTICULOS",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldCollation: "Modern_Spanish_CI_AI");
        }
    }
}
