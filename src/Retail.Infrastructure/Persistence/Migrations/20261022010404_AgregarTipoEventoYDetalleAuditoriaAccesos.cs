using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTipoEventoYDetalleAuditoriaAccesos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // El DEFAULT 0 (InicioSesion) mantiene válido el INSERT de sp_RegistrarIntentoLogin, que no conoce estas columnas.
            migrationBuilder.Sql(@"
                ALTER TABLE dbo.AUDITORIA_ACCESOS ADD
                    tipo_evento TINYINT NOT NULL CONSTRAINT DF_AUDITORIA_ACCESOS_tipo_evento DEFAULT 0,
                    detalle NVARCHAR(100) NULL;
            ");

            // En otra sentencia: el CHECK referencia una columna que recién existe cuando termina la anterior.
            migrationBuilder.Sql(@"
                ALTER TABLE dbo.AUDITORIA_ACCESOS
                    ADD CONSTRAINT CK_AUDITORIA_ACCESOS_tipo_evento CHECK (tipo_evento IN (0, 1));
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE dbo.AUDITORIA_ACCESOS DROP CONSTRAINT CK_AUDITORIA_ACCESOS_tipo_evento;");
            migrationBuilder.Sql("ALTER TABLE dbo.AUDITORIA_ACCESOS DROP CONSTRAINT DF_AUDITORIA_ACCESOS_tipo_evento;");
            migrationBuilder.Sql("ALTER TABLE dbo.AUDITORIA_ACCESOS DROP COLUMN tipo_evento, detalle;");
        }
    }
}
