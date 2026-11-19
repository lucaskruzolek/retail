using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConcurrenciaCajaYClientes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "cantidad_ventas",
                table: "TURNOS_CAJA",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Los turnos anteriores a esta migración reciben el contador en 0 por defecto, y los reportes de caja
            // mostrarían "0 ventas" para turnos que sí vendieron. Por eso se recalcula con las ventas vigentes de
            // cada turno. Debe ejecutarse antes de agregar el CHECK (cantidad_ventas >= 0).
            migrationBuilder.Sql(
                """
                UPDATE t
                SET cantidad_ventas = c.cantidad
                FROM TURNOS_CAJA t
                INNER JOIN (
                    SELECT id_turno, COUNT(*) AS cantidad
                    FROM VENTAS
                    WHERE deleted_at IS NULL
                    GROUP BY id_turno) c ON c.id_turno = t.id_turno;
                """);

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "TURNOS_CAJA",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "CLIENTES",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TURNOS_CAJA_CantidadVentas",
                table: "TURNOS_CAJA",
                sql: "[cantidad_ventas] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TURNOS_CAJA_CantidadVentas",
                table: "TURNOS_CAJA");

            migrationBuilder.DropColumn(
                name: "cantidad_ventas",
                table: "TURNOS_CAJA");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "TURNOS_CAJA");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "CLIENTES");
        }
    }
}
