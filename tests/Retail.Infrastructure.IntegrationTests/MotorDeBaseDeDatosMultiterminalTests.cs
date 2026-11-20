using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Retail.Application.Exceptions;
using Retail.Domain.Entities;
using Retail.Infrastructure.IntegrationTests.TestData;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Robustez del motor para varias terminales (Track 6.7, Paquete 6, H11): READ_COMMITTED_SNAPSHOT y traducción de los
/// deadlocks. Todas son deterministas y sin hilos: la "otra terminal" es una segunda conexión o un segundo contexto.
/// </summary>
public class MotorDeBaseDeDatosMultiterminalTests : PedidosPruebaBase
{
    private const string MigracionAnterior = "20261119234857_SesionesActivasYMantenimiento";
    private static readonly TimeSpan EsperaGenerosa = TimeSpan.FromSeconds(60);

    public MotorDeBaseDeDatosMultiterminalTests()
        : base("MotorMulti")
    {
    }

    [Fact]
    public async Task BaseMigrada_ReadCommittedSnapshot_EstaActivado()
    {
        // Act
        var activado = await LeerRcsiAsync(Contexto);

        // Assert
        activado.Should().Be(1);
    }

    [Fact]
    public async Task MigrarDesdeEsquemaAnterior_ActivaReadCommittedSnapshot()
    {
        // Arrange: una base en el esquema anterior y con RCSI apagado.
        var nombre = $"RetailDb_Test_MigRcsi_{Guid.NewGuid():N}";
        var opciones = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(ServidorDePruebas.CadenaDeConexion(nombre))
            .Options;
        await using var contexto = new RetailDbContext(opciones);

        try
        {
            var migrador = contexto.GetService<IMigrator>();
            await migrador.MigrateAsync(MigracionAnterior);

            // EF Core ya crea las bases nuevas con RCSI activo; se lo apaga para reproducir una base que no nació de EF
            // (por ejemplo, una copia restaurada de otra instalación).
            await contexto.Database.ExecuteSqlRawAsync("ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT OFF WITH ROLLBACK IMMEDIATE;");
            (await LeerRcsiAsync(contexto)).Should().Be(0);

            // Act
            await migrador.MigrateAsync();

            // Assert
            (await LeerRcsiAsync(contexto)).Should().Be(1);

            // Y el Down lo apaga (ALTER DATABASE fuera de transacción).
            await migrador.MigrateAsync(MigracionAnterior);
            (await LeerRcsiAsync(contexto)).Should().Be(0);
        }
        finally
        {
            await contexto.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task Lectura_ConEscritorSinConfirmar_NoSeBloqueaYVeLaUltimaVersionConfirmada()
    {
        // Arrange
        var rubro = Rubro.Crear("Original");
        Contexto.Rubros.Add(rubro);
        await Contexto.SaveChangesAsync();
        var cadena = Contexto.Database.GetConnectionString()!;

        await using var conexionEscritor = new SqlConnection(cadena);
        await conexionEscritor.OpenAsync();
        await using var transaccionEscritor = (SqlTransaction)await conexionEscritor.BeginTransactionAsync();
        await using (var actualizar = conexionEscritor.CreateCommand())
        {
            actualizar.Transaction = transaccionEscritor;
            actualizar.CommandText = "UPDATE RUBROS SET nombre_rubro = 'Sin confirmar' WHERE id_rubro = @id";
            actualizar.Parameters.AddWithValue("@id", rubro.Id);
            await actualizar.ExecuteNonQueryAsync();
        }

        // Act: sin RCSI esta lectura esperaría el bloqueo del escritor y vencería por tiempo.
        await using var conexionLector = new SqlConnection(cadena);
        await conexionLector.OpenAsync();
        await using var leer = conexionLector.CreateCommand();
        leer.CommandTimeout = 5;
        leer.CommandText = "SELECT nombre_rubro FROM RUBROS WHERE id_rubro = @id";
        leer.Parameters.AddWithValue("@id", rubro.Id);
        var leido = await leer.ExecuteScalarAsync();

        await transaccionEscritor.RollbackAsync();

        // Assert
        leido.Should().Be("Original");
    }

    [Fact]
    public async Task SaveChangesAsync_ConDeadlockReal_UnaVictimaRecibeConflictoYLaOtraTermina()
    {
        // Arrange: dos rubros y dos "terminales" con la misma pareja cargada, antes de abrir las transacciones.
        var rubro1 = Rubro.Crear("Rubro uno");
        var rubro2 = Rubro.Crear("Rubro dos");
        Contexto.Rubros.AddRange(rubro1, rubro2);
        await Contexto.SaveChangesAsync();

        await using var contextoA = NuevoContexto();
        await using var contextoB = NuevoContexto();
        using var terminalA = new UnitOfWork(contextoA);
        using var terminalB = new UnitOfWork(contextoB);
        var aRubro1 = await contextoA.Rubros.SingleAsync(r => r.Id == rubro1.Id);
        var aRubro2 = await contextoA.Rubros.SingleAsync(r => r.Id == rubro2.Id);
        var bRubro1 = await contextoB.Rubros.SingleAsync(r => r.Id == rubro1.Id);
        var bRubro2 = await contextoB.Rubros.SingleAsync(r => r.Id == rubro2.Id);

        await terminalA.BeginTransactionAsync();
        await terminalB.BeginTransactionAsync();

        aRubro1.Renombrar("A toma uno");
        await terminalA.SaveChangesAsync();
        bRubro2.Renombrar("B toma dos");
        await terminalB.SaveChangesAsync();

        // Act: A pide el rubro 2 (queda esperando a B) y B pide el rubro 1 (espera a A): ciclo. El motor elige una víctima.
        aRubro2.Renombrar("A pide dos");
        var tareaA = terminalA.SaveChangesAsync();
        bRubro1.Renombrar("B pide uno");
        var tareaB = terminalB.SaveChangesAsync();

        var resultados = await Task.WhenAll(
            Resultado(tareaA).WaitAsync(EsperaGenerosa),
            Resultado(tareaB).WaitAsync(EsperaGenerosa));

        // Assert: exactamente una falló, con la excepción de conflicto y el mensaje nuevo; la otra guardó.
        var fallas = resultados.Where(r => r is not null).ToList();
        fallas.Should().ContainSingle();
        fallas[0].Should().BeOfType<ConflictoDeConcurrenciaException>(fallas[0]!.ToString());
        fallas[0]!.Message.Should().Be(UnitOfWork.MensajeDeadlock);
        fallas[0]!.InnerException.Should().NotBeNull();
        resultados.Count(r => r is null).Should().Be(1);

        await terminalA.RollbackTransactionAsync();
        await terminalB.RollbackTransactionAsync();
    }

    private static async Task<Exception?> Resultado(Task tarea)
    {
        try
        {
            await tarea;
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private static async Task<int> LeerRcsiAsync(RetailDbContext contexto)
    {
        return await contexto.Database
            .SqlQueryRaw<int>("SELECT CAST(is_read_committed_snapshot_on AS int) AS [Value] FROM sys.databases WHERE name = DB_NAME()")
            .SingleAsync();
    }
}
