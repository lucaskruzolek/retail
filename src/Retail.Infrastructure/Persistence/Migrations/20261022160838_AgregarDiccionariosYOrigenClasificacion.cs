using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Agrega los diccionarios del motor de catalogación (Track 6.6): TERMINOS_CLASIFICACION (raíz propia, M-27),
    /// ALIAS_MARCAS y PREFIJOS_EAN_MARCAS (hijos de MARCAS), con unicidad entre los activos; y el origen de la
    /// clasificación en ARTICULOS (sección 8.2, M-30), custodiado por dos CHECK.
    /// </summary>
    /// <remarks>
    /// Antes de los CHECK se completa el origen de los artículos que ya tienen categoría o marca con 'Manual': las
    /// asignó una persona, desde el formulario o en la incorporación por lote, sin sugerencia del sistema. Sin ese
    /// relleno los CHECK no podrían crearse en una base con datos. Incluye los artículos dados de baja, porque un CHECK
    /// vale para todas las filas. El relleno no tiene Down: al bajar se eliminan las columnas.
    /// </remarks>
    public partial class AgregarDiccionariosYOrigenClasificacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "origen_categoria",
                table: "ARTICULOS",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "origen_marca",
                table: "ARTICULOS",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE ARTICULOS SET origen_categoria = N'Manual' WHERE id_categoria IS NOT NULL;
                UPDATE ARTICULOS SET origen_marca = N'Manual' WHERE id_marca IS NOT NULL;
                """);

            migrationBuilder.CreateTable(
                name: "ALIAS_MARCAS",
                columns: table => new
                {
                    id_alias = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_marca = table.Column<int>(type: "int", nullable: false),
                    alias = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ALIAS_MARCAS", x => x.id_alias);
                    table.ForeignKey(
                        name: "FK_ALIAS_MARCAS_MARCAS_id_marca",
                        column: x => x.id_marca,
                        principalTable: "MARCAS",
                        principalColumn: "id_marca",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PREFIJOS_EAN_MARCAS",
                columns: table => new
                {
                    id_prefijo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_marca = table.Column<int>(type: "int", nullable: false),
                    prefijo = table.Column<string>(type: "varchar(7)", unicode: false, maxLength: 7, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PREFIJOS_EAN_MARCAS", x => x.id_prefijo);
                    table.CheckConstraint("CK_PREFIJOS_EAN_MARCAS_Prefijo", "LEN([prefijo]) = 7 AND [prefijo] NOT LIKE '%[^0-9]%' AND [prefijo] NOT LIKE '2%'");
                    table.ForeignKey(
                        name: "FK_PREFIJOS_EAN_MARCAS_MARCAS_id_marca",
                        column: x => x.id_marca,
                        principalTable: "MARCAS",
                        principalColumn: "id_marca",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TERMINOS_CLASIFICACION",
                columns: table => new
                {
                    id_termino = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    termino = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    id_categoria = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TERMINOS_CLASIFICACION", x => x.id_termino);
                    table.ForeignKey(
                        name: "FK_TERMINOS_CLASIFICACION_CATEGORIAS_id_categoria",
                        column: x => x.id_categoria,
                        principalTable: "CATEGORIAS",
                        principalColumn: "id_categoria",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ARTICULOS_OrigenCategoria",
                table: "ARTICULOS",
                sql: "([id_categoria] IS NULL AND [origen_categoria] IS NULL) OR ([id_categoria] IS NOT NULL AND [origen_categoria] IS NOT NULL AND [origen_categoria] IN ('Manual', 'SugerenciaAceptada', 'SugerenciaCorregida'))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ARTICULOS_OrigenMarca",
                table: "ARTICULOS",
                sql: "([id_marca] IS NULL AND [origen_marca] IS NULL) OR ([origen_marca] IS NOT NULL AND [origen_marca] IN ('Manual', 'SugerenciaAceptada', 'SugerenciaCorregida'))");

            migrationBuilder.CreateIndex(
                name: "IX_ALIAS_MARCAS_alias",
                table: "ALIAS_MARCAS",
                column: "alias",
                unique: true,
                filter: "[deleted_at] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ALIAS_MARCAS_id_marca",
                table: "ALIAS_MARCAS",
                column: "id_marca");

            migrationBuilder.CreateIndex(
                name: "IX_PREFIJOS_EAN_MARCAS_id_marca",
                table: "PREFIJOS_EAN_MARCAS",
                column: "id_marca");

            migrationBuilder.CreateIndex(
                name: "IX_PREFIJOS_EAN_MARCAS_prefijo",
                table: "PREFIJOS_EAN_MARCAS",
                column: "prefijo",
                unique: true,
                filter: "[deleted_at] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TERMINOS_CLASIFICACION_id_categoria",
                table: "TERMINOS_CLASIFICACION",
                column: "id_categoria");

            migrationBuilder.CreateIndex(
                name: "IX_TERMINOS_CLASIFICACION_termino",
                table: "TERMINOS_CLASIFICACION",
                column: "termino",
                unique: true,
                filter: "[deleted_at] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ALIAS_MARCAS");

            migrationBuilder.DropTable(
                name: "PREFIJOS_EAN_MARCAS");

            migrationBuilder.DropTable(
                name: "TERMINOS_CLASIFICACION");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ARTICULOS_OrigenCategoria",
                table: "ARTICULOS");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ARTICULOS_OrigenMarca",
                table: "ARTICULOS");

            migrationBuilder.DropColumn(
                name: "origen_categoria",
                table: "ARTICULOS");

            migrationBuilder.DropColumn(
                name: "origen_marca",
                table: "ARTICULOS");
        }
    }
}
