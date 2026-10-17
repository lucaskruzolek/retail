using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Crea RUBROS, cuelga cada categoría de un rubro (CATEGORIAS.id_rubro obligatorio con FK) y hace únicos entre los
    /// activos los nombres de rubros, categorías y marcas bajo la colación Modern_Spanish_CI_AI (Track 6.5).
    /// </summary>
    /// <remarks>
    /// Antes del DDL hay dos prechequeos que abortan con THROW y un mensaje claro (D-24), igual que el precedente
    /// NormalizarCuitProveedoresYCheck cuida el CHECK de CUIT. Sin ellos el error sería un fallo críptico de FK o de
    /// índice a mitad de la migración:
    /// 1) CATEGORIAS con filas (error 50001): la columna id_rubro nace obligatoria y no hay un rubro al que colgar
    /// categorías existentes; D-16 descarta la migración de datos y manda recrear la base de desarrollo.
    /// 2) MARCAS con nombres activos duplicados bajo CI_AI (error 50002): el índice único nuevo no podría crearse.
    /// El mensaje lista los nombres para que una persona los renombre o los dé de baja y reintente.
    /// Los prechequeos no tienen Down: son lecturas, no cambian el esquema.
    /// </remarks>
    public partial class AgregarRubrosYUnicidadClasificacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM CATEGORIAS)
                    THROW 50001, N'La tabla CATEGORIAS tiene datos y la migración agrega una columna id_rubro obligatoria sin migración de datos (Track 6.5, D-16). Recree la base de desarrollo: borre RetailDb y ejecute Retail.Cli migrar, inicializar y datos-demo.', 1;
                """);

            migrationBuilder.Sql(
                """
                DECLARE @duplicadas NVARCHAR(MAX);
                DECLARE @mensaje NVARCHAR(2048);

                SELECT @duplicadas = STRING_AGG(CONVERT(NVARCHAR(MAX), N'''' + nombre_marca + N''''), N', ') WITHIN GROUP (ORDER BY nombre_marca)
                FROM MARCAS
                WHERE deleted_at IS NULL
                  AND nombre_marca COLLATE Modern_Spanish_CI_AI IN (
                      SELECT nombre_marca COLLATE Modern_Spanish_CI_AI
                      FROM MARCAS
                      WHERE deleted_at IS NULL
                      GROUP BY nombre_marca COLLATE Modern_Spanish_CI_AI
                      HAVING COUNT(*) > 1);

                IF @duplicadas IS NOT NULL
                BEGIN
                    SET @mensaje = LEFT(N'La tabla MARCAS tiene nombres activos duplicados sin distinguir mayúsculas ni acentos (Track 6.5, D-24): ' + @duplicadas + N'. Renombre o dé de baja los duplicados antes de migrar.', 2048);
                    THROW 50002, @mensaje, 1;
                END
                """);

            migrationBuilder.AlterColumn<string>(
                name: "nombre_marca",
                table: "MARCAS",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                collation: "Modern_Spanish_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "nombre_categoria",
                table: "CATEGORIAS",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                collation: "Modern_Spanish_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<int>(
                name: "id_rubro",
                table: "CATEGORIAS",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "RUBROS",
                columns: table => new
                {
                    id_rubro = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    nombre_rubro = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Modern_Spanish_CI_AI"),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RUBROS", x => x.id_rubro);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MARCAS_nombre_marca",
                table: "MARCAS",
                column: "nombre_marca",
                unique: true,
                filter: "[deleted_at] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CATEGORIAS_id_rubro",
                table: "CATEGORIAS",
                column: "id_rubro");

            migrationBuilder.CreateIndex(
                name: "IX_CATEGORIAS_nombre_categoria",
                table: "CATEGORIAS",
                column: "nombre_categoria",
                unique: true,
                filter: "[deleted_at] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RUBROS_nombre_rubro",
                table: "RUBROS",
                column: "nombre_rubro",
                unique: true,
                filter: "[deleted_at] IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_CATEGORIAS_RUBROS_id_rubro",
                table: "CATEGORIAS",
                column: "id_rubro",
                principalTable: "RUBROS",
                principalColumn: "id_rubro",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CATEGORIAS_RUBROS_id_rubro",
                table: "CATEGORIAS");

            migrationBuilder.DropTable(
                name: "RUBROS");

            migrationBuilder.DropIndex(
                name: "IX_MARCAS_nombre_marca",
                table: "MARCAS");

            migrationBuilder.DropIndex(
                name: "IX_CATEGORIAS_id_rubro",
                table: "CATEGORIAS");

            migrationBuilder.DropIndex(
                name: "IX_CATEGORIAS_nombre_categoria",
                table: "CATEGORIAS");

            migrationBuilder.DropColumn(
                name: "id_rubro",
                table: "CATEGORIAS");

            migrationBuilder.AlterColumn<string>(
                name: "nombre_marca",
                table: "MARCAS",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldCollation: "Modern_Spanish_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "nombre_categoria",
                table: "CATEGORIAS",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldCollation: "Modern_Spanish_CI_AI");
        }
    }
}
