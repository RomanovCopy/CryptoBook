using CryptoBook.Security;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace CryptoBook.Tests;

public sealed class KeyProtectionTests
{
    [Theory]
    [InlineData("1234", false)]
    [InlineData("R7!mQ2#", false)]
    [InlineData("R7!mQ2#v", true)]
    [InlineData("кедрлуна", true)]
    [InlineData("12345678", false)]
    [InlineData("password", false)]
    [InlineData("12345678901234567890", false)]
    [InlineData("PasswordPassword123!", false)]
    [InlineData("AbcdefghAbcdefgh", false)]
    [InlineData("xY7!qZ9@xY7!qZ9@", false)]
    [InlineData("amber meadow violin 82", true)]
    [InlineData("тихий берег алмаз 47", true)]
    [InlineData("R7!mQ2#vL9@tZ4%k", true)]
    public void NewEncryptionPasswordPolicy(string password, bool accepted) =>
        Assert.Equal(accepted, EncryptionPasswordPolicy.IsStrong(password));

    [Fact]
    public async Task EightCharacterPassword_CanEncryptAndDecryptWithProductionKeyProvider()
    {
        using var provider = new MemoryKeyProvider(new Argon2idKeyDeriver());
        provider.SetKey("R7!mQ2#v");
        Assert.True(provider.CanEncrypt);
        var codec = new SecureFileV2Codec(provider, new SecureFileV2Options());
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".cbook");
        try
        {
            using var source = new MemoryStream("roundtrip with eight characters"u8.ToArray());
            await codec.EncryptStreamAsync(source, ".txt", path);
            provider.Clear();
            provider.SetKey("R7!mQ2#v");
            await using var decrypted = await codec.DecryptFileContentAsync(path);
            using var output = new MemoryStream();
            await decrypted.Content.CopyToAsync(output);
            Assert.Equal("roundtrip with eight characters"u8.ToArray(), output.ToArray());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void MemoryKey_IsProtectedAtRest_AndLegacyDerivationStaysCompatible()
    {
        using var provider = new MemoryKeyProvider(new Argon2idKeyDeriver());
        byte[] password = Encoding.UTF8.GetBytes("known test password 81");
        provider.SetKey("known test password 81");
        byte[] storage = (byte[])typeof(MemoryKeyProvider).GetField("protectedPassword",
            BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(provider)!;
        Assert.False(storage.AsSpan().IndexOf(password) >= 0);
        byte[] salt = new byte[16];
        Assert.Equal(Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32),
            provider.DeriveKey(salt));
        provider.Clear();
        Assert.All(storage, value => Assert.Equal(0, value));
        Assert.False(provider.HasKey);
        Assert.False(provider.CanEncrypt);
        Assert.Throws<InvalidOperationException>(() => provider.DeriveKey(salt));
    }

    [Fact]
    public async Task DerivationOwnsAndClearsItsPlaintext_WhenKeyChangesDuringOperation()
    {
        var deriver = new SuspendedDeriver();
        using var provider = new MemoryKeyProvider(deriver);
        provider.SetKey("original secret value");
        Task<byte[]> pending = provider.DeriveKeyAsync(new byte[16], KeyDerivationParameters.SecureFileV2);
        provider.SetKey("replacement secret value");
        Assert.Equal("original secret value", Encoding.UTF8.GetString(deriver.Password.Span));
        deriver.Completion.SetException(new CryptographicException("synthetic failure"));
        await Assert.ThrowsAsync<CryptographicException>(() => pending);
        Assert.All(deriver.Password.ToArray(), value => Assert.Equal(0, value));
        Assert.True(provider.HasKey);
    }

    [Fact]
    public async Task WeakPasswordCanBeSetForReading_ButCannotCreateNewEncryptedFiles()
    {
        using var provider = new MemoryKeyProvider(new Argon2idKeyDeriver());
        provider.SetKey("1234");
        Assert.True(provider.HasKey);
        Assert.False(provider.CanEncrypt);
        var codec = new SecureFileV2Codec(provider, new SecureFileV2Options());
        using var source = new MemoryStream("example"u8.ToArray());
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".cbook");
        await Assert.ThrowsAsync<CryptographicException>(() => codec.EncryptStreamAsync(source, ".txt", path));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task PasswordSnapshot_AuthenticatesHeaderCiphertextAndPassword()
    {
        using var key = new MemoryKeyProvider(new Argon2idKeyDeriver());
        key.SetKey("old-key");
        var protector = new PasswordProtectedBuffer(key);
        byte[] encrypted = await protector.ProtectAsync("private document"u8.ToArray());
        Assert.Equal("private document"u8.ToArray(), await protector.UnprotectAsync(encrypted));
        foreach(int offset in new[] { 0, 8, 24, 36, encrypted.Length - 1 })
        {
            byte[] changed = encrypted.ToArray();
            changed[offset] ^= 1;
            await Assert.ThrowsAnyAsync<CryptographicException>(() => protector.UnprotectAsync(changed));
        }
        key.SetKey("wrong-key");
        await Assert.ThrowsAnyAsync<CryptographicException>(() => protector.UnprotectAsync(encrypted));
    }

    [Fact]
    public void SensitiveStream_ClearsOldAllocationOnGrowthAndCurrentAllocationOnDispose()
    {
        var stream = new SensitiveMemoryStream();
        stream.Write(new byte[] { 1, 2, 3 });
        byte[] old = stream.GetBuffer();
        stream.Capacity = old.Length + 1024;
        Assert.All(old, value => Assert.Equal(0, value));
        byte[] current = stream.GetBuffer();
        Assert.Equal(1, current[0]);
        stream.Dispose();
        Assert.All(current, value => Assert.Equal(0, value));
    }

    private sealed class SuspendedDeriver : IPasswordKeyDeriver
    {
        public ReadOnlyMemory<byte> Password { get; private set; }
        public TaskCompletionSource<byte[]> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<byte[]> DeriveAsync(ReadOnlyMemory<byte> password, ReadOnlyMemory<byte> salt,
            KeyDerivationParameters parameters, CancellationToken cancellationToken = default)
        {
            Password = password;
            return Completion.Task;
        }
    }
}
