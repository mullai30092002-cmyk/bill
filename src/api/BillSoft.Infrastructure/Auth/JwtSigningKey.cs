using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace BillSoft.Infrastructure.Auth;

public static class JwtSigningKey
{
    private const int MinimumSigningKeyBytes = 32;

    public static SymmetricSecurityKey Create(string signingKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signingKey);

        var keyBytes = Encoding.UTF8.GetBytes(signingKey);
        if (keyBytes.Length < MinimumSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"Jwt:SigningKey must be at least {MinimumSigningKeyBytes} bytes when encoded as UTF-8.");
        }

        return new SymmetricSecurityKey(keyBytes)
        {
            KeyId = BuildKeyId(keyBytes)
        };
    }

    private static string BuildKeyId(byte[] keyBytes)
    {
        var hash = SHA256.HashData(keyBytes);
        return Convert.ToHexString(hash);
    }
}
