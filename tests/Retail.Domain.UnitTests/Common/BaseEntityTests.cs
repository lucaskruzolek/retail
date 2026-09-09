using FluentAssertions;
using Retail.Domain.Common;
using Xunit;

namespace Retail.Domain.UnitTests.Common;

// Clase concreta de prueba para instanciar BaseEntity abstracta
internal sealed class DummyEntity : BaseEntity
{
}

public class BaseEntityTests
{
    [Fact]
    public void NuevaEntidad_DebeInicializarseNoEliminada_YConFechaCreacionUtc()
    {
        // Act
        var entidad = new DummyEntity
        {
            Id = 42
        };

        // Assert
        entidad.Id.Should().Be(42);
        entidad.IsDeleted.Should().BeFalse();
        entidad.DeletedAt.Should().BeNull();
        entidad.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void MarkAsDeleted_DebeMarcarEntidadComoEliminada_ConFechaUtc()
    {
        // Arrange
        var entidad = new DummyEntity();

        // Act
        entidad.MarkAsDeleted();

        // Assert
        entidad.IsDeleted.Should().BeTrue();
        entidad.DeletedAt.Should().NotBeNull();
        entidad.DeletedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Restore_EntidadEliminada_DebeRestablecerEstadoActivo()
    {
        // Arrange
        var entidad = new DummyEntity();
        entidad.MarkAsDeleted();

        // Act
        entidad.Restore();

        // Assert
        entidad.IsDeleted.Should().BeFalse();
        entidad.DeletedAt.Should().BeNull();
    }
}
