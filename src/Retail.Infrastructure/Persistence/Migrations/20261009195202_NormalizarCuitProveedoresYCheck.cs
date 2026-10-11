using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lleva los CUIT de proveedores a la forma canónica (11 dígitos sin guiones), normaliza email y teléfono, y
    /// agrega CK_PROVEEDORES_Cuit como defensa en profundidad.
    /// </summary>
    /// <remarks>
    /// Corrige el origen del bug P-1: el CUIT se guardaba tal como se escribía y "20-12345678-6" no coincidía con
    /// "20123456786", así que el mismo proveedor podía darse de alta dos veces. Igual que en
    /// NormalizarDocumentosYContactoClientes, un CUIT no se normaliza si chocaría con otro proveedor activo: esos
    /// duplicados reales los resuelve una persona.
    /// El scaffold creaba el CHECK verificando las filas existentes; si quedara alguna sin normalizar, la migración
    /// fallaría y la aplicación no arrancaría (MigrateAsync corre al iniciar). Por eso se crea WITH NOCHECK: valida
    /// todo INSERT y UPDATE nuevo, y SQL Server lo marca como "no confiable" hasta que se revise con
    /// ALTER TABLE PROVEEDORES WITH CHECK CHECK CONSTRAINT CK_PROVEEDORES_Cuit.
    /// </remarks>
    public partial class NormalizarCuitProveedoresYCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH candidatos AS (
                    SELECT p.id_proveedor,
                           p.deleted_at,
                           n.normalizado,
                           ROW_NUMBER() OVER (PARTITION BY n.normalizado, CASE WHEN p.deleted_at IS NULL THEN 0 ELSE p.id_proveedor END
                                              ORDER BY p.id_proveedor) AS orden
                    FROM PROVEEDORES p
                    CROSS APPLY (SELECT REPLACE(REPLACE(REPLACE(p.cuit, '-', ''), ' ', ''), '.', '') AS normalizado) n
                    WHERE p.cuit <> n.normalizado
                )
                UPDATE p
                SET cuit = cand.normalizado
                FROM PROVEEDORES p
                JOIN candidatos cand ON cand.id_proveedor = p.id_proveedor
                WHERE cand.deleted_at IS NOT NULL
                   OR (cand.orden = 1
                       AND NOT EXISTS (SELECT 1
                                       FROM PROVEEDORES otro
                                       WHERE otro.deleted_at IS NULL
                                         AND otro.id_proveedor <> p.id_proveedor
                                         AND otro.cuit = cand.normalizado));
                """);

            migrationBuilder.Sql(
                """
                UPDATE PROVEEDORES
                SET email = NULLIF(LOWER(LTRIM(RTRIM(email))), ''),
                    telefono = NULLIF(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(telefono)), ' ', ''), '-', ''), '(', ''), ')', ''), '.', ''), '')
                WHERE email IS NOT NULL OR telefono IS NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE PROVEEDORES WITH NOCHECK
                ADD CONSTRAINT CK_PROVEEDORES_Cuit CHECK (LEN([cuit]) = 11 AND [cuit] NOT LIKE '%[^0-9]%');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PROVEEDORES_Cuit",
                table: "PROVEEDORES");
        }
    }
}
