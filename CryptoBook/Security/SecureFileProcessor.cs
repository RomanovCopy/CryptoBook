using CryptoBook.Interfaces;

using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CryptoBook.Security
{
    public class SecureFileProcessor:ISecureFileProcessor
    {

        public async Task EncryptFileAsync( string inputFile, string outputFile, char[] password, IProgressReporter? progress = null, CancellationToken cancellationToken = default)
        {
            byte[]? key = null;
            string tempFile = outputFile + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                byte[] salt = RandomNumberGenerator.GetBytes( SecureFileFormat.SaltSize);

                key = KeyGenerator.GenerateKeyFromPassword(password, salt);

                await using FileStream outputStream = new ( tempFile, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                SecureFileFormat.BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

                using HmacWriteStream hmacStream = new HmacWriteStream( outputStream, key, leaveOpen: true);

                await hmacStream.WriteAsync( SecureFileFormat.MagicHeader, cancellationToken);

                await hmacStream.WriteAsync(salt, cancellationToken);

                using Aes aes = Aes.Create();

                aes.Key = key;
                aes.GenerateIV();

                await hmacStream.WriteAsync(aes.IV, cancellationToken);

                using CryptoStream cryptoStream = new CryptoStream( hmacStream, aes.CreateEncryptor(),
                    CryptoStreamMode.Write,
                    leaveOpen: true);

                await WriteFileExtensionAsync( cryptoStream, inputFile, progress, cancellationToken);

                await EncryptFileContentAsync( inputFile, cryptoStream, progress, cancellationToken);

                await cryptoStream.FlushFinalBlockAsync(cancellationToken);

                byte[] hmacHash = hmacStream.GetHashAndReset();

                await outputStream.WriteAsync( hmacHash, cancellationToken);

                await outputStream.FlushAsync(cancellationToken);

                File.Move(tempFile, outputFile, overwrite: true);
            } catch
            {
                if(File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }

                throw;
            } finally
            {
                if(key is not null)
                {
                    CryptographicOperations.ZeroMemory(key);
                }
            }
        }
        public async Task DecryptFileAsyncToFile( string inputFile, string outputFile, char[] password, IProgressReporter? progress = null,
        CancellationToken cancellationToken = default)
        {
            string? finalFile = null;
            string? tempFile = null;

            try
            {
                await DecryptFileCoreAsync(
                    inputFile,
                    password,
                    fileExtension =>
                    {
                        finalFile = outputFile + fileExtension;

                        tempFile = finalFile + "." + Guid.NewGuid().ToString("N") + ".tmp";

                        return new FileStream( tempFile, FileMode.CreateNew, FileAccess.Write, FileShare.None);
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
        public async Task<Stream> DecryptFileAsyncToStream( string inputFile, char[] password, IProgressReporter? progress = null, 
        CancellationToken cancellationToken = default)
        {
            MemoryStream outputStream = new ();

            try
            {
                await DecryptFileCoreAsync( inputFile, password, _ => outputStream, leaveOutputOpen: true, progress, cancellationToken);

                outputStream.Position = 0;
                return outputStream;
            } catch
            {
                await outputStream.DisposeAsync();
                throw;
            }
        }


        private async Task WriteFileExtensionAsync( Stream cryptoStream, string inputFile, IProgressReporter? progress = null, CancellationToken cancellationToken = default)
        {
            string fileExtension = Path.GetExtension(inputFile);

            byte[] extensionBytes = Encoding.UTF8.GetBytes(fileExtension);

            if(extensionBytes.Length <= 0 ||
               extensionBytes.Length > SecureFileFormat.MaxExtensionLength)
            {
                throw new CryptographicException( "Некорректная длина расширения файла.");
            }

            await cryptoStream.WriteAsync( BitConverter.GetBytes(extensionBytes.Length), cancellationToken);

            await cryptoStream.WriteAsync( extensionBytes, cancellationToken);
        }

        private async Task EncryptFileContentAsync( string inputFile, Stream cryptoStream, IProgressReporter? progress = null,
        CancellationToken cancellationToken = default)
        {
            await using FileStream inputStream = new ( inputFile, FileMode.Open, FileAccess.Read, FileShare.Read, SecureFileFormat.BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

            byte[] buffer = new byte[SecureFileFormat.BufferSize];

            long totalBytes = inputStream.Length;
            long processedBytes = 0;

            int bytesRead;

            while((bytesRead = await inputStream.ReadAsync( buffer, cancellationToken)) > 0)
            {
                await cryptoStream.WriteAsync(
                    buffer.AsMemory(0, bytesRead),
                    cancellationToken);

                processedBytes += bytesRead;

                if(totalBytes > 0)
                {
                    progress?.Report((double)processedBytes / totalBytes);
                }

                cancellationToken.ThrowIfCancellationRequested();
            }

            progress?.Report(1.0);
        }
        private async Task<string> DecryptFileCoreAsync( string inputFile, char[] password, Func<string, Stream> outputStreamFactory, 
        bool leaveOutputOpen, IProgressReporter? progress, CancellationToken cancellationToken = default)
        {
            byte[]? key = null;
            Stream? outputStream = null;

            try
            {
                using FileStream inputStream = new( inputFile, FileMode.Open, FileAccess.Read, FileShare.Read);

                await ValidateHeaderAsync(inputStream, cancellationToken);

                byte[] salt = new byte[SecureFileFormat.SaltSize];
                await inputStream.ReadExactlyAsync(salt, cancellationToken);

                key = KeyGenerator.GenerateKeyFromPassword(password, salt);

                using Aes aes = Aes.Create();

                byte[] iv = new byte[aes.BlockSize / 8];
                await inputStream.ReadExactlyAsync(iv, cancellationToken);

                aes.Key = key;
                aes.IV = iv;

                long contentLength = inputStream.Length - SecureFileFormat.HmacSize;

                if(contentLength <= inputStream.Position)
                {
                    throw new CryptographicException( "Некорректная структура зашифрованного файла.");
                }

                await ValidateHmacAsync( inputStream, contentLength, key, cancellationToken);

                long encryptedContentStart = SecureFileFormat.MagicHeader.Length + SecureFileFormat.SaltSize + iv.Length;

                inputStream.Position = encryptedContentStart;

                using CryptoStream cryptoStream = new ( inputStream, aes.CreateDecryptor(), CryptoStreamMode.Read);

                string fileExtension = await ReadFileExtensionAsync( cryptoStream, cancellationToken);

                outputStream = outputStreamFactory(fileExtension);

                await CopyDecryptedContentAsync( cryptoStream, outputStream, contentLength - encryptedContentStart, progress, cancellationToken);

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
        private async Task ValidateHeaderAsync( Stream stream, CancellationToken cancellationToken = default)
        {
            byte[] header = new byte[SecureFileFormat.MagicHeader.Length];
            await stream.ReadExactlyAsync(header, cancellationToken);

            if(!header.SequenceEqual(SecureFileFormat.MagicHeader))
            {
                throw new CryptographicException(
                    "Файл не является зашифрованным файлом CryptoBook.");
            }
        }
        private async Task ValidateHmacAsync( Stream stream, long contentLength, byte[] key, CancellationToken cancellationToken = default)
        {
            stream.Position = 0;

            using IncrementalHash hmac = IncrementalHash.CreateHMAC( HashAlgorithmName.SHA256, key);

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
                    throw new EndOfStreamException( "Неожиданный конец файла при проверке HMAC.");
                }

                hmac.AppendData(buffer, 0, bytesRead);
                remainingBytes -= bytesRead;
            }

            byte[] computedHmac = hmac.GetHashAndReset();

            byte[] storedHmac = new byte[SecureFileFormat.HmacSize];

            await stream.ReadExactlyAsync( storedHmac, cancellationToken);

            if(!CryptographicOperations.FixedTimeEquals(computedHmac, storedHmac))
            {
                throw new CryptographicException( "Ошибка проверки HMAC. Данные повреждены или пароль неверен.");
            }
        }
        private async Task CopyDecryptedContentAsync( Stream cryptoStream, Stream outputStream, long approximateTotalBytes,
        IProgressReporter? progress, CancellationToken cancellationToken = default)
        {
            byte[] buffer = new byte[SecureFileFormat.BufferSize];

            long processedBytes = 0;
            int bytesRead;

            while((bytesRead = await cryptoStream.ReadAsync(
                      buffer,
                      cancellationToken)) > 0)
            {
                await outputStream.WriteAsync( buffer.AsMemory(0, bytesRead), cancellationToken);

                processedBytes += bytesRead;

                if(approximateTotalBytes > 0)
                {
                    progress?.Report( Math.Min(1.0, (double)processedBytes / approximateTotalBytes));
                }

                cancellationToken.ThrowIfCancellationRequested();
            }

            progress?.Report(1.0);
        }
        private async Task<string> ReadFileExtensionAsync( Stream cryptoStream, CancellationToken cancellationToken = default)
        {
            byte[] extensionLengthBytes = new byte[sizeof(int)];

            await cryptoStream.ReadExactlyAsync( extensionLengthBytes, cancellationToken);

            int extensionLength = BitConverter.ToInt32(extensionLengthBytes, 0);

            if(extensionLength <= 0 ||
               extensionLength > SecureFileFormat.MaxExtensionLength)
            {
                throw new CryptographicException( "Некорректная длина расширения файла.");
            }

            byte[] extensionBytes = new byte[extensionLength];

            await cryptoStream.ReadExactlyAsync( extensionBytes, cancellationToken);

            return Encoding.UTF8.GetString(extensionBytes);
        }
    }
}

