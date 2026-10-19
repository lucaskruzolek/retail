using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarSpRegistrarIntentoLoginYAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE dbo.ACCESOS_USUARIOS (
                    id_usuario INT NOT NULL,
                    intentos_fallidos INT NOT NULL CONSTRAINT DF_ACCESOS_USUARIOS_intentos_fallidos DEFAULT 0,
                    bloqueado_hasta DATETIME2 NULL,
                    ultimo_acceso DATETIME2 NULL,
                    CONSTRAINT PK_ACCESOS_USUARIOS PRIMARY KEY CLUSTERED (id_usuario),
                    CONSTRAINT FK_ACCESOS_USUARIOS_USUARIOS FOREIGN KEY (id_usuario) REFERENCES dbo.USUARIOS (id_usuario) ON DELETE CASCADE
                );
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE dbo.AUDITORIA_ACCESOS (
                    id_auditoria BIGINT IDENTITY(1,1) NOT NULL,
                    fecha_hora DATETIME2 NOT NULL,
                    nombre_usuario NVARCHAR(50) NOT NULL,
                    id_usuario INT NULL,
                    resultado TINYINT NOT NULL,
                    nombre_equipo NVARCHAR(100) NULL,
                    CONSTRAINT PK_AUDITORIA_ACCESOS PRIMARY KEY CLUSTERED (id_auditoria),
                    CONSTRAINT FK_AUDITORIA_ACCESOS_USUARIOS FOREIGN KEY (id_usuario) REFERENCES dbo.USUARIOS (id_usuario) ON DELETE SET NULL
                );

                CREATE NONCLUSTERED INDEX IX_AUDITORIA_ACCESOS_fecha_hora ON dbo.AUDITORIA_ACCESOS (fecha_hora DESC);
                CREATE NONCLUSTERED INDEX IX_AUDITORIA_ACCESOS_nombre_usuario ON dbo.AUDITORIA_ACCESOS (nombre_usuario);
            ");

            migrationBuilder.Sql(@"
                CREATE OR ALTER PROCEDURE dbo.sp_RegistrarIntentoLogin
                    @nombre_usuario       NVARCHAR(50),
                    @id_usuario           INT = NULL,
                    @credenciales_validas BIT,
                    @max_intentos         INT,
                    @minutos_bloqueo      INT,
                    @equipo               NVARCHAR(100) = NULL,
                    @resultado            TINYINT   OUTPUT,
                    @intentos_restantes   INT       OUTPUT,
                    @bloqueado_hasta      DATETIME2 OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;

                    DECLARE @ahora DATETIME2 = SYSUTCDATETIME();
                    DECLARE @fallos_actuales INT = 0;
                    DECLARE @bloqueo_previo DATETIME2 = NULL;

                    BEGIN TRANSACTION;

                    IF @id_usuario IS NOT NULL
                    BEGIN
                        IF NOT EXISTS (SELECT 1 FROM dbo.ACCESOS_USUARIOS WITH (UPDLOCK, HOLDLOCK) WHERE id_usuario = @id_usuario)
                        BEGIN
                            INSERT INTO dbo.ACCESOS_USUARIOS (id_usuario, intentos_fallidos, bloqueado_hasta, ultimo_acceso)
                            VALUES (@id_usuario, 0, NULL, NULL);
                        END

                        SELECT 
                            @fallos_actuales = intentos_fallidos,
                            @bloqueo_previo = bloqueado_hasta
                        FROM dbo.ACCESOS_USUARIOS WITH (UPDLOCK, HOLDLOCK)
                        WHERE id_usuario = @id_usuario;

                        IF @bloqueo_previo IS NOT NULL AND @bloqueo_previo > @ahora
                        BEGIN
                            SET @resultado = 2;
                            SET @intentos_restantes = 0;
                            SET @bloqueado_hasta = @bloqueo_previo;
                        END
                        ELSE IF @credenciales_validas = 1
                        BEGIN
                            UPDATE dbo.ACCESOS_USUARIOS
                            SET intentos_fallidos = 0,
                                bloqueado_hasta = NULL,
                                ultimo_acceso = @ahora
                            WHERE id_usuario = @id_usuario;

                            SET @resultado = 0;
                            SET @intentos_restantes = @max_intentos;
                            SET @bloqueado_hasta = NULL;
                        END
                        ELSE
                        BEGIN
                            IF @bloqueo_previo IS NOT NULL AND @bloqueo_previo <= @ahora
                            BEGIN
                                SET @fallos_actuales = 0;
                            END

                            SET @fallos_actuales = @fallos_actuales + 1;

                            IF @fallos_actuales >= @max_intentos
                            BEGIN
                                SET @bloqueado_hasta = DATEADD(MINUTE, @minutos_bloqueo, @ahora);
                                UPDATE dbo.ACCESOS_USUARIOS
                                SET intentos_fallidos = @fallos_actuales,
                                    bloqueado_hasta = @bloqueado_hasta
                                WHERE id_usuario = @id_usuario;

                                SET @resultado = 2;
                                SET @intentos_restantes = 0;
                            END
                            ELSE
                            BEGIN
                                UPDATE dbo.ACCESOS_USUARIOS
                                SET intentos_fallidos = @fallos_actuales,
                                    bloqueado_hasta = NULL
                                WHERE id_usuario = @id_usuario;

                                SET @resultado = 1;
                                SET @intentos_restantes = @max_intentos - @fallos_actuales;
                                SET @bloqueado_hasta = NULL;
                            END
                        END
                    END
                    ELSE
                    BEGIN
                        SET @resultado = 1;
                        SET @intentos_restantes = @max_intentos;
                        SET @bloqueado_hasta = NULL;
                    END

                    INSERT INTO dbo.AUDITORIA_ACCESOS (fecha_hora, nombre_usuario, id_usuario, resultado, nombre_equipo)
                    VALUES (@ahora, @nombre_usuario, @id_usuario, @resultado, @equipo);

                    COMMIT TRANSACTION;
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_RegistrarIntentoLogin;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS dbo.AUDITORIA_ACCESOS;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS dbo.ACCESOS_USUARIOS;");
        }
    }
}
