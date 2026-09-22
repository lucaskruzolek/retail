using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckConstraintsToEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_VENTAS_Totales",
                table: "VENTAS",
                sql: "[subtotal] >= 0 AND [descuento] >= 0 AND [total] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TURNOS_CAJA_Saldos",
                table: "TURNOS_CAJA",
                sql: "[saldo_inicial] >= 0 AND [total_ventas_efectivo] >= 0 AND [total_ingresos_efectivo] >= 0 AND [total_egresos_efectivo] >= 0 AND [total_ventas_electronicas] >= 0 AND [monto_retenido_en_caja] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PRESUPUESTOS_Totales",
                table: "PRESUPUESTOS",
                sql: "[subtotal] >= 0 AND [descuento] >= 0 AND [total] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PAGOS_VENTA_Monto",
                table: "PAGOS_VENTA",
                sql: "[monto] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MOVIMIENTOS_CAJA_Monto",
                table: "MOVIMIENTOS_CAJA",
                sql: "[monto] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DETALLE_VENTAS_Valores",
                table: "DETALLE_VENTAS",
                sql: "[cantidad] > 0 AND [precio_unitario] >= 0 AND [subtotal_item] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DETALLE_PRESUPUESTOS_Valores",
                table: "DETALLE_PRESUPUESTOS",
                sql: "[cantidad] > 0 AND [precio_unitario_pactado] >= 0 AND [subtotal_item] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DETALLE_COMPRAS_Valores",
                table: "DETALLE_COMPRAS",
                sql: "[cantidad] > 0 AND [costo_unitario] >= 0 AND [subtotal_item] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_COMPROBANTES_FISCALES_Numeracion",
                table: "COMPROBANTES_FISCALES",
                sql: "[punto_venta] > 0 AND [numero_comprobante] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_COMPRAS_Totales",
                table: "COMPRAS",
                sql: "[subtotal] >= 0 AND [total] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_COBRANZAS_CLIENTES_Monto",
                table: "COBRANZAS_CLIENTES",
                sql: "[monto] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CLIENTES_LimiteCredito",
                table: "CLIENTES",
                sql: "[limite_credito] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CLIENTES_SaldoCuentaCorriente",
                table: "CLIENTES",
                sql: "[saldo_cuenta_corriente] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CATALOGOS_PROVEEDORES_PrecioCosto",
                table: "CATALOGOS_PROVEEDORES",
                sql: "[precio_costo] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ARTICULOS_Precios",
                table: "ARTICULOS",
                sql: "[precio_venta] >= 0 AND [costo_reposicion] >= 0 AND [porcentaje_ganancia] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ARTICULOS_StockActual",
                table: "ARTICULOS",
                sql: "([stock_actual] >= 0) OR ([es_servicio] = 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ARTICULOS_StockMinimo",
                table: "ARTICULOS",
                sql: "[stock_minimo] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_VENTAS_Totales",
                table: "VENTAS");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TURNOS_CAJA_Saldos",
                table: "TURNOS_CAJA");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PRESUPUESTOS_Totales",
                table: "PRESUPUESTOS");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PAGOS_VENTA_Monto",
                table: "PAGOS_VENTA");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MOVIMIENTOS_CAJA_Monto",
                table: "MOVIMIENTOS_CAJA");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DETALLE_VENTAS_Valores",
                table: "DETALLE_VENTAS");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DETALLE_PRESUPUESTOS_Valores",
                table: "DETALLE_PRESUPUESTOS");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DETALLE_COMPRAS_Valores",
                table: "DETALLE_COMPRAS");

            migrationBuilder.DropCheckConstraint(
                name: "CK_COMPROBANTES_FISCALES_Numeracion",
                table: "COMPROBANTES_FISCALES");

            migrationBuilder.DropCheckConstraint(
                name: "CK_COMPRAS_Totales",
                table: "COMPRAS");

            migrationBuilder.DropCheckConstraint(
                name: "CK_COBRANZAS_CLIENTES_Monto",
                table: "COBRANZAS_CLIENTES");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CLIENTES_LimiteCredito",
                table: "CLIENTES");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CLIENTES_SaldoCuentaCorriente",
                table: "CLIENTES");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CATALOGOS_PROVEEDORES_PrecioCosto",
                table: "CATALOGOS_PROVEEDORES");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ARTICULOS_Precios",
                table: "ARTICULOS");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ARTICULOS_StockActual",
                table: "ARTICULOS");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ARTICULOS_StockMinimo",
                table: "ARTICULOS");
        }
    }
}
