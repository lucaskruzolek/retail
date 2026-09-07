namespace Retail.Domain.Common;

/// <summary>
/// Clase base abstracta para todas las entidades del dominio con soporte de borrado lógico (Soft Delete).
/// </summary>
public abstract class BaseEntity
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedAt { get; set; }

    public bool IsDeleted => DeletedAt.HasValue;

    public void MarkAsDeleted()
    {
        DeletedAt = DateTime.UtcNow;
    }

    public void Restore()
    {
        DeletedAt = null;
    }
}
