using System.Security.Cryptography;
using System.Text;

namespace BillSoft.Infrastructure.Auth;

public static class RefreshTokenHash
{
    public static string Compute(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
