using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Crea REGISTROS_BACKUP, el historial de copias de seguridad (RF-22, Track 6.3 Fase 3).
    /// </summary>
    /// <remarks>
    /// Sin clave foránea a USUARIOS: el operador se guarda como foto para poder reinsertar el historial en una
    /// base restaurada (R-1). El índice filtrado IX_REGISTROS_BACKUP_EnCurso impide dos copias simultáneas (R-12) y
    /// los CHECK replican las invariantes del agregado RegistroBackup.
    /// </remarks>
    public partial class AgregarRegistrosBackup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "REGISTROS_BACKUP",
                columns: table => new
                {
                    id_registro_backup = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    referencia = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    id_usuario = table.Column<int>(type: "int", nullable: true),
                    nombre_usuario = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    fecha_inicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    fecha_fin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ruta_archivo = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    tamano_bytes = table.Column<long>(type: "bigint", nullable: true),
                    hash_sha256 = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: true),
                    ultima_migracion = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    version_app = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    mensaje_error = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REGISTROS_BACKUP", x => x.id_registro_backup);
                    table.CheckConstraint("CK_REGISTROS_BACKUP_Operador", "([tipo] IN ('Manual', 'PreRestauracion') AND [id_usuario] IS NOT NULL AND [nombre_usuario] IS NOT NULL) OR ([tipo] IN ('Automatico', 'PreMigracion') AND [id_usuario] IS NULL AND [nombre_usuario] IS NULL)");
                    table.CheckConstraint("CK_REGISTROS_BACKUP_Tamano", "[tamano_bytes] IS NULL OR [tamano_bytes] > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_REGISTROS_BACKUP_EnCurso",
                table: "REGISTROS_BACKUP",
                column: "estado",
                unique: true,
                filter: "[estado] = 'EnCurso' AND [deleted_at] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_REGISTROS_BACKUP_fecha_inicio",
                table: "REGISTROS_BACKUP",
                column: "fecha_inicio",
                descending: Array.Empty<bool>());

            migrationBuilder.CreateIndex(
                name: "IX_REGISTROS_BACKUP_referencia",
                table: "REGISTROS_BACKUP",
                column: "referencia",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "REGISTROS_BACKUP");
        }
    }
}
