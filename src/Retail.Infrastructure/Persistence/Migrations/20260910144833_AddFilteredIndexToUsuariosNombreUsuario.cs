using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFilteredIndexToUsuariosNombreUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_USUARIOS_nombre_usuario",
                table: "USUARIOS");

            migrationBuilder.CreateIndex(
                name: "IX_USUARIOS_nombre_usuario",
                table: "USUARIOS",
                column: "nombre_usuario",
                unique: true,
                filter: "[deleted_at] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_USUARIOS_nombre_usuario",
                table: "USUARIOS");

            migrationBuilder.CreateIndex(
                name: "IX_USUARIOS_nombre_usuario",
                table: "USUARIOS",
                column: "nombre_usuario",
                unique: true);
        }
    }
}
