using System.Security.Cryptography;

namespace CryptoBook.Security;

/// <summary>Versioned password-encrypted snapshots, independent of Windows-user keys.</summary>
public sealed class PasswordProtectedBuffer(IKeyProvider keyProvider)
{
    private static ReadOnlySpan<byte> Magic => "CBSNAP03"u8;
    private const int HeaderLength = 8 + 16 + 12;
    // These are format constants, independent of future file-encryption defaults.
    private static readonly KeyDerivationParameters Parameters = new(3, 64 * 1024, 4, 32);

    public static bool HasHeader(ReadOnlySpan<byte> data) => data.StartsWith(Magic);

    public async Task<byte[]> ProtectAsync(ReadOnlyMemory<byte> plaintext,
        CancellationToken cancellationToken = default)
    {
        byte[] result = new byte[checked(HeaderLength + plaintext.Length + 16)];
        Magic.CopyTo(result);
        RandomNumberGenerator.Fill(result.AsSpan(8, 28));
        byte[] key = await keyProvider.DeriveKeyAsync(result.AsMemory(8, 16),
            Parameters, cancellationToken);
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(result.AsSpan(24, 12), plaintext.Span,
                result.AsSpan(HeaderLength, plaintext.Length), result.AsSpan(HeaderLength + plaintext.Length, 16),
                result.AsSpan(0, HeaderLength));
            return result;
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    public async Task<byte[]> UnprotectAsync(ReadOnlyMemory<byte> ciphertext,
        CancellationToken cancellationToken = default)
    {
        if(ciphertext.Length < HeaderLength + 16 || !HasHeader(ciphertext.Span))
            throw new CryptographicException("Некорректный защищённый снимок.");
        byte[] key = await keyProvider.DeriveKeyAsync(ciphertext.Slice(8, 16),
            Parameters, cancellationToken);
        byte[] result = new byte[ciphertext.Length - HeaderLength - 16];
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(ciphertext.Span.Slice(24, 12), ciphertext.Span.Slice(HeaderLength, result.Length),
                ciphertext.Span[^16..], result, ciphertext.Span[..HeaderLength]);
            return result;
        }
        catch { CryptographicOperations.ZeroMemory(result); throw; }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
}
