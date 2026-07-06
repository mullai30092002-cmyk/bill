using System.Security.Cryptography;
using System.Text;

namespace BillSoft.Domain.Common;

public static class DeterministicGuid
{
    public static Guid FromString(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Trim();
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(normalized));

        return new Guid(bytes);
    }
}
