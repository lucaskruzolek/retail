CREATE PROCEDURE dbo.sp_Login
    @NombreUsuario   NVARCHAR(50),
    @PasswordHash    VARBINARY(64),
    @IdUsuario       INT            OUTPUT,
    @NombreCompleto  NVARCHAR(200) OUTPUT,
    @Rol             INT            OUTPUT,
    @Resultado       INT            OUTPUT   -- 0 = OK, 1 = Credenciales inválidas, 2 = Usuario inactivo
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @StoredHash VARBINARY(64);

    SELECT  @IdUsuario      = u.Id,
            @NombreCompleto = u.NombreCompleto,
            @Rol            = u.IdRol,
            @StoredHash     = u.PasswordHash,
            @Resultado      = CASE
                                 WHEN u.IsDeleted = 1 THEN 2               -- usuario inactivo
                                 WHEN @StoredHash <> @PasswordHash THEN 1 -- credenciales inválidas
                                 ELSE 0                                   -- OK
                               END
    FROM dbo.Usuarios u
    WHERE u.NombreUsuario = @NombreUsuario;

    IF @Resultado IS NULL
        SET @Resultado = 1;   -- usuario no encontrado → credenciales inválidas
END
