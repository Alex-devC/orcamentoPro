using OrcPro.Application.Interfaces.Services;
using BCrypt.Net;

namespace OrcPro.Infrastructure.Security;

public class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 11;

    public string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("A senha não pode ser nula ou vazia.", nameof(password));

        return BCrypt.Net.BCrypt.EnhancedHashPassword(password, WorkFactor, HashType.SHA384);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(passwordHash))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.EnhancedVerify(password, passwordHash, HashType.SHA384);
        }
        catch
        {
            return false;
        }
    }
}
