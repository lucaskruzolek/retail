using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CATEGORIAS",
                columns: table => new
                {
                    id_categoria = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    nombre_categoria = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CATEGORIAS", x => x.id_categoria);
                });

            migrationBuilder.CreateTable(
                name: "CLIENTES",
                columns: table => new
                {
                    id_cliente = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    razon_social_o_nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    tipo_documento = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    numero_documento = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    condicion_iva = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    domicilio_fiscal = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    telefono = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    tiene_cuenta_corriente = table.Column<bool>(type: "bit", nullable: false),
                    limite_credito = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    saldo_cuenta_corriente = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CLIENTES", x => x.id_cliente);
                });

            migrationBuilder.CreateTable(
                name: "MARCAS",
                columns: table => new
                {
                    id_marca = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    nombre_marca = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MARCAS", x => x.id_marca);
                });

            migrationBuilder.CreateTable(
                name: "PROVEEDORES",
                columns: table => new
                {
                    id_proveedor = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    razon_social = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    cuit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    telefono = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PROVEEDORES", x => x.id_proveedor);
                });

            migrationBuilder.CreateTable(
                name: "ROLES",
                columns: table => new
                {
                    id_rol = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    nombre_rol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ROLES", x => x.id_rol);
                });

            migrationBuilder.CreateTable(
                name: "CATALOGOS_PROVEEDORES",
                columns: table => new
                {
                    id_catalogo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_proveedor = table.Column<int>(type: "int", nullable: false),
                    codigo_proveedor = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    descripcion_proveedor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    precio_costo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    fecha_actualizacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CATALOGOS_PROVEEDORES", x => x.id_catalogo);
                    table.ForeignKey(
                        name: "FK_CATALOGOS_PROVEEDORES_PROVEEDORES_id_proveedor",
                        column: x => x.id_proveedor,
                        principalTable: "PROVEEDORES",
                        principalColumn: "id_proveedor",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "USUARIOS",
                columns: table => new
                {
                    id_usuario = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    nombre_usuario = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    password_hash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    nombre_completo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    id_rol = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_USUARIOS", x => x.id_usuario);
                    table.ForeignKey(
                        name: "FK_USUARIOS_ROLES_id_rol",
                        column: x => x.id_rol,
                        principalTable: "ROLES",
                        principalColumn: "id_rol",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ARTICULOS",
                columns: table => new
                {
                    id_articulo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    codigo_barras = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    id_categoria = table.Column<int>(type: "int", nullable: false),
                    id_marca = table.Column<int>(type: "int", nullable: false),
                    id_catalogo_proveedor = table.Column<int>(type: "int", nullable: true),
                    costo_reposicion = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    porcentaje_ganancia = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    precio_venta = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    stock_actual = table.Column<int>(type: "int", nullable: false),
                    stock_minimo = table.Column<int>(type: "int", nullable: false),
                    es_servicio = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ARTICULOS", x => x.id_articulo);
                    table.ForeignKey(
                        name: "FK_ARTICULOS_CATALOGOS_PROVEEDORES_id_catalogo_proveedor",
                        column: x => x.id_catalogo_proveedor,
                        principalTable: "CATALOGOS_PROVEEDORES",
                        principalColumn: "id_catalogo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ARTICULOS_CATEGORIAS_id_categoria",
                        column: x => x.id_categoria,
                        principalTable: "CATEGORIAS",
                        principalColumn: "id_categoria",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ARTICULOS_MARCAS_id_marca",
                        column: x => x.id_marca,
                        principalTable: "MARCAS",
                        principalColumn: "id_marca",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COMPRAS",
                columns: table => new
                {
                    id_compra = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_proveedor = table.Column<int>(type: "int", nullable: false),
                    id_usuario = table.Column<int>(type: "int", nullable: false),
                    tipo_comprobante = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    numero_comprobante = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    fecha_emision = table.Column<DateTime>(type: "datetime2", nullable: false),
                    subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COMPRAS", x => x.id_compra);
                    table.ForeignKey(
                        name: "FK_COMPRAS_PROVEEDORES_id_proveedor",
                        column: x => x.id_proveedor,
                        principalTable: "PROVEEDORES",
                        principalColumn: "id_proveedor",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COMPRAS_USUARIOS_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "USUARIOS",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PRESUPUESTOS",
                columns: table => new
                {
                    id_presupuesto = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_usuario = table.Column<int>(type: "int", nullable: false),
                    id_cliente = table.Column<int>(type: "int", nullable: false),
                    fecha_emision = table.Column<DateTime>(type: "datetime2", nullable: false),
                    fecha_vencimiento = table.Column<DateTime>(type: "datetime2", nullable: false),
                    subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    descuento = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRESUPUESTOS", x => x.id_presupuesto);
                    table.ForeignKey(
                        name: "FK_PRESUPUESTOS_CLIENTES_id_cliente",
                        column: x => x.id_cliente,
                        principalTable: "CLIENTES",
                        principalColumn: "id_cliente",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PRESUPUESTOS_USUARIOS_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "USUARIOS",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TURNOS_CAJA",
                columns: table => new
                {
                    id_turno = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_usuario = table.Column<int>(type: "int", nullable: false),
                    fecha_apertura = table.Column<DateTime>(type: "datetime2", nullable: false),
                    saldo_inicial = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    fecha_cierre = table.Column<DateTime>(type: "datetime2", nullable: true),
                    total_ventas_efectivo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    total_ingresos_efectivo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    total_egresos_efectivo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    saldo_teorico_efectivo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    saldo_declarado_efectivo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    diferencia_efectivo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    total_ventas_electronicas = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    monto_retenido_en_caja = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TURNOS_CAJA", x => x.id_turno);
                    table.ForeignKey(
                        name: "FK_TURNOS_CAJA_USUARIOS_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "USUARIOS",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DETALLE_COMPRAS",
                columns: table => new
                {
                    id_detalle_compra = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_compra = table.Column<int>(type: "int", nullable: false),
                    id_articulo = table.Column<int>(type: "int", nullable: false),
                    cantidad = table.Column<int>(type: "int", nullable: false),
                    costo_unitario = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    subtotal_item = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DETALLE_COMPRAS", x => x.id_detalle_compra);
                    table.ForeignKey(
                        name: "FK_DETALLE_COMPRAS_ARTICULOS_id_articulo",
                        column: x => x.id_articulo,
                        principalTable: "ARTICULOS",
                        principalColumn: "id_articulo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DETALLE_COMPRAS_COMPRAS_id_compra",
                        column: x => x.id_compra,
                        principalTable: "COMPRAS",
                        principalColumn: "id_compra",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DETALLE_PRESUPUESTOS",
                columns: table => new
                {
                    id_detalle_presupuesto = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_presupuesto = table.Column<int>(type: "int", nullable: false),
                    id_articulo = table.Column<int>(type: "int", nullable: false),
                    cantidad = table.Column<int>(type: "int", nullable: false),
                    precio_unitario_pactado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    subtotal_item = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DETALLE_PRESUPUESTOS", x => x.id_detalle_presupuesto);
                    table.ForeignKey(
                        name: "FK_DETALLE_PRESUPUESTOS_ARTICULOS_id_articulo",
                        column: x => x.id_articulo,
                        principalTable: "ARTICULOS",
                        principalColumn: "id_articulo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DETALLE_PRESUPUESTOS_PRESUPUESTOS_id_presupuesto",
                        column: x => x.id_presupuesto,
                        principalTable: "PRESUPUESTOS",
                        principalColumn: "id_presupuesto",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "COBRANZAS_CLIENTES",
                columns: table => new
                {
                    id_cobranza = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_cliente = table.Column<int>(type: "int", nullable: false),
                    id_turno = table.Column<int>(type: "int", nullable: false),
                    id_usuario = table.Column<int>(type: "int", nullable: false),
                    fecha_hora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    medio_pago = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    monto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    referencia = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COBRANZAS_CLIENTES", x => x.id_cobranza);
                    table.ForeignKey(
                        name: "FK_COBRANZAS_CLIENTES_CLIENTES_id_cliente",
                        column: x => x.id_cliente,
                        principalTable: "CLIENTES",
                        principalColumn: "id_cliente",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COBRANZAS_CLIENTES_TURNOS_CAJA_id_turno",
                        column: x => x.id_turno,
                        principalTable: "TURNOS_CAJA",
                        principalColumn: "id_turno",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COBRANZAS_CLIENTES_USUARIOS_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "USUARIOS",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MOVIMIENTOS_CAJA",
                columns: table => new
                {
                    id_movimiento = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_turno = table.Column<int>(type: "int", nullable: false),
                    tipo_movimiento = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    monto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    concepto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    fecha_hora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MOVIMIENTOS_CAJA", x => x.id_movimiento);
                    table.ForeignKey(
                        name: "FK_MOVIMIENTOS_CAJA_TURNOS_CAJA_id_turno",
                        column: x => x.id_turno,
                        principalTable: "TURNOS_CAJA",
                        principalColumn: "id_turno",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VENTAS",
                columns: table => new
                {
                    id_venta = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_turno = table.Column<int>(type: "int", nullable: false),
                    id_usuario = table.Column<int>(type: "int", nullable: false),
                    id_cliente = table.Column<int>(type: "int", nullable: false),
                    id_presupuesto_origen = table.Column<int>(type: "int", nullable: true),
                    fecha_hora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    descuento = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    estado_fiscal = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VENTAS", x => x.id_venta);
                    table.ForeignKey(
                        name: "FK_VENTAS_CLIENTES_id_cliente",
                        column: x => x.id_cliente,
                        principalTable: "CLIENTES",
                        principalColumn: "id_cliente",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VENTAS_PRESUPUESTOS_id_presupuesto_origen",
                        column: x => x.id_presupuesto_origen,
                        principalTable: "PRESUPUESTOS",
                        principalColumn: "id_presupuesto",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VENTAS_TURNOS_CAJA_id_turno",
                        column: x => x.id_turno,
                        principalTable: "TURNOS_CAJA",
                        principalColumn: "id_turno",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VENTAS_USUARIOS_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "USUARIOS",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COMPROBANTES_FISCALES",
                columns: table => new
                {
                    id_comprobante = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_venta = table.Column<int>(type: "int", nullable: false),
                    tipo_comprobante = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    punto_venta = table.Column<int>(type: "int", nullable: false),
                    numero_comprobante = table.Column<int>(type: "int", nullable: false),
                    cae = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    fecha_vto_cae = table.Column<DateTime>(type: "datetime2", nullable: true),
                    resultado_arca = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    motivo_error = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    fecha_emision = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COMPROBANTES_FISCALES", x => x.id_comprobante);
                    table.ForeignKey(
                        name: "FK_COMPROBANTES_FISCALES_VENTAS_id_venta",
                        column: x => x.id_venta,
                        principalTable: "VENTAS",
                        principalColumn: "id_venta",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DETALLE_VENTAS",
                columns: table => new
                {
                    id_detalle = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_venta = table.Column<int>(type: "int", nullable: false),
                    id_articulo = table.Column<int>(type: "int", nullable: false),
                    cantidad = table.Column<int>(type: "int", nullable: false),
                    precio_unitario = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    subtotal_item = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DETALLE_VENTAS", x => x.id_detalle);
                    table.ForeignKey(
                        name: "FK_DETALLE_VENTAS_ARTICULOS_id_articulo",
                        column: x => x.id_articulo,
                        principalTable: "ARTICULOS",
                        principalColumn: "id_articulo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DETALLE_VENTAS_VENTAS_id_venta",
                        column: x => x.id_venta,
                        principalTable: "VENTAS",
                        principalColumn: "id_venta",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PAGOS_VENTA",
                columns: table => new
                {
                    id_pago = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_venta = table.Column<int>(type: "int", nullable: false),
                    medio_pago = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    monto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    referencia_pago = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAGOS_VENTA", x => x.id_pago);
                    table.ForeignKey(
                        name: "FK_PAGOS_VENTA_VENTAS_id_venta",
                        column: x => x.id_venta,
                        principalTable: "VENTAS",
                        principalColumn: "id_venta",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ARTICULOS_codigo_barras",
                table: "ARTICULOS",
                column: "codigo_barras",
                unique: true,
                filter: "[codigo_barras] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ARTICULOS_id_catalogo_proveedor",
                table: "ARTICULOS",
                column: "id_catalogo_proveedor");

            migrationBuilder.CreateIndex(
                name: "IX_ARTICULOS_id_categoria",
                table: "ARTICULOS",
                column: "id_categoria");

            migrationBuilder.CreateIndex(
                name: "IX_ARTICULOS_id_marca",
                table: "ARTICULOS",
                column: "id_marca");

            migrationBuilder.CreateIndex(
                name: "IX_CATALOGOS_PROVEEDORES_id_proveedor",
                table: "CATALOGOS_PROVEEDORES",
                column: "id_proveedor");

            migrationBuilder.CreateIndex(
                name: "IX_CLIENTES_numero_documento",
                table: "CLIENTES",
                column: "numero_documento",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COBRANZAS_CLIENTES_id_cliente",
                table: "COBRANZAS_CLIENTES",
                column: "id_cliente");

            migrationBuilder.CreateIndex(
                name: "IX_COBRANZAS_CLIENTES_id_turno",
                table: "COBRANZAS_CLIENTES",
                column: "id_turno");

            migrationBuilder.CreateIndex(
                name: "IX_COBRANZAS_CLIENTES_id_usuario",
                table: "COBRANZAS_CLIENTES",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "IX_COMPRAS_id_proveedor",
                table: "COMPRAS",
                column: "id_proveedor");

            migrationBuilder.CreateIndex(
                name: "IX_COMPRAS_id_usuario",
                table: "COMPRAS",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "IX_COMPROBANTES_FISCALES_id_venta",
                table: "COMPROBANTES_FISCALES",
                column: "id_venta",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DETALLE_COMPRAS_id_articulo",
                table: "DETALLE_COMPRAS",
                column: "id_articulo");

            migrationBuilder.CreateIndex(
                name: "IX_DETALLE_COMPRAS_id_compra",
                table: "DETALLE_COMPRAS",
                column: "id_compra");

            migrationBuilder.CreateIndex(
                name: "IX_DETALLE_PRESUPUESTOS_id_articulo",
                table: "DETALLE_PRESUPUESTOS",
                column: "id_articulo");

            migrationBuilder.CreateIndex(
                name: "IX_DETALLE_PRESUPUESTOS_id_presupuesto",
                table: "DETALLE_PRESUPUESTOS",
                column: "id_presupuesto");

            migrationBuilder.CreateIndex(
                name: "IX_DETALLE_VENTAS_id_articulo",
                table: "DETALLE_VENTAS",
                column: "id_articulo");

            migrationBuilder.CreateIndex(
                name: "IX_DETALLE_VENTAS_id_venta",
                table: "DETALLE_VENTAS",
                column: "id_venta");

            migrationBuilder.CreateIndex(
                name: "IX_MOVIMIENTOS_CAJA_id_turno",
                table: "MOVIMIENTOS_CAJA",
                column: "id_turno");

            migrationBuilder.CreateIndex(
                name: "IX_PAGOS_VENTA_id_venta",
                table: "PAGOS_VENTA",
                column: "id_venta");

            migrationBuilder.CreateIndex(
                name: "IX_PRESUPUESTOS_id_cliente",
                table: "PRESUPUESTOS",
                column: "id_cliente");

            migrationBuilder.CreateIndex(
                name: "IX_PRESUPUESTOS_id_usuario",
                table: "PRESUPUESTOS",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "IX_PROVEEDORES_cuit",
                table: "PROVEEDORES",
                column: "cuit",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TURNOS_CAJA_id_usuario",
                table: "TURNOS_CAJA",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "IX_USUARIOS_id_rol",
                table: "USUARIOS",
                column: "id_rol");

            migrationBuilder.CreateIndex(
                name: "IX_USUARIOS_nombre_usuario",
                table: "USUARIOS",
                column: "nombre_usuario",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VENTAS_id_cliente",
                table: "VENTAS",
                column: "id_cliente");

            migrationBuilder.CreateIndex(
                name: "IX_VENTAS_id_presupuesto_origen",
                table: "VENTAS",
                column: "id_presupuesto_origen");

            migrationBuilder.CreateIndex(
                name: "IX_VENTAS_id_turno",
                table: "VENTAS",
                column: "id_turno");

            migrationBuilder.CreateIndex(
                name: "IX_VENTAS_id_usuario",
                table: "VENTAS",
                column: "id_usuario");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "COBRANZAS_CLIENTES");

            migrationBuilder.DropTable(
                name: "COMPROBANTES_FISCALES");

            migrationBuilder.DropTable(
                name: "DETALLE_COMPRAS");

            migrationBuilder.DropTable(
                name: "DETALLE_PRESUPUESTOS");

            migrationBuilder.DropTable(
                name: "DETALLE_VENTAS");

            migrationBuilder.DropTable(
                name: "MOVIMIENTOS_CAJA");

            migrationBuilder.DropTable(
                name: "PAGOS_VENTA");

            migrationBuilder.DropTable(
                name: "COMPRAS");

            migrationBuilder.DropTable(
                name: "ARTICULOS");

            migrationBuilder.DropTable(
                name: "VENTAS");

            migrationBuilder.DropTable(
                name: "CATALOGOS_PROVEEDORES");

            migrationBuilder.DropTable(
                name: "CATEGORIAS");

            migrationBuilder.DropTable(
                name: "MARCAS");

            migrationBuilder.DropTable(
                name: "PRESUPUESTOS");

            migrationBuilder.DropTable(
                name: "TURNOS_CAJA");

            migrationBuilder.DropTable(
                name: "PROVEEDORES");

            migrationBuilder.DropTable(
                name: "CLIENTES");

            migrationBuilder.DropTable(
                name: "USUARIOS");

            migrationBuilder.DropTable(
                name: "ROLES");
        }
    }
}
