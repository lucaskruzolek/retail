using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lleva los clientes ya guardados a la forma canónica que ahora exige el agregado Cliente: documento sin
    /// separadores (pasaporte en mayúsculas), email en minúsculas y teléfono con solo dígitos y un "+" inicial.
    /// No cambia el esquema.
    /// </summary>
    /// <remarks>
    /// Sin esta migración, un "12.345.678" guardado antes no coincidiría con el "12345678" que ahora busca
    /// ClienteService, y el duplicado volvería a pasar. Un documento solo se normaliza si el resultado no choca con
    /// otro cliente activo (si chocara, el índice único filtrado haría fallar la migración y la aplicación no
    /// arrancaría); y si varias filas normalizan al mismo valor, solo se actualiza la de menor id. Las que quedan
    /// sin normalizar son duplicados reales que debe resolver una persona: elegir cuál conservar es una decisión
    /// de negocio. Los valores con otros errores de formato se corrigen al editar el cliente, porque el agregado
    /// los rechaza al guardar.
    /// </remarks>
    public partial class NormalizarDocumentosYContactoClientes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH candidatos AS (
                    SELECT c.id_cliente,
                           c.deleted_at,
                           n.normalizado,
                           ROW_NUMBER() OVER (PARTITION BY n.normalizado, CASE WHEN c.deleted_at IS NULL THEN 0 ELSE c.id_cliente END
                                              ORDER BY c.id_cliente) AS orden
                    FROM CLIENTES c
                    CROSS APPLY (SELECT CASE WHEN c.tipo_documento = 'Pasaporte'
                                             THEN UPPER(REPLACE(REPLACE(REPLACE(c.numero_documento, '.', ''), '-', ''), ' ', ''))
                                             ELSE REPLACE(REPLACE(REPLACE(c.numero_documento, '.', ''), '-', ''), ' ', '')
                                        END AS normalizado) n
                    WHERE c.numero_documento <> n.normalizado COLLATE Latin1_General_BIN2
                )
                UPDATE c
                SET numero_documento = cand.normalizado
                FROM CLIENTES c
                JOIN candidatos cand ON cand.id_cliente = c.id_cliente
                WHERE cand.deleted_at IS NOT NULL
                   OR (cand.orden = 1
                       AND NOT EXISTS (SELECT 1
                                       FROM CLIENTES otro
                                       WHERE otro.deleted_at IS NULL
                                         AND otro.id_cliente <> c.id_cliente
                                         AND otro.numero_documento = cand.normalizado));
                """);

            migrationBuilder.Sql(
                """
                UPDATE CLIENTES
                SET email = NULLIF(LOWER(LTRIM(RTRIM(email))), ''),
                    telefono = NULLIF(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(telefono)), ' ', ''), '-', ''), '(', ''), ')', ''), '.', ''), '')
                WHERE email IS NOT NULL OR telefono IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // La normalización no es reversible (no se guardó cómo estaban escritos los separadores) ni hace falta
            // revertirla: los valores canónicos son válidos también para el esquema anterior.
        }
    }
}
