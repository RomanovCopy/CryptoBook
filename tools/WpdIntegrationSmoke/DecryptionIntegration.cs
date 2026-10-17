using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Services;

using System.Security.Cryptography;
using System.Text;

internal static class DecryptionIntegration
{
    public static async Task RunAsync(
        IStorageFacade storage,
        WpdStorageProvider provider,
        StorageLocation remoteFolder,
        string localRoot)
    {
        using var key = new IntegrationKeyProvider();
        var processor = new SecureFileProcessor(
            new SecureFileV2Codec(key, new SecureFileV2Options()),
            new LegacySecureFileCodec(key));
        var engine = new TransferEngine(storage);
        var export = new DecryptionExportService(
            processor,
            new SecureFileValidator(),
            new OriginalOnlyConversionService(),
            Path.Combine(localRoot, "decryption"),
            storage,
            engine);
        var options = new DecryptionOptions(
            EncryptionTargetMode.SaveAs,
            DecryptionOutputFormat.Original);
        (string Name, byte[] Content)[] fixtures =
        [
            ("Текст.txt", Encoding.UTF8.GetBytes(string.Concat(
                Enumerable.Repeat("CryptoBook: проверка расшифровки Windows ↔ Android.\r\n", 2048)))),
            ("Изображение.png", Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII="))
        ];

        foreach((string name, byte[] content) in fixtures)
        {
            string extension = Path.GetExtension(name);
            string encrypted = Path.Combine(localRoot, name + ".cbook");
            await using(var input = new MemoryStream(content, writable: false))
                await processor.EncryptStreamAsync(input, extension, encrypted);
            byte[] encryptedHash = SHA256.HashData(await File.ReadAllBytesAsync(encrypted));
            var remoteOutput = provider.GetChild(remoteFolder, name);
            string preparedDirectory;
            await using(PreparedDecryption prepared = await export.PrepareAsync(encrypted))
            {
                preparedDirectory = prepared.TemporaryDirectory;
                string published = await export.PublishAsync(prepared, options, remoteOutput.ToString());
                await VerifyRemoteAsync(storage.Resolve(published), content);
                string duplicate = await export.PublishAsync(prepared, options, remoteOutput.ToString());
                if(storage.AreEquivalent(storage.Resolve(published), storage.Resolve(duplicate)))
                    throw new IOException("A conflicting decrypted output was overwritten.");
                await VerifyRemoteAsync(storage.Resolve(duplicate), content);
            }
            EnsureWorkspaceRemoved(preparedDirectory);
            byte[] preservedHash = SHA256.HashData(await File.ReadAllBytesAsync(encrypted));
            if(!encryptedHash.SequenceEqual(preservedHash))
                throw new IOException("Windows protected source was changed.");
            Console.WriteLine($"Windows .cbook -> Android {extension}: SHA256 OK; conflict copy OK; source preserved");

            StorageLocation remoteSource = provider.GetChild(remoteFolder, name + ".cbook");
            FileOperationResult upload = await engine.CopyAsync(
                new StorageLocation(StorageLocation.LocalProviderId, encrypted), remoteSource);
            if(!upload.Success)
                throw new IOException(upload.ErrorMessage);
            await using(PreparedDecryption prepared = await export.PrepareAsync(remoteSource.ToString()))
            {
                preparedDirectory = prepared.TemporaryDirectory;
                string output = await export.PublishAsync(
                    prepared, options, Path.Combine(localRoot, "restored-" + name));
                byte[] restoredHash = SHA256.HashData(await File.ReadAllBytesAsync(output));
                if(!restoredHash.SequenceEqual(SHA256.HashData(content)))
                    throw new IOException("Android -> Windows decrypted content mismatch.");
            }
            EnsureWorkspaceRemoved(preparedDirectory);
            await using(Stream input = await provider.OpenRawReadAsync(remoteSource))
            {
                byte[] preservedRemoteHash = await SHA256.HashDataAsync(input);
                if(!encryptedHash.SequenceEqual(preservedRemoteHash))
                    throw new IOException("Android protected source was changed.");
            }
            Console.WriteLine($"Android .cbook -> Windows {extension}: SHA256 OK; source preserved; temporary files removed");
        }

        async Task VerifyRemoteAsync(StorageLocation location, byte[] content)
        {
            await using Stream input = await provider.OpenRawReadAsync(location);
            byte[] actualHash = await SHA256.HashDataAsync(input);
            if(!SHA256.HashData(content).SequenceEqual(actualHash))
                throw new IOException("Windows -> Android decrypted content mismatch.");
        }
    }

    private static void EnsureWorkspaceRemoved(string directory)
    {
        if(Directory.Exists(directory))
            throw new IOException("The prepared decryption workspace was not removed.");
    }

    private sealed class OriginalOnlyConversionService: IDecryptedDocumentConversionService
    {
        public bool CanConvert(string originalExtension) => false;

        public Task ConvertAsync(Stream source, string originalExtension,
            DecryptionOutputFormat targetFormat, Stream destination,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    // A disposable random password used only for generated integration fixtures.
    private sealed class IntegrationKeyProvider: IKeyProvider, IDisposable
    {
        private readonly byte[] password = RandomNumberGenerator.GetBytes(32);
        private readonly Argon2idKeyDeriver deriver = new();
        public bool HasKey => true;
        public bool CanEncrypt => true;
        public void SetKey(ReadOnlySpan<char> value) => throw new NotSupportedException();
        public byte[] DeriveKey(byte[] salt) => Rfc2898DeriveBytes.Pbkdf2(
            password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        public Task<byte[]> DeriveKeyAsync(ReadOnlyMemory<byte> salt,
            KeyDerivationParameters parameters, CancellationToken cancellationToken = default) =>
            deriver.DeriveAsync(password, salt, parameters, cancellationToken);
        public void Clear() => CryptographicOperations.ZeroMemory(password);
        public void Dispose() => Clear();
    }
}
