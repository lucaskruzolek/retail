using Retail.Application.DTOs.Backups;
using Retail.Application.DTOs.Caja;
using Retail.Application.DTOs.Pedidos;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.DTOs.Usuarios;
using Retail.Domain.Enums;

namespace Retail.App.UnitTests.Snapshots;

/// <summary>
/// Datos de muestra realistas de una librería para poblar las páginas de proveedores, usuarios, pedidos, importador de
/// catálogos, respaldos y caja en las capturas (<see cref="PaginasSnapshotTests"/>). Complementa a <see cref="DatosDeMuestra"/>.
/// </summary>
internal static class DatosDeMuestraPaginas
{
    public static IReadOnlyList<ProveedorDto> Proveedores()
    {
        return
        [
            Proveedor(1, "Distribuidora Sur S.R.L.", "30-71234567-8", "011 4567-1200", "ventas@distribuidorasur.com.ar"),
            Proveedor(2, "Papelera del Plata S.A.", "30-69876543-2", "0221 453-7788", "pedidos@papeleradelplata.com.ar"),
            Proveedor(3, "Mayorista Escolar Norte", "30-70112233-4", "0381 422-9090", "info@escolarnorte.com.ar"),
            Proveedor(4, "Útiles y Más S.A.", "30-68445566-1", "0341 430-1122", null),
            Proveedor(5, "Editorial Horizonte", "30-71998877-5", "011 4821-6600", "comercial@editorialhorizonte.com.ar"),
            Proveedor(6, "Cartonería Rosario", "30-65432109-7", null, "ventas@cartoneriarosario.com.ar"),
            Proveedor(7, "Insumos de Oficina Cuyo", "30-70987654-3", "0261 425-3344", "cuyo@insumosoficina.com.ar"),
            Proveedor(8, "Marcela Benítez (Artesanías)", "27-23456789-4", "11 5544-3322", null)
        ];
    }

    public static IReadOnlyList<UsuarioDto> Usuarios()
    {
        return
        [
            Usuario(1, "lkruzolek", "Lucas", "Kruzolek", RolUsuarioEnum.Gerente, true, false, new DateTime(2026, 8, 3)),
            Usuario(2, "pfernandez", "Pablo", "Fernandez", RolUsuarioEnum.Gerente, true, false, new DateTime(2026, 8, 3)),
            Usuario(3, "mgomez", "María Laura", "Gómez", RolUsuarioEnum.Encargado, true, false, new DateTime(2026, 8, 20)),
            Usuario(4, "jrossi", "Juan Pablo", "Rossi", RolUsuarioEnum.Cajero, true, false, new DateTime(2026, 9, 2)),
            Usuario(5, "lfernandez", "Lucía", "Fernández", RolUsuarioEnum.Cajero, true, true, new DateTime(2026, 9, 14)),
            Usuario(6, "dmolina", "Diego", "Molina", RolUsuarioEnum.Cajero, false, false, new DateTime(2026, 9, 14)),
            Usuario(7, "soporte", "Soporte", "Técnico", RolUsuarioEnum.Administrador, true, false, new DateTime(2026, 7, 28)),
            Usuario(8, "cbenitez", "Carolina", "Benítez", RolUsuarioEnum.Encargado, true, false, new DateTime(2026, 10, 5))
        ];
    }

    public static IReadOnlyList<PedidoResumenDto> Pedidos()
    {
        return
        [
            Pedido(48, 1, "Distribuidora Sur S.R.L.", EstadoPedidoEnum.Borrador, new DateTime(2026, 10, 30, 14, 20, 0), null, null, 6, 184500m),
            Pedido(47, 2, "Papelera del Plata S.A.", EstadoPedidoEnum.Emitido, new DateTime(2026, 10, 28, 12, 5, 0), new DateTime(2026, 10, 28, 12, 40, 0), null, 3, 312000m),
            Pedido(46, 1, "Distribuidora Sur S.R.L.", EstadoPedidoEnum.Emitido, new DateTime(2026, 10, 27, 9, 30, 0), new DateTime(2026, 10, 27, 10, 0, 0), null, 12, 96750m),
            Pedido(45, 3, "Mayorista Escolar Norte", EstadoPedidoEnum.Recibido, new DateTime(2026, 10, 22, 16, 0, 0), new DateTime(2026, 10, 22, 16, 15, 0), new DateTime(2026, 10, 26, 11, 0, 0), 9, 540200m),
            Pedido(44, 5, "Editorial Horizonte", EstadoPedidoEnum.Recibido, new DateTime(2026, 10, 18, 10, 45, 0), new DateTime(2026, 10, 18, 11, 0, 0), new DateTime(2026, 10, 23, 15, 30, 0), 4, 227800m),
            Pedido(43, 4, "Útiles y Más S.A.", EstadoPedidoEnum.Cancelado, new DateTime(2026, 10, 15, 13, 10, 0), null, new DateTime(2026, 10, 15, 13, 40, 0), 2, 38400m),
            Pedido(42, 2, "Papelera del Plata S.A.", EstadoPedidoEnum.Recibido, new DateTime(2026, 10, 9, 8, 50, 0), new DateTime(2026, 10, 9, 9, 10, 0), new DateTime(2026, 10, 14, 17, 0, 0), 7, 418900m),
            Pedido(41, 6, "Cartonería Rosario", EstadoPedidoEnum.Cancelado, new DateTime(2026, 10, 6, 15, 25, 0), new DateTime(2026, 10, 6, 15, 50, 0), new DateTime(2026, 10, 8, 9, 0, 0), 5, 121300m)
        ];
    }

    public static IReadOnlyList<CatalogoProveedorDto> CatalogoProveedor()
    {
        var hoy = new DateTime(2026, 10, 28);
        return
        [
            ItemCatalogo(1, "DS-CU-001", "7790001000011", "Cuaderno Rivadavia ABC 50 hojas rayado", 1110m, hoy, true, true, 1, "Cuaderno Rivadavia ABC 50 hojas rayado", 1850m, 42, "Cuadernos", "Rivadavia"),
            ItemCatalogo(2, "DS-LA-014", "7790002000028", "Lápiz negro Faber-Castell HB x12", 3300m, hoy, true, true, 2, "Lápiz negro Faber-Castell HB", 320m, 8, "Lápices", "Faber-Castell"),
            ItemCatalogo(3, "DS-BI-203", "7790004000042", "Birome Bic Cristal azul caja x50", 9800m, hoy, true, false, 4, "Birome Bic Cristal azul", 280m, 150, "Lapiceras y bolígrafos", "Bic"),
            ItemCatalogo(4, "DS-CU-007", "7790001000059", "Cuaderno Rivadavia ABC 100 hojas cuadriculado", 1740m, hoy, false, false, null, null, null, null, "Cuadernos", "Rivadavia"),
            ItemCatalogo(5, "DS-MA-031", "7790007000080", "Marcador Sharpie punta fina negro", 1250m, hoy, false, false, null, null, null, null, null, "Sharpie"),
            ItemCatalogo(6, "DS-CO-044", "7790006000073", "Corrector líquido Bic Wite-Out", 570m, hoy, true, true, 9, "Corrector líquido Bic Wite-Out", 950m, 18, "Correctores", "Bic"),
            ItemCatalogo(7, "DS-CA-090", null, "Carpeta oficio 3 anillos lomo ancho", 2100m, hoy, false, false, null, null, null, null, null, null, pendiente: true),
            ItemCatalogo(8, "DS-RE-012", "7790003000035", "Resma A4 Autor 75g x 500 hojas", 4500m, hoy, true, true, 3, "Resma A4 Autor 75g x 500 hojas", 6900m, 0, null, null),
            ItemCatalogo(9, "DS-TI-055", "7790008000097", "Tijera escolar punta redonda 13 cm", 890m, hoy, false, false, null, null, null, null, null, null, pendiente: true)
        ];
    }

    public static IReadOnlyList<RegistroBackupDto> Copias()
    {
        return
        [
            Copia(1, TipoBackupEnum.Manual, EstadoBackupEnum.Verificado, "lkruzolek", new DateTime(2026, 10, 30, 18, 5, 0), 48L * 1024 * 1024, true),
            Copia(2, TipoBackupEnum.Automatico, EstadoBackupEnum.Verificado, "Sistema", new DateTime(2026, 10, 30, 3, 0, 0), 47L * 1024 * 1024, true),
            Copia(3, TipoBackupEnum.Automatico, EstadoBackupEnum.Exitoso, "Sistema", new DateTime(2026, 10, 29, 3, 0, 0), 47L * 1024 * 1024, true),
            Copia(4, TipoBackupEnum.PreRestauracion, EstadoBackupEnum.Verificado, "pfernandez", new DateTime(2026, 10, 27, 11, 20, 0), 45L * 1024 * 1024, true),
            Copia(5, TipoBackupEnum.Automatico, EstadoBackupEnum.Fallido, "Sistema", new DateTime(2026, 10, 26, 3, 0, 0), null, false),
            Copia(6, TipoBackupEnum.PreMigracion, EstadoBackupEnum.Exitoso, "Sistema", new DateTime(2026, 10, 20, 9, 45, 0), 41L * 1024 * 1024, true),
            Copia(7, TipoBackupEnum.Manual, EstadoBackupEnum.ArchivoPurgado, "lkruzolek", new DateTime(2026, 9, 12, 17, 30, 0), 39L * 1024 * 1024, false),
            Copia(8, TipoBackupEnum.Automatico, EstadoBackupEnum.EnCurso, "Sistema", new DateTime(2026, 10, 31, 3, 0, 0), null, false)
        ];
    }

    public static RegistroRestauracionDto UltimaRestauracion()
    {
        return new RegistroRestauracionDto
        {
            Referencia = Guid.NewGuid(),
            Fecha = new DateTime(2026, 10, 27, 14, 25, 0),
            Origen = OrigenRestauracionEnum.Aplicacion,
            NombreUsuario = "pfernandez",
            NombreOperador = "mgomez",
            ReferenciaCopia = Guid.NewGuid(),
            RutaArchivo = @"C:\RetailData\Backups\Retail_20261020.bak",
            TuvoCopiaPrevia = true,
            BaseAnterior = "Retail_anterior"
        };
    }

    public static TurnoCajaDto TurnoAbierto()
    {
        return new TurnoCajaDto
        {
            IdTurno = 112,
            IdUsuario = 4,
            NombreUsuario = "jrossi",
            FechaApertura = new DateTime(2026, 10, 31, 8, 30, 0),
            SaldoInicial = 20000m,
            TotalVentasEfectivo = 184350m,
            TotalIngresosEfectivo = 15000m,
            TotalEgresosEfectivo = 32500m,
            SaldoTeoricoEfectivo = 186850m,
            TotalVentasElectronicas = 252900m,
            Estado = EstadoTurnoEnum.Abierto
        };
    }

    private static ProveedorDto Proveedor(int id, string razonSocial, string cuit, string? telefono, string? email)
    {
        return new ProveedorDto
        {
            IdProveedor = id,
            RazonSocial = razonSocial,
            Cuit = cuit,
            Telefono = telefono,
            Email = email
        };
    }

    private static UsuarioDto Usuario(
        int id,
        string nombreUsuario,
        string nombre,
        string apellido,
        RolUsuarioEnum rol,
        bool activo,
        bool bloqueada,
        DateTime creado)
    {
        return new UsuarioDto
        {
            IdUsuario = id,
            NombreUsuario = nombreUsuario,
            Nombre = nombre,
            Apellido = apellido,
            Rol = rol,
            Activo = activo,
            EstaBloqueada = bloqueada,
            CreatedAt = creado
        };
    }

    private static PedidoResumenDto Pedido(
        int id,
        int idProveedor,
        string razonSocial,
        EstadoPedidoEnum estado,
        DateTime creacion,
        DateTime? emision,
        DateTime? cierre,
        int renglones,
        decimal total)
    {
        return new PedidoResumenDto
        {
            IdPedido = id,
            IdProveedor = idProveedor,
            RazonSocialProveedor = razonSocial,
            Estado = estado,
            FechaCreacion = creacion,
            FechaEmision = emision,
            FechaCierre = cierre,
            CantidadRenglones = renglones,
            TotalEstimado = total
        };
    }

    private static CatalogoProveedorDto ItemCatalogo(
        int id,
        string codigoProveedor,
        string? codigoBarras,
        string descripcion,
        decimal costo,
        DateTime actualizado,
        bool vinculado,
        bool coincide,
        int? idArticulo,
        string? nombreArticulo,
        decimal? precioVenta,
        int? stock,
        string? categoriaSugerida = null,
        string? marcaSugerida = null,
        bool pendiente = false)
    {
        return new CatalogoProveedorDto
        {
            Id = id,
            IdProveedor = 1,
            ProveedorRazonSocial = "Distribuidora Sur S.R.L.",
            CodigoProveedor = codigoProveedor,
            CodigoBarras = codigoBarras,
            DescripcionProveedor = descripcion,
            CostoReposicion = costo,
            FechaActualizacion = actualizado,
            EstaVinculado = vinculado,
            CoincideConArticuloTienda = coincide,
            IdArticuloVinculado = idArticulo,
            NombreArticuloTienda = nombreArticulo,
            PrecioVentaTienda = precioVenta,
            StockActualTienda = stock,
            CategoriaSugerida = categoriaSugerida,
            MarcaSugerida = marcaSugerida,
            FechaClasificacion = pendiente ? null : actualizado
        };
    }

    private static RegistroBackupDto Copia(
        int id,
        TipoBackupEnum tipo,
        EstadoBackupEnum estado,
        string operador,
        DateTime inicio,
        long? tamanoBytes,
        bool restaurable)
    {
        return new RegistroBackupDto
        {
            IdRegistroBackup = id,
            Referencia = Guid.NewGuid(),
            Tipo = tipo,
            Estado = estado,
            Operador = operador,
            FechaInicio = inicio,
            RutaArchivo = $@"C:\RetailData\Backups\Retail_{inicio:yyyyMMdd_HHmm}.bak",
            TamanoBytes = tamanoBytes,
            MensajeError = estado == EstadoBackupEnum.Fallido ? "No hay espacio suficiente en el disco de destino." : null,
            EsRestaurable = restaurable
        };
    }
}
