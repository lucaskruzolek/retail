namespace Retail.Application.Interfaces.Infrastructure;

/// <summary>
/// Contrato para el hashing y verificación criptográfica de contraseñas de operadores (BCrypt / PBKDF2).
/// </summary>
public interface IPasswordHasher
{
    string HashPassword(string password);

    bool VerifyPassword(string password, string passwordHash);
}
