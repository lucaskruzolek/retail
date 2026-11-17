using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PresupuestosConversionYVigencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Salvaguarda (riesgo R-4): esta migración cambia el tipo de fecha_vencimiento (datetime2 -> date) y agrega
            // columnas NOT NULL sin migrar datos, porque el módulo de presupuestos nunca escribió: la tabla está vacía en
            // todas las bases. Si alguna tuviera filas, se frena acá con un mensaje claro en lugar de perderlas o
            // convertirlas mal. DETALLE_PRESUPUESTOS depende de PRESUPUESTOS, así que queda cubierta.
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM PRESUPUESTOS) " +
                "THROW 50000, N'No se puede migrar: la tabla PRESUPUESTOS tiene filas y esta migración no convierte datos existentes. Revise esas filas antes de continuar.', 1;");
            migrationBuilder.DropIndex(
                name: "IX_VENTAS_id_presupuesto_origen",
                table: "VENTAS");

            migrationBuilder.AlterColumn<int>(
                name: "id_cliente",
                table: "PRESUPUESTOS",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "fecha_vencimiento",
                table: "PRESUPUESTOS",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<DateTime>(
                name: "fecha_cierre",
                table: "PRESUPUESTOS",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "PRESUPUESTOS",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "titulo",
                table: "PRESUPUESTOS",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                collation: "Modern_Spanish_CI_AI");

            migrationBuilder.AddColumn<string>(
                name: "descripcion",
                table: "DETALLE_PRESUPUESTOS",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_VENTAS_IdPresupuestoOrigen",
                table: "VENTAS",
                column: "id_presupuesto_origen",
                unique: true,
                filter: "[id_presupuesto_origen] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PRESUPUESTOS_Estado_FechaVencimiento",
                table: "PRESUPUESTOS",
                columns: new[] { "estado", "fecha_vencimiento" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Volver a id_cliente NOT NULL no es posible si hay presupuestos a Consumidor Final (id_cliente NULL):
            // se frena con un mensaje claro en lugar de inventar un cliente.
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM PRESUPUESTOS WHERE id_cliente IS NULL) " +
                "THROW 50000, N'No se puede revertir: hay presupuestos a Consumidor Final (id_cliente NULL) y la columna volvería a ser obligatoria.', 1;");
            migrationBuilder.DropIndex(
                name: "IX_VENTAS_IdPresupuestoOrigen",
                table: "VENTAS");

            migrationBuilder.DropIndex(
                name: "IX_PRESUPUESTOS_Estado_FechaVencimiento",
                table: "PRESUPUESTOS");

            migrationBuilder.DropColumn(
                name: "fecha_cierre",
                table: "PRESUPUESTOS");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "PRESUPUESTOS");

            migrationBuilder.DropColumn(
                name: "titulo",
                table: "PRESUPUESTOS");

            migrationBuilder.DropColumn(
                name: "descripcion",
                table: "DETALLE_PRESUPUESTOS");

            migrationBuilder.AlterColumn<int>(
                name: "id_cliente",
                table: "PRESUPUESTOS",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "fecha_vencimiento",
                table: "PRESUPUESTOS",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.CreateIndex(
                name: "IX_VENTAS_id_presupuesto_origen",
                table: "VENTAS",
                column: "id_presupuesto_origen");
        }
    }
}
