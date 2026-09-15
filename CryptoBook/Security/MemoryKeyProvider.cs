using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace CryptoBook.Security;

internal sealed class MemoryKeyProvider(IPasswordKeyDeriver keyDeriver) : IKeyProvider, IDisposable
{
    private readonly object sync = new();
    private byte[]? protectedPassword;
    private int passwordLength;
    private bool canEncrypt;

    public bool HasKey { get { lock(sync) return protectedPassword is not null; } }
    public bool CanEncrypt { get { lock(sync) return canEncrypt && protectedPassword is not null; } }

    public void SetKey(ReadOnlySpan<char> password)
    {
        if(password.IsEmpty)
            throw new ArgumentException("Пароль не задан.", nameof(password));
        int length = Encoding.UTF8.GetByteCount(password);
        byte[] buffer = GC.AllocateArray<byte>(checked((length + 15) / 16 * 16), pinned: true);
        try
        {
            Encoding.UTF8.GetBytes(password, buffer);
            Transform(buffer, protect: true);
            lock(sync)
            {
                Clear();
                passwordLength = length;
                canEncrypt = EncryptionPasswordPolicy.IsStrong(password);
                protectedPassword = buffer;
            }
        }
        catch
        {
            CryptographicOperations.ZeroMemory(buffer);
            throw;
        }
    }

    private (byte[] Buffer, int Length) OpenPassword(bool requireStrong = false)
    {
        lock(sync)
        {
            if(protectedPassword is null)
                throw new InvalidOperationException("Ключ не задан.");
            if(requireStrong && !canEncrypt)
                throw new CryptographicException(CryptoBook.Infrastructure.LocalizationManager.GetString("Key.StrongPasswordRequired"));
            byte[] buffer = GC.AllocateArray<byte>(protectedPassword.Length, pinned: true);
            protectedPassword.CopyTo(buffer, 0);
            try
            {
                Transform(buffer, protect: false);
                return (buffer, passwordLength);
            }
            catch
            {
                CryptographicOperations.ZeroMemory(buffer);
                throw;
            }
        }
    }

    public byte[] DeriveKey(byte[] salt)
    {
        var password = OpenPassword();
        try
        {
            // V1 is read-only. Changing this parameter would break existing files.
            return Rfc2898DeriveBytes.Pbkdf2(password.Buffer.AsSpan(0, password.Length),
                salt, 100_000, HashAlgorithmName.SHA256, 32);
        }
        finally { CryptographicOperations.ZeroMemory(password.Buffer); }
    }

    public Task<byte[]> DeriveKeyAsync(ReadOnlyMemory<byte> salt,
        KeyDerivationParameters parameters, CancellationToken cancellationToken = default) =>
        DeriveAsync(salt, parameters, false, cancellationToken);

    public Task<byte[]> DeriveEncryptionKeyAsync(ReadOnlyMemory<byte> salt,
        KeyDerivationParameters parameters, CancellationToken cancellationToken = default) =>
        DeriveAsync(salt, parameters, true, cancellationToken);

    private async Task<byte[]> DeriveAsync(ReadOnlyMemory<byte> salt,
        KeyDerivationParameters parameters, bool requireStrong, CancellationToken cancellationToken)
    {
        var password = OpenPassword(requireStrong);
        try
        {
            return await keyDeriver.DeriveAsync(password.Buffer.AsMemory(0, password.Length),
                salt, parameters, cancellationToken).ConfigureAwait(false);
        }
        finally { CryptographicOperations.ZeroMemory(password.Buffer); }
    }

    public void Clear()
    {
        lock(sync)
        {
            if(protectedPassword is not null)
                CryptographicOperations.ZeroMemory(protectedPassword);
            protectedPassword = null;
            passwordLength = 0;
            canEncrypt = false;
        }
    }

    public void Dispose() => Clear();

    private static void Transform(byte[] buffer, bool protect)
    {
        IntPtr address = Marshal.UnsafeAddrOfPinnedArrayElement(buffer, 0);
        bool success = protect
            ? CryptProtectMemory(address, (uint)buffer.Length, 0)
            : CryptUnprotectMemory(address, (uint)buffer.Length, 0);
        if(!success)
            throw new CryptographicException("Не удалось защитить память ключа.",
                new Win32Exception(Marshal.GetLastWin32Error()));
    }

    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectMemory(IntPtr data, uint length, uint flags);

    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectMemory(IntPtr data, uint length, uint flags);
}
