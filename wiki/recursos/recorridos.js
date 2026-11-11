// Lista única de los recorridos de wiki/07. La leen la portada (tarjetas y cobertura) y cada recorrido (anterior y
// siguiente, y la lista lateral). Agregar un recorrido = escribir su HTML, pasar aquí su estado a "escrito" y nada más.
// Es un .js y no un .json porque el navegador no deja leer un JSON local abierto con doble clic (file://).
//
// archivo: ruta desde wiki/. estado: "escrito" o "pendiente". implementado: false si el código todavía no existe.
// El orden es el de lectura sugerido; anterior y siguiente recorren solo los escritos, en este orden.
window.RETAIL_RECORRIDOS = [
    {
        grupo: "Mostrador",
        titulo: "Venta en mostrador",
        archivo: "07-funcionalidades-de-lo-general-al-detalle/venta-en-mostrador.html",
        requisitos: ["RF-09", "RF-10", "RF-21", "RNF-01", "RNF-02"],
        resumen: "Ticket en el POS, cliente, cobro multimedio con vuelto y cuenta corriente, y descuento de stock en una transacción.",
        estado: "pendiente"
    },
    {
        grupo: "Mostrador",
        titulo: "Turno de caja",
        archivo: "07-funcionalidades-de-lo-general-al-detalle/turno-de-caja.html",
        requisitos: ["RF-13", "RF-14", "RF-15"],
        resumen: "Apertura con saldo inicial, ingresos y retiros justificados, y arqueo ciego con diferencia al cierre.",
        estado: "pendiente"
    },
    {
        grupo: "Mostrador",
        titulo: "Clientes y cobranzas",
        archivo: "07-funcionalidades-de-lo-general-al-detalle/clientes-y-cobranzas.html",
        requisitos: ["RF-20", "RF-09"],
        resumen: "Clientes con CUIT o DNI, condición frente al IVA y límite de crédito; cobranza de la deuda que suma a la caja.",
        estado: "pendiente"
    },
    {
        grupo: "Catálogo y abastecimiento",
        titulo: "Catálogo propio",
        archivo: "07-funcionalidades-de-lo-general-al-detalle/catalogo-propio.html",
        requisitos: ["RF-04", "RF-06", "RF-08", "RF-21"],
        resumen: "Alta y edición de artículos y servicios, markup y precio de venta, baja lógica, stock mínimo y presentaciones derivadas.",
        estado: "pendiente"
    },
    {
        grupo: "Catálogo y abastecimiento",
        titulo: "Clasificación del catálogo",
        archivo: "07-funcionalidades-de-lo-general-al-detalle/clasificacion-del-catalogo.html",
        requisitos: ["RF-04", "RF-27"],
        resumen: "Rubros, categorías y marcas, y los diccionarios de equivalencias y términos que alimentan la sugerencia.",
        estado: "pendiente"
    },
    {
        grupo: "Catálogo y abastecimiento",
        titulo: "Importador de catálogos",
        archivo: "07-funcionalidades-de-lo-general-al-detalle/importador-de-catalogos.html",
        requisitos: ["RF-05", "RF-07", "RF-21", "RF-27", "RF-28"],
        resumen: "Importación de listas de precios, clasificación del catálogo del proveedor, incorporación a la tienda y fuentes múltiples.",
        estado: "escrito"
    },
    {
        grupo: "Catálogo y abastecimiento",
        titulo: "Proveedores",
        archivo: "07-funcionalidades-de-lo-general-al-detalle/proveedores.html",
        requisitos: ["RF-05", "RF-28"],
        resumen: "Alta de proveedores con CUIT validado y su relación con los catálogos y las fuentes de cada artículo.",
        estado: "pendiente"
    },
    {
        grupo: "Catálogo y abastecimiento",
        titulo: "Pedidos a proveedores",
        archivo: "07-funcionalidades-de-lo-general-al-detalle/pedidos-a-proveedores.html",
        requisitos: ["RF-29", "RF-28"],
        resumen: "Pedidos armados desde el catálogo, por fila o en lote, con la fuente principal de cada artículo, y su emisión.",
        estado: "pendiente"
    },
    {
        grupo: "Catálogo y abastecimiento",
        titulo: "Compra a distribuidor",
        archivo: "07-funcionalidades-de-lo-general-al-detalle/compra-a-distribuidor.html",
        requisitos: ["RF-19", "RF-29", "RF-10"],
        resumen: "Factura del distribuidor: sube el stock, actualiza el costo y recalcula el precio; se puede precargar desde un pedido.",
        estado: "pendiente"
    },
    {
        grupo: "Administración",
        titulo: "Acceso y usuarios",
        archivo: "07-funcionalidades-de-lo-general-al-detalle/acceso-y-usuarios.html",
        requisitos: ["RF-01", "RF-02", "RF-03", "RNF-04", "RNF-06"],
        resumen: "Inicio de sesión con BCrypt, cambio rápido de usuario, roles y permisos por página, y administración de usuarios.",
        estado: "pendiente"
    },
    {
        grupo: "Administración",
        titulo: "Copia de seguridad",
        archivo: "07-funcionalidades-de-lo-general-al-detalle/copia-de-seguridad.html",
        requisitos: ["RF-22", "RF-23", "RNF-08", "RF-03"],
        resumen: "Copia manual, diaria y previa a migrar o a restaurar; verificación, bloqueo entre procesos, copias huérfanas y retención.",
        estado: "escrito"
    },
    {
        grupo: "Administración",
        titulo: "Restauración de una copia",
        archivo: "07-funcionalidades-de-lo-general-al-detalle/restauracion.html",
        requisitos: ["RF-22", "RF-23", "RF-03"],
        resumen: "Desde el historial, con control dual, y por consola; base temporal e intercambio, historial que sobrevive y red de seguridad.",
        estado: "escrito"
    },
    {
        grupo: "Administración",
        titulo: "Tablero y reportes",
        archivo: "07-funcionalidades-de-lo-general-al-detalle/tablero-y-reportes.html",
        requisitos: ["RF-24", "RF-25", "RF-26", "RNF-09"],
        resumen: "Indicadores del período, reportes parametrizables resueltos en SQL y exportación a Excel y PDF.",
        estado: "pendiente"
    },
    {
        grupo: "Sin implementar",
        titulo: "Presupuestos y facturación ARCA",
        archivo: "07-funcionalidades-de-lo-general-al-detalle/presupuestos-y-arca.html",
        requisitos: ["RF-11", "RF-12", "RF-16", "RF-17", "RF-18", "RNF-07"],
        resumen: "Presupuestos con vigencia y conversión a venta, emisión fiscal asíncrona y contingencia. Solo existen los contratos.",
        estado: "pendiente",
        implementado: false
    }
];
