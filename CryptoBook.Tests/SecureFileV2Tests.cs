using CryptoBook.Security;

using System.IO;
using System.Security.Cryptography;
using System.Text;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class SecureFileV2Tests: IDisposable
    {
        private readonly string _directory =
            Path.Combine(Path.GetTempPath(), "CryptoBook.Tests", Guid.NewGuid().ToString("N"));

        public SecureFileV2Tests()
        {
            Directory.CreateDirectory(_directory);
        }

        [Fact]
        public async Task V2_RoundTrip_PreservesMultiChunkFile()
        {
            byte[] content = RandomNumberGenerator.GetBytes(12_345);
            string source = CreateSource("document.bin", content);
            string encrypted = Path.Combine(_directory, "document.cbook");
            string outputBase = Path.Combine(_directory, "restored");
            var (processor, _) = CreateProcessor("correct horse battery staple");

            await processor.EncryptFileAsync(source, encrypted);
            await processor.DecryptFileAsyncToFile(encrypted, outputBase);

            Assert.Equal(content, await File.ReadAllBytesAsync(outputBase + ".bin"));
        }

        [Fact]
        public async Task V2_EncryptStream_DoesNotRequirePlaintextFile()
        {
            byte[] content = Encoding.UTF8.GetBytes(
                "document kept only in memory");
            string encrypted =
                Path.Combine(_directory, "memory.cbook");
            var (processor, _) = CreateProcessor("stream password");
            await using var source =
                new MemoryStream(content, writable: false);

            await processor.EncryptStreamAsync(
                source,
                ".XamlPackage",
                encrypted);
            await using DecryptedFileContent decryptedContent =
                await processor.DecryptFileContentAsync(encrypted);
            Stream decrypted = decryptedContent.Content;
            using var restored = new MemoryStream();
            await decrypted.CopyToAsync(restored);

            Assert.Equal(content, restored.ToArray());
            Assert.Equal(".XamlPackage", decryptedContent.OriginalExtension);
            Assert.DoesNotContain(
                Directory.EnumerateFiles(_directory),
                path => path.EndsWith(".tmp"));
        }

        [Fact]
        public async Task V2_EncryptStream_ReplacingPlaintext_DoesNotLeavePlaintextBackup()
        {
            byte[] content = Encoding.UTF8.GetBytes("protected document");
            string target = CreateSource(
                "document.XamlPackage",
                Encoding.UTF8.GetBytes("previous plaintext"));
            var (processor, _) = CreateProcessor("stream password");
            await using var source = new MemoryStream(content, writable: false);

            await processor.EncryptStreamAsync(
                source,
                ".XamlPackage",
                target);

            Assert.False(File.Exists(target + ".bak"));
            await using DecryptedFileContent decrypted =
                await processor.DecryptFileContentAsync(target);
            using var restored = new MemoryStream();
            await decrypted.Content.CopyToAsync(restored);
            Assert.Equal(content, restored.ToArray());
        }

        [Fact]
        public async Task V2_EncryptStream_ReplacingEncryptedFile_PreservesEncryptedBackup()
        {
            byte[] firstContent = Encoding.UTF8.GetBytes("first version");
            byte[] secondContent = Encoding.UTF8.GetBytes("second version");
            string target = Path.Combine(_directory, "document.cbook");
            var (processor, _) = CreateProcessor("stream password");

            await using(var first = new MemoryStream(firstContent, writable: false))
            {
                await processor.EncryptStreamAsync(
                    first,
                    ".XamlPackage",
                    target);
            }
            await using(var second = new MemoryStream(secondContent, writable: false))
            {
                await processor.EncryptStreamAsync(
                    second,
                    ".XamlPackage",
                    target);
            }

            string backup = target + ".bak";
            Assert.True(File.Exists(backup));
            Assert.True(await new SecureFileValidator()
                .HasCryptoBookHeaderAsync(backup));
            await using DecryptedFileContent decrypted =
                await processor.DecryptFileContentAsync(backup);
            using var restored = new MemoryStream();
            await decrypted.Content.CopyToAsync(restored);
            Assert.Equal(firstContent, restored.ToArray());
        }

        [Fact]
        public async Task V2_EncryptFile_CanReplaceSourceFile()
        {
            byte[] content = Encoding.UTF8.GetBytes(
                "replace the source without locking it");
            string source = CreateSource("replace.txt", content);
            var (processor, _) = CreateProcessor("replace password");

            await processor.EncryptFileAsync(source, source);

            await using Stream decrypted =
                await processor.DecryptFileAsyncToStream(source);
            using var restored = new MemoryStream();
            await decrypted.CopyToAsync(restored);

            Assert.Equal(content, restored.ToArray());
            Assert.False(File.Exists(source + ".bak"));
            Assert.DoesNotContain(
                Directory.EnumerateFiles(_directory),
                path => path.EndsWith(".tmp"));
        }

        [Fact]
        public async Task V2_WrongPassword_IsRejected()
        {
            string source = CreateSource("secret.txt", Encoding.UTF8.GetBytes("classified"));
            string encrypted = Path.Combine(_directory, "secret.cbook");
            var (processor, provider) = CreateProcessor("right password");
            await processor.EncryptFileAsync(source, encrypted);
            provider.SetKey("wrong password");

            await Assert.ThrowsAnyAsync<CryptographicException>(
                () => processor.DecryptFileAsyncToStream(encrypted));
        }

        [Fact]
        public async Task V2_ModifiedCiphertext_IsRejected()
        {
            string source = CreateSource("secret.txt", Encoding.UTF8.GetBytes("classified"));
            string encrypted = Path.Combine(_directory, "secret.cbook");
            var (processor, _) = CreateProcessor("password");
            await processor.EncryptFileAsync(source, encrypted);

            byte[] bytes = await File.ReadAllBytesAsync(encrypted);
            bytes[75] ^= 0x40;
            await File.WriteAllBytesAsync(encrypted, bytes);

            await Assert.ThrowsAnyAsync<CryptographicException>(
                () => processor.DecryptFileAsyncToStream(encrypted));
        }

        [Fact]
        public async Task V2_TruncatedFile_IsRejected()
        {
            string source = CreateSource("secret.txt", Encoding.UTF8.GetBytes("classified"));
            string encrypted = Path.Combine(_directory, "secret.cbook");
            var (processor, _) = CreateProcessor("password");
            await processor.EncryptFileAsync(source, encrypted);

            byte[] bytes = await File.ReadAllBytesAsync(encrypted);
            await File.WriteAllBytesAsync(encrypted, bytes[..^1]);

            await Assert.ThrowsAnyAsync<IOException>(
                () => processor.DecryptFileAsyncToStream(encrypted));
        }

        [Fact]
        public async Task V2_MediaStream_IsSeekableAndDoesNotCreatePlaintextFile()
        {
            byte[] content = RandomNumberGenerator.GetBytes(20_000);
            string source = CreateSource("video.mp4", content);
            string encrypted = Path.Combine(_directory, "video.cbook");
            var (processor, _) = CreateProcessor("media password");
            await processor.EncryptFileAsync(source, encrypted);
            File.Delete(source);

            await using DecryptedFileContent decrypted =
                await processor.OpenDecryptedMediaStreamAsync(
                    encrypted,
                    legacyMemoryLimitBytes: 1024);
            Stream stream = decrypted.Content;

            Assert.True(stream.CanSeek);
            Assert.Equal(content.Length, stream.Length);
            Assert.Equal(".mp4", decrypted.OriginalExtension);

            foreach(int offset in new[] { 0, 4_090, 4_096, 8_187, 16_500 })
            {
                stream.Position = offset;
                byte[] actual = new byte[Math.Min(700, content.Length - offset)];
                await stream.ReadExactlyAsync(actual);
                Assert.Equal(content.AsSpan(offset, actual.Length).ToArray(), actual);
            }

            Assert.Equal([encrypted], Directory.GetFiles(_directory));
        }

        [Fact]
        public async Task V2_MediaStream_AuthenticatesBlockBeforeReturningIt()
        {
            byte[] content = RandomNumberGenerator.GetBytes(16_000);
            string source = CreateSource("video.mp4", content);
            string encrypted = Path.Combine(_directory, "video.cbook");
            var (processor, _) = CreateProcessor("media password");
            await processor.EncryptFileAsync(source, encrypted);

            byte[] bytes = await File.ReadAllBytesAsync(encrypted);
            const int headerSize = 66;
            const int recordSize = 5 + 4096 + 16;
            bytes[headerSize + 2 * recordSize + 5 + 20] ^= 0x20;
            await File.WriteAllBytesAsync(encrypted, bytes);

            await using DecryptedFileContent decrypted =
                await processor.OpenDecryptedMediaStreamAsync(
                    encrypted,
                    legacyMemoryLimitBytes: 1024);
            decrypted.Content.Position = 2 * 4096;

            Assert.ThrowsAny<CryptographicException>(() =>
                decrypted.Content.ReadByte());
        }

        [Fact]
        public async Task Legacy_MediaStream_RejectsPlaintextAboveMemoryLimit()
        {
            const string password = "legacy media password";
            byte[] content = RandomNumberGenerator.GetBytes(256);
            string encrypted = Path.Combine(_directory, "legacy.cbox");
            await CreateLegacyFileAsync(encrypted, ".mp4", content, password);
            var (processor, _) = CreateProcessor(password);

            await Assert.ThrowsAsync<IOException>(() =>
                processor.OpenDecryptedMediaStreamAsync(
                    encrypted,
                    legacyMemoryLimitBytes: 128));
        }

        [Fact]
        public async Task Legacy_MediaStream_IsSeekableWithinMemoryLimit()
        {
            const string password = "legacy media password";
            byte[] content = RandomNumberGenerator.GetBytes(256);
            string encrypted = Path.Combine(_directory, "legacy.cbox");
            await CreateLegacyFileAsync(encrypted, ".mp4", content, password);
            var (processor, _) = CreateProcessor(password);

            await using DecryptedFileContent decrypted =
                await processor.OpenDecryptedMediaStreamAsync(
                    encrypted,
                    legacyMemoryLimitBytes: 512);
            decrypted.Content.Position = 123;
            byte[] actual = new byte[50];
            await decrypted.Content.ReadExactlyAsync(actual);

            Assert.Equal(".mp4", decrypted.OriginalExtension);
            Assert.Equal(content.AsSpan(123, actual.Length).ToArray(), actual);
        }

        [Fact]
        public async Task Validator_RecognizesV2Header()
        {
            string source = CreateSource("secret.txt", Encoding.UTF8.GetBytes("classified"));
            string encrypted = Path.Combine(_directory, "secret.cbook");
            var (processor, _) = CreateProcessor("password");
            await processor.EncryptFileAsync(source, encrypted);

            SecureFileValidator validator = new();

            Assert.True(await validator.HasCryptoBookHeaderAsync(encrypted));
        }

        [Fact]
        public async Task Processor_StillDecryptsLegacyV1()
        {
            const string password = "legacy password";
            byte[] content = Encoding.UTF8.GetBytes("legacy content");
            string encrypted = Path.Combine(_directory, "legacy.cbox");
            await CreateLegacyFileAsync(encrypted, ".txt", content, password);
            var (processor, _) = CreateProcessor(password);

            await using Stream decrypted = await processor.DecryptFileAsyncToStream(encrypted);
            using MemoryStream copy = new();
            await decrypted.CopyToAsync(copy);

            Assert.Equal(content, copy.ToArray());
        }

        private (SecureFileProcessor Processor, TestKeyProvider Provider) CreateProcessor(string password)
        {
            SecureFileV2Options options = new()
            {
                KeyDerivation = new KeyDerivationParameters(
                    Iterations: 1,
                    MemorySizeKiB: 8 * 1024,
                    DegreeOfParallelism: 1,
                    OutputLength: 32),
                ChunkSize = 4096
            };
            TestKeyProvider provider = new(new Argon2idKeyDeriver());
            provider.SetKey(password);
            SecureFileV2Codec codec = new(provider, options);
            LegacySecureFileCodec legacyCodec = new(provider);
            return (new SecureFileProcessor(codec, legacyCodec), provider);
        }

        private string CreateSource(string name, byte[] content)
        {
            string path = Path.Combine(_directory, name);
            File.WriteAllBytes(path, content);
            return path;
        }

        private static async Task CreateLegacyFileAsync(
            string path,
            string extension,
            byte[] content,
            string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            byte[] key = Rfc2898DeriveBytes.Pbkdf2(
                passwordBytes,
                salt,
                100_000,
                HashAlgorithmName.SHA256,
                32);

            try
            {
                using Aes aes = Aes.Create();
                aes.Key = key;
                aes.GenerateIV();
                await using MemoryStream authenticatedContent = new();
                await authenticatedContent.WriteAsync(Encoding.ASCII.GetBytes("CRYPTOBOOK"));
                await authenticatedContent.WriteAsync(salt);
                await authenticatedContent.WriteAsync(aes.IV);

                await using(CryptoStream crypto = new(
                    authenticatedContent,
                    aes.CreateEncryptor(),
                    CryptoStreamMode.Write,
                    leaveOpen: true))
                {
                    byte[] extensionBytes = Encoding.UTF8.GetBytes(extension);
                    await crypto.WriteAsync(BitConverter.GetBytes(extensionBytes.Length));
                    await crypto.WriteAsync(extensionBytes);
                    await crypto.WriteAsync(content);
                    crypto.FlushFinalBlock();
                }

                byte[] authenticatedBytes = authenticatedContent.ToArray();
                using HMACSHA256 hmac = new(key);
                byte[] tag = hmac.ComputeHash(authenticatedBytes);
                await using FileStream output = File.Create(path);
                await output.WriteAsync(authenticatedBytes);
                await output.WriteAsync(tag);
            } finally
            {
                CryptographicOperations.ZeroMemory(passwordBytes);
                CryptographicOperations.ZeroMemory(key);
            }
        }

        public void Dispose()
        {
            if(Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        private sealed class TestKeyProvider: IKeyProvider, IDisposable
        {
            private readonly IPasswordKeyDeriver _deriver;
            private byte[]? _password;

            public TestKeyProvider(IPasswordKeyDeriver deriver)
            {
                _deriver = deriver;
            }

            public bool HasKey => _password is { Length: > 0 };

            public void SetKey(ReadOnlySpan<char> password)
            {
                Clear();
                char[] characters = password.ToArray();
                try
                {
                    _password = Encoding.UTF8.GetBytes(characters);
                } finally
                {
                    CryptographicOperations.ZeroMemory(
                        System.Runtime.InteropServices.MemoryMarshal.AsBytes(
                            characters.AsSpan()));
                }
            }

            public byte[] DeriveKey(byte[] salt)
            {
                if(_password is null)
                    throw new InvalidOperationException();
                return Rfc2898DeriveBytes.Pbkdf2(
                    _password,
                    salt,
                    100_000,
                    HashAlgorithmName.SHA256,
                    32);
            }

            public Task<byte[]> DeriveKeyAsync(
                ReadOnlyMemory<byte> salt,
                KeyDerivationParameters parameters,
                CancellationToken cancellationToken = default)
            {
                if(_password is null)
                    throw new InvalidOperationException();
                return _deriver.DeriveAsync(_password, salt, parameters, cancellationToken);
            }

            public void Clear()
            {
                if(_password is not null)
                    CryptographicOperations.ZeroMemory(_password);
                _password = null;
            }

            public void Dispose() => Clear();
        }
    }
}
