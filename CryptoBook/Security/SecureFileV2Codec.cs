using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CryptoBook.Security
{
    public sealed class SecureFileV2Codec: ISecureFileV2Codec
    {
        private const byte FormatVersion = 2;
        private const byte Argon2idAlgorithm = 1;
        private const byte Aes256GcmAlgorithm = 1;
        private const byte FinalChunkFlag = 1;
        private const int HeaderSize = 66;
        private const int RecordHeaderSize = 5;

        private readonly IKeyProvider _keyProvider;
        private readonly SecureFileV2Options _options;

        public SecureFileV2Codec(
            IKeyProvider keyProvider,
            SecureFileV2Options options)
        {
            _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _options.Validate();
        }

        public async Task<bool> HasHeaderAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            await using FileStream stream = OpenRead(filePath);
            if(stream.Length < SecureFileFormat.V2MagicHeader.Length)
                return false;

            byte[] header = new byte[SecureFileFormat.V2MagicHeader.Length];
            await stream.ReadExactlyAsync(header, cancellationToken);
            return header.AsSpan().SequenceEqual(SecureFileFormat.V2MagicHeader);
        }

        public async Task EncryptFileAsync(
            string inputFile,
            string outputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default)
        {
            StagedEncryption staged;
            await using(FileStream input = OpenRead(inputFile))
            {
                staged = await EncryptToTemporaryFileAsync(
                    input,
                    Path.GetExtension(inputFile),
                    outputFile,
                    progress,
                    cancellationToken);
            }

            CommitEncryptedFile(
                staged,
                preserveBackup: !PathsEqual(inputFile, outputFile));
        }

        public async Task EncryptStreamAsync(
            Stream input,
            string originalExtension,
            string outputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default)
        {
            StagedEncryption staged = await EncryptToTemporaryFileAsync(
                input,
                originalExtension,
                outputFile,
                progress,
                cancellationToken);

            CommitEncryptedFile(staged, preserveBackup: true);
        }

        private async Task<StagedEncryption> EncryptToTemporaryFileAsync(
            Stream input,
            string originalExtension,
            string outputFile,
            IProgressReporter? progress,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(input);
            if(!input.CanRead || !input.CanSeek)
                throw new ArgumentException(
                    "Поток должен поддерживать чтение и позиционирование.",
                    nameof(input));
            ArgumentException.ThrowIfNullOrWhiteSpace(originalExtension);

            string fullOutputPath = Path.GetFullPath(outputFile);
            string outputDirectory = Path.GetDirectoryName(fullOutputPath) ??
                throw new IOException("Не удалось определить каталог назначения.");
            string tempFile = Path.Combine(
                outputDirectory,
                $".{Path.GetFileName(outputFile)}.{Guid.NewGuid():N}.tmp");

            try
            {
                await EncryptCoreAsync(
                    input,
                    originalExtension,
                    tempFile,
                    progress,
                    cancellationToken);
                return new StagedEncryption(tempFile, fullOutputPath);
            } catch
            {
                TryDelete(tempFile);
                throw;
            }
        }

        private static void CommitEncryptedFile(
            StagedEncryption staged,
            bool preserveBackup)
        {
            try
            {
                if(preserveBackup)
                {
                    AtomicFileCommit.CommitWithBackup(
                        staged.TemporaryPath,
                        staged.OutputPath);
                }
                else
                {
                    AtomicFileCommit.CommitWithoutBackup(
                        staged.TemporaryPath,
                        staged.OutputPath);
                }
            } catch
            {
                TryDelete(staged.TemporaryPath);
                throw;
            }
        }

        private static bool PathsEqual(string firstPath, string secondPath) =>
            string.Equals(
                Path.GetFullPath(firstPath),
                Path.GetFullPath(secondPath),
                StringComparison.OrdinalIgnoreCase);

        public async Task DecryptFileAsyncToFile(
            string inputFile,
            string outputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default)
        {
            string? finalFile = null;
            string? tempFile = null;

            try
            {
                await DecryptCoreAsync(
                    inputFile,
                    extension =>
                    {
                        finalFile = outputFile + extension;
                        tempFile = finalFile + $".{Guid.NewGuid():N}.tmp";
                        return new FileStream(
                            tempFile,
                            FileMode.CreateNew,
                            FileAccess.Write,
                            FileShare.None,
                            SecureFileFormat.BufferSize,
                            FileOptions.Asynchronous | FileOptions.SequentialScan);
                    },
                    leaveOutputOpen: false,
                    progress,
                    cancellationToken);

                File.Move(tempFile!, finalFile!, overwrite: true);
            } catch
            {
                if(tempFile is not null)
                    TryDelete(tempFile);
                throw;
            }
        }

        public async Task<Stream> DecryptFileAsyncToStream(
            string inputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default)
        {
            MemoryStream output = new();
            try
            {
                await DecryptCoreAsync(
                    inputFile,
                    _ => output,
                    leaveOutputOpen: true,
                    progress,
                    cancellationToken);
                output.Position = 0;
                return output;
            } catch
            {
                await output.DisposeAsync();
                throw;
            }
        }

        private async Task EncryptCoreAsync(
            Stream input,
            string originalExtension,
            string tempFile,
            IProgressReporter? progress,
            CancellationToken cancellationToken)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SecureFileFormat.SaltSize);
            byte[] baseNonce = RandomNumberGenerator.GetBytes(SecureFileFormat.NonceSize);
            baseNonce.AsSpan(4).Clear();

            KeyDerivationParameters parameters = _options.KeyDerivation;
            byte[]? key = null;
            byte[] plaintext = new byte[_options.ChunkSize];
            byte[] ciphertext = new byte[_options.ChunkSize];
            byte[] tag = new byte[SecureFileFormat.GcmTagSize];
            byte[] nonce = new byte[SecureFileFormat.NonceSize];

            try
            {
                key = await _keyProvider.DeriveKeyAsync(
                    salt,
                    parameters,
                    cancellationToken);

                if(!originalExtension.StartsWith('.') ||
                   originalExtension.IndexOfAny(
                       Path.GetInvalidFileNameChars()) >= 0)
                {
                    throw new CryptographicException(
                        "Некорректное расширение файла.");
                }

                byte[] extension =
                    Encoding.UTF8.GetBytes(originalExtension);
                if(extension.Length is <= 0 or
                   > SecureFileFormat.MaxExtensionLength)
                    throw new CryptographicException("Некорректная длина расширения файла.");

                byte[] metadata = new byte[sizeof(int) + extension.Length];
                BinaryPrimitives.WriteInt32LittleEndian(metadata, extension.Length);
                extension.CopyTo(metadata.AsSpan(sizeof(int)));

                long contentLength = input.Length - input.Position;
                long payloadLength =
                    checked(contentLength + metadata.Length);
                byte[] header = CreateHeader(
                    parameters,
                    salt,
                    baseNonce,
                    payloadLength,
                    _options.ChunkSize);

                await using FileStream output = new(
                    tempFile,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    SecureFileFormat.BufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await output.WriteAsync(header, cancellationToken);

                using AesGcm aes = new(key, SecureFileFormat.GcmTagSize);
                long remaining = payloadLength;
                long fileProcessed = 0;
                ulong chunkIndex = 0;
                bool firstChunk = true;

                while(remaining > 0)
                {
                    int chunkLength = (int)Math.Min(plaintext.Length, remaining);
                    int offset = 0;

                    if(firstChunk)
                    {
                        metadata.CopyTo(plaintext, 0);
                        offset = metadata.Length;
                        firstChunk = false;
                    }

                    int fileBytes = await ReadUpToAsync(
                        input,
                        plaintext.AsMemory(offset, chunkLength - offset),
                        cancellationToken);
                    int actualLength = offset + fileBytes;
                    if(actualLength != chunkLength)
                        throw new EndOfStreamException("Исходный файл неожиданно закончился.");

                    remaining -= actualLength;
                    byte flags = remaining == 0 ? FinalChunkFlag : (byte)0;
                    byte[] recordHeader = new byte[RecordHeaderSize];
                    recordHeader[0] = flags;
                    BinaryPrimitives.WriteInt32LittleEndian(recordHeader.AsSpan(1), actualLength);

                    CreateNonce(baseNonce, chunkIndex, nonce);
                    byte[] associatedData = CreateAssociatedData(header, chunkIndex, recordHeader);
                    aes.Encrypt(
                        nonce,
                        plaintext.AsSpan(0, actualLength),
                        ciphertext.AsSpan(0, actualLength),
                        tag,
                        associatedData);

                    await output.WriteAsync(recordHeader, cancellationToken);
                    await output.WriteAsync(ciphertext.AsMemory(0, actualLength), cancellationToken);
                    await output.WriteAsync(tag, cancellationToken);

                    fileProcessed += fileBytes;
                    if(contentLength > 0)
                        progress?.Report(
                            (double)fileProcessed / contentLength);
                    chunkIndex++;
                }

                await output.FlushAsync(cancellationToken);
                output.Flush(flushToDisk: true);
                progress?.Report(1.0);
            } finally
            {
                if(key is not null)
                    CryptographicOperations.ZeroMemory(key);
                CryptographicOperations.ZeroMemory(plaintext);
                CryptographicOperations.ZeroMemory(nonce);
            }
        }

        private async Task DecryptCoreAsync(
            string inputFile,
            Func<string, Stream> outputFactory,
            bool leaveOutputOpen,
            IProgressReporter? progress,
            CancellationToken cancellationToken)
        {
            byte[]? key = null;
            Stream? output = null;
            byte[] plaintext = new byte[SecureFileFormat.V2ChunkSize];
            byte[] ciphertext = new byte[SecureFileFormat.V2ChunkSize];
            byte[] tag = new byte[SecureFileFormat.GcmTagSize];
            byte[] nonce = new byte[SecureFileFormat.NonceSize];

            try
            {
                await using FileStream input = OpenRead(inputFile);
                byte[] header = new byte[HeaderSize];
                await input.ReadExactlyAsync(header, cancellationToken);
                ParsedHeader parsed = ParseHeader(header);

                key = await _keyProvider.DeriveKeyAsync(
                    parsed.Salt,
                    parsed.Parameters,
                    cancellationToken);

                using AesGcm aes = new(key, SecureFileFormat.GcmTagSize);
                long remaining = parsed.PayloadLength;
                ulong chunkIndex = 0;
                bool finalSeen = false;
                bool firstChunk = true;
                long contentProcessed = 0;
                long contentLength = 0;

                while(remaining > 0)
                {
                    byte[] recordHeader = new byte[RecordHeaderSize];
                    await input.ReadExactlyAsync(recordHeader, cancellationToken);
                    byte flags = recordHeader[0];
                    int chunkLength = BinaryPrimitives.ReadInt32LittleEndian(recordHeader.AsSpan(1));

                    if((flags & ~FinalChunkFlag) != 0 ||
                       chunkLength <= 0 ||
                       chunkLength > parsed.ChunkSize ||
                       chunkLength > remaining)
                    {
                        throw new CryptographicException("Некорректная структура блока.");
                    }

                    await input.ReadExactlyAsync(ciphertext.AsMemory(0, chunkLength), cancellationToken);
                    await input.ReadExactlyAsync(tag, cancellationToken);

                    CreateNonce(parsed.BaseNonce, chunkIndex, nonce);
                    byte[] associatedData = CreateAssociatedData(header, chunkIndex, recordHeader);
                    aes.Decrypt(
                        nonce,
                        ciphertext.AsSpan(0, chunkLength),
                        tag,
                        plaintext.AsSpan(0, chunkLength),
                        associatedData);

                    int dataOffset = 0;
                    if(firstChunk)
                    {
                        if(chunkLength < sizeof(int))
                            throw new CryptographicException("Отсутствуют метаданные файла.");

                        int extensionLength = BinaryPrimitives.ReadInt32LittleEndian(plaintext);
                        if(extensionLength is <= 0 or > SecureFileFormat.MaxExtensionLength ||
                           sizeof(int) + extensionLength > chunkLength)
                        {
                            throw new CryptographicException("Некорректное расширение файла.");
                        }

                        string extension = Encoding.UTF8.GetString(
                            plaintext,
                            sizeof(int),
                            extensionLength);
                        if(!extension.StartsWith('.') ||
                           extension.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                        {
                            throw new CryptographicException("Некорректное расширение файла.");
                        }

                        output = outputFactory(extension);
                        dataOffset = sizeof(int) + extensionLength;
                        contentLength = parsed.PayloadLength - dataOffset;
                        firstChunk = false;
                    }

                    if(output is null)
                        throw new CryptographicException("Не удалось создать выходной поток.");

                    int dataLength = chunkLength - dataOffset;
                    if(dataLength > 0)
                        await output.WriteAsync(plaintext.AsMemory(dataOffset, dataLength), cancellationToken);

                    remaining -= chunkLength;
                    contentProcessed += dataLength;
                    finalSeen = (flags & FinalChunkFlag) != 0;
                    if(finalSeen != (remaining == 0))
                        throw new CryptographicException("Некорректный завершающий блок.");

                    if(contentLength > 0)
                        progress?.Report(Math.Min(1.0, (double)contentProcessed / contentLength));
                    chunkIndex++;
                }

                if(!finalSeen || input.Position != input.Length)
                    throw new CryptographicException("Некорректное завершение защищённого файла.");

                if(output is not null)
                    await output.FlushAsync(cancellationToken);
                progress?.Report(1.0);
            } finally
            {
                if(key is not null)
                    CryptographicOperations.ZeroMemory(key);
                CryptographicOperations.ZeroMemory(plaintext);
                CryptographicOperations.ZeroMemory(nonce);

                if(output is not null && !leaveOutputOpen)
                    await output.DisposeAsync();
            }
        }

        private static byte[] CreateHeader(
            KeyDerivationParameters parameters,
            byte[] salt,
            byte[] baseNonce,
            long payloadLength,
            int chunkSize)
        {
            byte[] header = new byte[HeaderSize];
            SecureFileFormat.V2MagicHeader.CopyTo(header, 0);
            header[8] = FormatVersion;
            header[9] = Argon2idAlgorithm;
            header[10] = Aes256GcmAlgorithm;
            header[11] = SecureFileFormat.SaltSize;
            header[12] = SecureFileFormat.NonceSize;
            header[13] = SecureFileFormat.GcmTagSize;
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(14), parameters.Iterations);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(18), parameters.MemorySizeKiB);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(22), parameters.DegreeOfParallelism);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(26), chunkSize);
            BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(30), payloadLength);
            salt.CopyTo(header, 38);
            baseNonce.CopyTo(header, 54);
            return header;
        }

        private static ParsedHeader ParseHeader(byte[] header)
        {
            if(header.Length != HeaderSize ||
               !header.AsSpan(0, SecureFileFormat.V2MagicHeader.Length)
                   .SequenceEqual(SecureFileFormat.V2MagicHeader) ||
               header[8] != FormatVersion ||
               header[9] != Argon2idAlgorithm ||
               header[10] != Aes256GcmAlgorithm ||
               header[11] != SecureFileFormat.SaltSize ||
               header[12] != SecureFileFormat.NonceSize ||
               header[13] != SecureFileFormat.GcmTagSize)
            {
                throw new CryptographicException("Неподдерживаемый формат защищённого файла.");
            }

            KeyDerivationParameters parameters = new(
                BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(14)),
                BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(18)),
                BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(22)),
                32);
            parameters.Validate();

            int chunkSize = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(26));
            long payloadLength = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(30));
            if(chunkSize is < 4096 or > SecureFileFormat.V2ChunkSize ||
               payloadLength is <= sizeof(int) or > long.MaxValue - HeaderSize)
            {
                throw new CryptographicException("Недопустимые параметры защищённого файла.");
            }

            return new ParsedHeader(
                parameters,
                header.AsSpan(38, SecureFileFormat.SaltSize).ToArray(),
                header.AsSpan(54, SecureFileFormat.NonceSize).ToArray(),
                chunkSize,
                payloadLength);
        }

        private static byte[] CreateAssociatedData(
            byte[] header,
            ulong chunkIndex,
            ReadOnlySpan<byte> recordHeader)
        {
            byte[] associatedData = new byte[header.Length + sizeof(ulong) + RecordHeaderSize];
            header.CopyTo(associatedData, 0);
            BinaryPrimitives.WriteUInt64LittleEndian(
                associatedData.AsSpan(header.Length),
                chunkIndex);
            recordHeader.CopyTo(associatedData.AsSpan(header.Length + sizeof(ulong)));
            return associatedData;
        }

        private static void CreateNonce(byte[] baseNonce, ulong chunkIndex, byte[] destination)
        {
            baseNonce.CopyTo(destination, 0);
            BinaryPrimitives.WriteUInt64BigEndian(destination.AsSpan(4), chunkIndex);
        }

        private static async Task<int> ReadUpToAsync(
            Stream stream,
            Memory<byte> destination,
            CancellationToken cancellationToken)
        {
            int total = 0;
            while(total < destination.Length)
            {
                int read = await stream.ReadAsync(destination[total..], cancellationToken);
                if(read == 0)
                    break;
                total += read;
            }
            return total;
        }

        private static FileStream OpenRead(string path) => new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            SecureFileFormat.BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        private static void TryDelete(string path)
        {
            try
            {
                if(File.Exists(path))
                    File.Delete(path);
            } catch
            {
            }
        }

        private sealed record StagedEncryption(
            string TemporaryPath,
            string OutputPath);

        private sealed record ParsedHeader(
            KeyDerivationParameters Parameters,
            byte[] Salt,
            byte[] BaseNonce,
            int ChunkSize,
            long PayloadLength);
    }
}
