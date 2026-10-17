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
        var security = new FileSecurityService(
            new UnusedItemFactory(), processor, storageFacade: storage, transferEngine: engine);
        (string Name, byte[] Content)[] fixtures =
        [
            ("Текст.txt", Encoding.UTF8.GetBytes(string.Concat(
                Enumerable.Repeat("CryptoBook: проверка расшифровки Windows ↔ Android.\r\n", 2048)))),
            ("Изображение.png", Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII="))
        ];

        foreach((string name, byte[] content) in fixtures)
        {
            StorageLocation rawSource = provider.GetChild(remoteFolder, "Исходник-" + name);
            string rawLocal = Path.Combine(localRoot, "Исходник-" + name);
            await File.WriteAllBytesAsync(rawLocal, content);
            EnsureSuccess(await engine.CopyAsync(storage.Resolve(rawLocal), rawSource));
            var rawItem = new FileItem
            {
                FullPath = rawSource.ToString(),
                Name = "Исходник-" + Path.GetFileNameWithoutExtension(name),
                Extension = Path.GetExtension(name), Size = content.Length
            };
            FileOperationBatchResult protectedCopy = await security.CreateProtectedCopiesAsync(
                [rawItem], remoteFolder.ToString());
            if(!protectedCopy.Success || protectedCopy.CompletedCount != 1)
                throw new IOException(protectedCopy.Results.FirstOrDefault()?.ErrorMessage);
            await VerifyRemoteAsync(rawSource, content);
            string protectedPath = protectedCopy.Results[0].AffectedPath!;
            await using(PreparedDecryption prepared = await export.PrepareAsync(protectedPath))
            {
                if(!SHA256.HashData(await File.ReadAllBytesAsync(prepared.ContentPath))
                    .SequenceEqual(SHA256.HashData(content)))
                    throw new IOException("Android protected-copy round trip failed.");
            }
            Console.WriteLine($"Android {Path.GetExtension(name)} -> Android .cbook: SHA256 OK; source preserved");

            FileOperationResult replaced = await security.EncryptAsync(
                rawItem, rawItem.FullPath, EncryptionTargetMode.ReplaceSource);
            EnsureSuccess(replaced);
            if(await provider.GetMetadataAsync(rawSource) is not null)
                throw new IOException("Encrypted source was not replaced.");
            // The copy already owns the desired .cbook name. Replacement must
            // publish to a unique sibling and leave that earlier copy intact.
            if(storage.AreEquivalent(storage.Resolve(protectedPath), storage.Resolve(replaced.AffectedPath!)))
                throw new IOException("Existing protected copy was overwritten.");
            await using(PreparedDecryption prepared = await export.PrepareAsync(replaced.AffectedPath!))
            {
                string restored = await export.PublishAsync(prepared,
                    options with { TargetMode = EncryptionTargetMode.ReplaceSource }, rawSource.ToString());
                await VerifyRemoteAsync(storage.Resolve(restored), content);
            }
            if(await provider.GetMetadataAsync(storage.Resolve(replaced.AffectedPath!)) is not null)
                throw new IOException("Decrypted container was not replaced.");
            Console.WriteLine($"Android encrypt/replace -> decrypt/replace {Path.GetExtension(name)}: SHA256 OK; conflict copy preserved");

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

        static void EnsureSuccess(FileOperationResult result)
        {
            if(!result.Success)
                throw new IOException(result.ErrorMessage);
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

    private sealed class UnusedItemFactory: ISystemItemCreateService
    {
        public IDriveItem CreateRoot(string path) => throw new NotSupportedException();
        public IDirectoryItem CreateDirectory(string path, ISystemItem? parent) => throw new NotSupportedException();
        public IFileItem CreateFile(string path, ISystemItem? parent) => throw new NotSupportedException();
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
