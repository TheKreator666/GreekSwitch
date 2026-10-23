using System.Security.Cryptography;
using System.Text;

namespace GreekSwitch.Core.Decisions;

public interface IWordHasher
{
    /// <summary>A stable, non-reversible fingerprint of an already-normalised word.</summary>
    string Hash(string normalizedWord);
}

/// <summary>
/// HMAC-SHA256 with a secret key. Without the key, the hashes can't be checked against guesses,
/// so a copied settings file reveals nothing. The app keeps the key encrypted with Windows DPAPI.
/// </summary>
public sealed class HmacWordHasher : IWordHasher
{
    private readonly byte[] _key;

    public HmacWordHasher(byte[] key)
    {
        if (key.Length < 16)
        {
            throw new ArgumentException("The key must be at least 16 bytes.", nameof(key));
        }

        _key = (byte[])key.Clone();
    }

    /// <summary>A random key for this process only (learned words are then not persistable).</summary>
    public static HmacWordHasher Ephemeral() => new(RandomNumberGenerator.GetBytes(32));

    public string Hash(string normalizedWord) =>
        Convert.ToBase64String(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(normalizedWord)));
}
