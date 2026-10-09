using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Separa USUARIOS.nombre_completo en nombre y apellido (1FN) y deja los nombres de usuario en minúsculas,
    /// la forma canónica que exige el agregado Usuario.
    /// </summary>
    /// <remarks>
    /// El scaffold de EF borraba nombre_completo antes de crear las columnas nuevas, dejando las filas existentes
    /// con nombre y apellido vacíos. Se reordenó: agregar, completar desde la columna vieja y recién después borrarla.
    /// Los datos existentes son de prueba, así que se completan cortando en el último espacio ("Administrador
    /// General" → "Administrador" / "General"); si no hay espacio, el valor entero se usa en las dos columnas.
    /// </remarks>
    public partial class SepararNombreApellidoUsuarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "apellido",
                table: "USUARIOS",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                collation: "Modern_Spanish_CI_AI");

            migrationBuilder.AddColumn<string>(
                name: "nombre",
                table: "USUARIOS",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                collation: "Modern_Spanish_CI_AI");

            migrationBuilder.Sql(
                """
                UPDATE USUARIOS
                SET nombre = LEFT(LTRIM(RTRIM(
                        CASE WHEN CHARINDEX(' ', LTRIM(RTRIM(nombre_completo))) = 0
                             THEN nombre_completo
                             ELSE LEFT(LTRIM(RTRIM(nombre_completo)),
                                  LEN(LTRIM(RTRIM(nombre_completo))) - CHARINDEX(' ', REVERSE(LTRIM(RTRIM(nombre_completo)))))
                        END)), 50),
                    apellido = LEFT(LTRIM(RTRIM(
                        CASE WHEN CHARINDEX(' ', LTRIM(RTRIM(nombre_completo))) = 0
                             THEN nombre_completo
                             ELSE RIGHT(LTRIM(RTRIM(nombre_completo)),
                                  CHARINDEX(' ', REVERSE(LTRIM(RTRIM(nombre_completo)))) - 1)
                        END)), 50),
                    nombre_usuario = LOWER(nombre_usuario);
                """);

            migrationBuilder.DropColumn(
                name: "nombre_completo",
                table: "USUARIOS");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "nombre_completo",
                table: "USUARIOS",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE USUARIOS SET nombre_completo = nombre + ' ' + apellido;");

            migrationBuilder.DropColumn(
                name: "apellido",
                table: "USUARIOS");

            migrationBuilder.DropColumn(
                name: "nombre",
                table: "USUARIOS");
        }
    }
}
