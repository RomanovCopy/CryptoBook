using CryptoBook.Interfaces;

using CryptoBook.Infrastructure;

using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CryptoBook.Security
{
    /// <summary>
    /// Читает прежний формат защищённых файлов. Кодек оставлен только для обратной
    /// совместимости; новые контейнеры создаются форматом V2.
    /// </summary>
    public sealed class LegacySecureFileCodec: ILegacySecureFileCodec
    {
        private readonly IKeyProvider _keyProvider;

        public LegacySecureFileCodec(IKeyProvider keyProvider)
        {
            _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
        }

        public async Task DecryptFileAsyncToFile(string inputFile, string outputFile, IProgressReporter? progress = null,
        CancellationToken cancellationToken = default)
        {
            string? finalFile = null;
            string? tempFile = null;

            try
            {
                await DecryptFileCoreAsync(inputFile, fileExtension =>
                    {
                        finalFile = outputFile + fileExtension;

                        tempFile = finalFile + "." + Guid.NewGuid().ToString("N") + ".tmp";

                        return new FileStream(tempFile, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    }, leaveOutputOpen: false, progress, cancellationToken);

                File.Move(tempFile!, finalFile!, overwrite: true);
            } catch
            {
                if(tempFile is not null && File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }

                throw;
            }
        }
        public async Task<Stream> DecryptFileAsyncToStream(string inputFile, IProgressReporter? progress = null,
        CancellationToken cancellationToken = default)
        {
            DecryptedFileContent decrypted = await DecryptFileContentAsync(
                inputFile,
                progress,
                cancellationToken);
            return decrypted.Content;
        }

        public async Task<DecryptedFileContent> DecryptFileContentAsync(
            string inputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default)
        {
            MemoryStream outputStream = new();

            try
            {
                string extension = await DecryptFileCoreAsync(
                    inputFile,
                    _ => outputStream,
                    leaveOutputOpen: true,
                    progress,
                    cancellationToken);

                outputStream.Position = 0;
                return new DecryptedFileContent(outputStream, extension);
            } catch
            {
                await outputStream.DisposeAsync();
                throw;
            }
        }
        private async Task<string> DecryptFileCoreAsync(string inputFile, Func<string, Stream> outputStreamFactory,
        bool leaveOutputOpen, IProgressReporter? progress, CancellationToken cancellationToken = default)
        {
            byte[]? key = null;
            Stream? outputStream = null;

            try
            {
                using FileStream inputStream = new(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read);

                await ValidateHeaderAsync(inputStream, cancellationToken);

                byte[] salt = new byte[SecureFileFormat.SaltSize];
                await inputStream.ReadExactlyAsync(salt, cancellationToken);

                key = _keyProvider.DeriveKey(salt);

                using Aes aes = Aes.Create();

                byte[] iv = new byte[aes.BlockSize / 8];
                await inputStream.ReadExactlyAsync(iv, cancellationToken);

                aes.Key = key;
                aes.IV = iv;

                long contentLength = inputStream.Length - SecureFileFormat.HmacSize;

                if(contentLength <= inputStream.Position)
                {
                    throw new CryptographicException("Некорректная структура зашифрованного файла.");
                }

                // HMAC проверяется до расшифрования: повреждённые или подменённые данные
                // не должны передаваться в CryptoStream и записываться на диск.
                await ValidateHmacAsync(inputStream, contentLength, key, cancellationToken);

                long encryptedContentStart = SecureFileFormat.MagicHeader.Length + SecureFileFormat.SaltSize + iv.Length;
                long encryptedContentLength = contentLength - encryptedContentStart;

                inputStream.Position = encryptedContentStart;

                using LimitedReadStream encryptedStream = new(
                    inputStream,
                    encryptedContentLength,
                    leaveOpen: true);
                using CryptoStream cryptoStream = new(
                    encryptedStream,
                    aes.CreateDecryptor(),
                    CryptoStreamMode.Read);

                string fileExtension = await ReadFileExtensionAsync(cryptoStream, cancellationToken);

                outputStream = outputStreamFactory(fileExtension);

                await CopyDecryptedContentAsync(
                    cryptoStream,
                    outputStream,
                    encryptedContentLength,
                    progress,
                    cancellationToken);

                return fileExtension;
            } finally
            {
                if(key is not null)
                {
                    CryptographicOperations.ZeroMemory(key);
                }

                if(outputStream is not null && !leaveOutputOpen)
                {
                    await outputStream.DisposeAsync();
                }
            }
        }
        private async Task ValidateHeaderAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            byte[] header = new byte[SecureFileFormat.MagicHeader.Length];
            await stream.ReadExactlyAsync(header, cancellationToken);

            if(!header.SequenceEqual(SecureFileFormat.MagicHeader))
            {
                throw new CryptographicException(
                    LocalizationManager.GetString(
                        "Security.NotEncryptedCryptoBookFile"));
            }
        }
        private async Task ValidateHmacAsync(Stream stream, long contentLength, byte[] key, CancellationToken cancellationToken = default)
        {
            stream.Position = 0;

            using IncrementalHash hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, key);

            byte[] buffer = new byte[SecureFileFormat.BufferSize];
            long remainingBytes = contentLength;

            while(remainingBytes > 0)
            {
                int bytesToRead = (int)Math.Min(buffer.Length, remainingBytes);

                int bytesRead = await stream.ReadAsync(
                    buffer.AsMemory(0, bytesToRead),
                    cancellationToken);

                if(bytesRead == 0)
                {
                    throw new EndOfStreamException("Неожиданный конец файла при проверке HMAC.");
                }

                hmac.AppendData(buffer, 0, bytesRead);
                remainingBytes -= bytesRead;
            }

            byte[] computedHmac = hmac.GetHashAndReset();

            byte[] storedHmac = new byte[SecureFileFormat.HmacSize];

            await stream.ReadExactlyAsync(storedHmac, cancellationToken);

            if(!CryptographicOperations.FixedTimeEquals(computedHmac, storedHmac))
            {
                throw new CryptographicException("Ошибка проверки HMAC. Данные повреждены или пароль неверен.");
            }
        }
        private async Task CopyDecryptedContentAsync(Stream cryptoStream, Stream outputStream, long approximateTotalBytes,
        IProgressReporter? progress, CancellationToken cancellationToken = default)
        {
            byte[] buffer = new byte[SecureFileFormat.BufferSize];

            long processedBytes = 0;
            int bytesRead;

            while((bytesRead = await cryptoStream.ReadAsync(
                      buffer,
                      cancellationToken)) > 0)
            {
                await outputStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);

                processedBytes += bytesRead;

                if(approximateTotalBytes > 0)
                {
                    progress?.Report(Math.Min(1.0, (double)processedBytes / approximateTotalBytes));
                }

                cancellationToken.ThrowIfCancellationRequested();
            }

            progress?.Report(1.0);
        }
        private async Task<string> ReadFileExtensionAsync(Stream cryptoStream, CancellationToken cancellationToken = default)
        {
            byte[] extensionLengthBytes = new byte[sizeof(int)];

            await cryptoStream.ReadExactlyAsync(extensionLengthBytes, cancellationToken);

            int extensionLength = BitConverter.ToInt32(extensionLengthBytes, 0);

            if(extensionLength <= 0 ||
               extensionLength > SecureFileFormat.MaxExtensionLength)
            {
                throw new CryptographicException("Некорректная длина расширения файла.");
            }

            byte[] extensionBytes = new byte[extensionLength];

            await cryptoStream.ReadExactlyAsync(extensionBytes, cancellationToken);

            return Encoding.UTF8.GetString(extensionBytes);
        }

        private sealed class LimitedReadStream: Stream
        {
            // Ограничение не позволяет CryptoStream прочитать хвост контейнера с HMAC
            // как часть зашифрованной полезной нагрузки.
            private readonly Stream _innerStream;
            private readonly bool _leaveOpen;
            private long _remainingBytes;

            public LimitedReadStream(
                Stream innerStream,
                long length,
                bool leaveOpen)
            {
                if(length < 0)
                    throw new ArgumentOutOfRangeException(nameof(length));

                _innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
                _remainingBytes = length;
                _leaveOpen = leaveOpen;
            }

            public override bool CanRead => _innerStream.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _remainingBytes;

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if(_remainingBytes == 0)
                    return 0;

                int bytesToRead = (int)Math.Min(count, _remainingBytes);
                int bytesRead = _innerStream.Read(buffer, offset, bytesToRead);
                _remainingBytes -= bytesRead;
                return bytesRead;
            }

            public override async ValueTask<int> ReadAsync(
                Memory<byte> buffer,
                CancellationToken cancellationToken = default)
            {
                if(_remainingBytes == 0)
                    return 0;

                int bytesToRead = (int)Math.Min(buffer.Length, _remainingBytes);
                int bytesRead = await _innerStream.ReadAsync(
                    buffer[..bytesToRead],
                    cancellationToken);
                _remainingBytes -= bytesRead;
                return bytesRead;
            }

            public override int ReadByte()
            {
                if(_remainingBytes == 0)
                    return -1;

                int value = _innerStream.ReadByte();
                if(value >= 0)
                    _remainingBytes--;

                return value;
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin) =>
                throw new NotSupportedException();

            public override void SetLength(long value) =>
                throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) =>
                throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if(disposing && !_leaveOpen)
                    _innerStream.Dispose();

                base.Dispose(disposing);
            }

            public override async ValueTask DisposeAsync()
            {
                if(!_leaveOpen)
                    await _innerStream.DisposeAsync();

                GC.SuppressFinalize(this);
            }
        }
    }
}

