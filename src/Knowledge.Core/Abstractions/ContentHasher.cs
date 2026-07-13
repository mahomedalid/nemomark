using System.Security.Cryptography;
using System.Text;

namespace Knowledge.Core.Abstractions;

/// <summary>Deterministic content hashing helper used to detect changes.</summary>
public static class ContentHasher
{
    public static string Hash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content ?? string.Empty));
        return Convert.ToHexString(bytes);
    }
}
