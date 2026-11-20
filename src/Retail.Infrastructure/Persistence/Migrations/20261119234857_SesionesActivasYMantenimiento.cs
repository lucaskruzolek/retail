using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Sesión única por usuario (MT-3) y aviso de mantenimiento (MT-12), Track 6.7, Paquete 4.
    /// </summary>
    /// <remarks>
    /// Estas dos tablas están a propósito FUERA del modelo de EF Core: no hay entidad, ni DbSet, ni configuración, y por
    /// eso el snapshot no cambia y el código generado estaba vacío. Son estado técnico efímero (quién está conectado
    /// ahora y si hay un mantenimiento avisado), no registros comerciales: sus filas se borran físicamente, excepción
    /// documentada a la Ley 3 (borrado lógico). El historial de quién ingresó y desde dónde ya vive en
    /// AUDITORIA_ACCESOS. Se accede a ellas con SQL explícito desde SesionesActivasSqlServer.
    /// </remarks>
    public partial class SesionesActivasYMantenimiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Una fila por usuario con sesión abierta. La clave primaria (id_usuario) y el índice único de terminal son
            // la garantía final de MT-3: aunque dos logins lleguen a la vez, la base rechaza al segundo.
            migrationBuilder.CreateTable(
                name: "SESIONES_ACTIVAS",
                columns: table => new
                {
                    id_usuario = table.Column<int>(type: "int", nullable: false),
                    id_terminal = table.Column<int>(type: "int", nullable: false),
                    inicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ultimo_latido = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SESIONES_ACTIVAS", x => x.id_usuario);
                    table.ForeignKey(
                        name: "FK_SESIONES_ACTIVAS_USUARIOS_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "USUARIOS",
                        principalColumn: "id_usuario");
                    table.ForeignKey(
                        name: "FK_SESIONES_ACTIVAS_TERMINALES_id_terminal",
                        column: x => x.id_terminal,
                        principalTable: "TERMINALES",
                        principalColumn: "id_terminal");
                });

            // Una sola sesión por terminal: la PC no puede alojar a dos usuarios a la vez.
            migrationBuilder.CreateIndex(
                name: "UX_SESIONES_ACTIVAS_id_terminal",
                table: "SESIONES_ACTIVAS",
                column: "id_terminal",
                unique: true);

            // Tabla de una única fila (id fijo 1, como CONFIGURACION_COMERCIAL). "Sin mantenimiento" es
            // programado_para IS NULL. Una restauración reemplaza toda la base, así que el aviso desaparece junto con la
            // base anterior: es lo buscado.
            migrationBuilder.CreateTable(
                name: "MANTENIMIENTO_PROGRAMADO",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    programado_para = table.Column<DateTime>(type: "datetime2", nullable: true),
                    motivo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    id_usuario_programo = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MANTENIMIENTO_PROGRAMADO", x => x.id);
                    table.CheckConstraint("CK_MANTENIMIENTO_PROGRAMADO_FilaUnica", "[id] = 1");
                    table.ForeignKey(
                        name: "FK_MANTENIMIENTO_PROGRAMADO_USUARIOS_id_usuario_programo",
                        column: x => x.id_usuario_programo,
                        principalTable: "USUARIOS",
                        principalColumn: "id_usuario");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MANTENIMIENTO_PROGRAMADO_id_usuario_programo",
                table: "MANTENIMIENTO_PROGRAMADO",
                column: "id_usuario_programo");

            migrationBuilder.Sql("INSERT INTO MANTENIMIENTO_PROGRAMADO (id) VALUES (1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SESIONES_ACTIVAS");
            migrationBuilder.DropTable(name: "MANTENIMIENTO_PROGRAMADO");
        }
    }
}
