using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Nucleus.Infrastructure.Data;

/// <summary>
/// AES-256-CBC value converter that transparently encrypts sensitive strings at rest.
/// Format stored in DB: base64(IV[16 bytes] + ciphertext)
///
/// Call SetEncryptionKey() once at startup with a 32-byte key.
/// Set the CREDENTIAL_ENCRYPTION_KEY env var to a base64-encoded 32-byte key:
///   openssl rand -base64 32
/// </summary>
public sealed class EncryptedStringConverter : ValueConverter<string?, string?>
{
    private static byte[] _key = [];

    public static void SetEncryptionKey(byte[] key)
    {
        if (key.Length != 32)
            throw new ArgumentException("Encryption key must be exactly 32 bytes (256 bits) for AES-256.");
        _key = key;
    }

    public static bool IsConfigured => _key.Length == 32;

    public EncryptedStringConverter() : base(
        plaintext => plaintext == null ? null : Encrypt(plaintext),
        ciphertext => ciphertext == null ? null : Decrypt(ciphertext))
    { }

    private static string Encrypt(string plaintext)
    {
        if (!IsConfigured)
            throw new InvalidOperationException(
                "Credential encryption key is not set. Add CREDENTIAL_ENCRYPTION_KEY to Railway environment variables.");

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV(); // fresh 16-byte IV per encryption

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        // Store as: IV (16 bytes) || ciphertext → base64
        var combined = new byte[16 + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, combined, 0, 16);
        Buffer.BlockCopy(cipherBytes, 0, combined, 16, cipherBytes.Length);
        return Convert.ToBase64String(combined);
    }

    private static string Decrypt(string stored)
    {
        if (!IsConfigured)
            throw new InvalidOperationException(
                "Credential encryption key is not set. Add CREDENTIAL_ENCRYPTION_KEY to Railway environment variables.");

        try
        {
            var data = Convert.FromBase64String(stored);
            if (data.Length < 17) return stored; // too short to be encrypted — return as-is

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = data[..16];

            using var decryptor = aes.CreateDecryptor();
            var decrypted = decryptor.TransformFinalBlock(data, 16, data.Length - 16);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (CryptographicException)
        {
            // Data was stored before encryption was enabled — return raw value
            return stored;
        }
    }
}
