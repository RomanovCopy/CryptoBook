using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CryptoBook.Security
{
    internal class SecureFileProcessor
    {
        public static async Task EncryptFileAsync( string inputFile, string outputFile, string password, IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                byte[] salt = RandomNumberGenerator.GetBytes(
                    SecureFileFormat.SaltSize);

                byte[] key = KeyGenerator.GenerateKeyFromPassword(
                    password,
                    salt);

                using FileStream outputStream = new FileStream(
                    outputFile,
                    FileMode.Create,
                    FileAccess.ReadWrite,
                    FileShare.None);

                await outputStream.WriteAsync(
                    SecureFileFormat.MagicHeader,
                    cancellationToken);

                await outputStream.WriteAsync(salt, cancellationToken);

                using(Aes aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.GenerateIV();

                    await outputStream.WriteAsync(aes.IV, cancellationToken);

                    using(CryptoStream cryptoStream = new CryptoStream(
                        outputStream,
                        aes.CreateEncryptor(),
                        CryptoStreamMode.Write,
                        leaveOpen: true))
                    {
                        string fileExtension = Path.GetExtension(inputFile);
                        byte[] extensionBytes = Encoding.UTF8.GetBytes(
                            fileExtension);

                        await cryptoStream.WriteAsync(
                            BitConverter.GetBytes(extensionBytes.Length),
                            cancellationToken);

                        await cryptoStream.WriteAsync(
                            extensionBytes,
                            cancellationToken);

                        using FileStream inputStream = new FileStream(
                            inputFile,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read);

                        byte[] buffer = new byte[SecureFileFormat.BufferSize];
                        long totalBytes = inputStream.Length;
                        long processedBytes = 0;
                        int bytesRead;

                        while((bytesRead = await inputStream.ReadAsync(
                            buffer,
                            cancellationToken)) > 0)
                        {
                            await cryptoStream.WriteAsync(
                                buffer.AsMemory(0, bytesRead),
                                cancellationToken);

                            processedBytes += bytesRead;

                            if(totalBytes > 0)
                            {
                                progress?.Report(
                                    (double)processedBytes / totalBytes);
                            }

                            cancellationToken.ThrowIfCancellationRequested();
                        }
                    }
                }

                using HMACSHA256 hmac = new HMACSHA256(key);

                long contentLength = outputStream.Position;
                outputStream.Position = 0;

                byte[] content = new byte[contentLength];
                await outputStream.ReadExactlyAsync(
                    content,
                    cancellationToken);

                byte[] hmacHash = hmac.ComputeHash(content);

                outputStream.Position = contentLength;
                await outputStream.WriteAsync(hmacHash, cancellationToken);
            } catch(OperationCanceledException)
            {
                Console.Error.WriteLine(
                    "Операция шифрования была отменена.");
                throw;
            } catch(Exception ex)
            {
                Console.Error.WriteLine(
                    $"Ошибка при шифровании файла: {ex.Message}");
                throw;
            }
        }

        public static async Task DecryptFileAsync( string inputFile, string outputFile, string password, IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using FileStream inputStream = new FileStream( inputFile, FileMode.Open, FileAccess.Read, FileShare.Read);

                await ValidateHeaderAsync(inputStream, cancellationToken);

                byte[] salt = new byte[SecureFileFormat.SaltSize];
                await inputStream.ReadExactlyAsync(salt, cancellationToken);

                byte[] key = KeyGenerator.GenerateKeyFromPassword(
                    password,
                    salt);

                using Aes aes = Aes.Create();

                byte[] iv = new byte[aes.BlockSize / 8];
                await inputStream.ReadExactlyAsync(iv, cancellationToken);

                aes.Key = key;
                aes.IV = iv;

                long contentLength = inputStream.Length
                    - SecureFileFormat.HmacSize;

                await ValidateHmacAsync( inputStream, contentLength, key, cancellationToken);

                inputStream.Position = SecureFileFormat.MagicHeader.Length + SecureFileFormat.SaltSize + iv.Length;

                using CryptoStream cryptoStream = new CryptoStream( inputStream, aes.CreateDecryptor(), CryptoStreamMode.Read);

                byte[] extensionLengthBytes = new byte[sizeof(int)];
                await cryptoStream.ReadExactlyAsync( extensionLengthBytes, cancellationToken);

                int extensionLength = BitConverter.ToInt32( extensionLengthBytes, 0);

                if(extensionLength <= 0 || extensionLength > SecureFileFormat.MaxExtensionLength)
                {
                    throw new CryptographicException( "Некорректная длина расширения файла.");
                }

                byte[] extensionBytes = new byte[extensionLength];
                await cryptoStream.ReadExactlyAsync( extensionBytes, cancellationToken);

                string fileExtension = Encoding.UTF8.GetString( extensionBytes);

                using FileStream outputStream = new FileStream( outputFile + fileExtension, FileMode.Create, FileAccess.Write, FileShare.None);

                byte[] buffer = new byte[SecureFileFormat.BufferSize];
                long totalBytes = contentLength - inputStream.Position;
                long processedBytes = 0;
                int bytesRead;

                while((bytesRead = await cryptoStream.ReadAsync( buffer, cancellationToken)) > 0)
                {
                    await outputStream.WriteAsync( buffer.AsMemory(0, bytesRead), cancellationToken);

                    processedBytes += bytesRead;

                    if(totalBytes > 0)
                    {
                        progress?.Report((double)processedBytes / totalBytes);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                }
            } catch(OperationCanceledException)
            {
                Console.Error.WriteLine( "Операция расшифровки была отменена.");
                throw;
            } catch(CryptographicException ex)
            {
                Console.Error.WriteLine( $"Криптографическая ошибка при расшифровке файла: " + ex.Message);
                throw;
            } catch(Exception ex)
            {
                Console.Error.WriteLine( $"Ошибка при расшифровке файла: {ex.Message}");
                throw;
            }
        }

        private static async Task ValidateHeaderAsync( Stream stream, CancellationToken cancellationToken)
        {
            byte[] header = new byte[SecureFileFormat.MagicHeader.Length];
            await stream.ReadExactlyAsync(header, cancellationToken);

            if(!header.SequenceEqual(SecureFileFormat.MagicHeader))
            {
                throw new CryptographicException(
                    "Файл не является зашифрованным файлом CryptoBook.");
            }
        }

        private static async Task ValidateHmacAsync( Stream stream, long contentLength, byte[] key, CancellationToken cancellationToken)
        {
            stream.Position = 0;

            byte[] content = new byte[contentLength];
            await stream.ReadExactlyAsync(content, cancellationToken);

            using HMACSHA256 hmac = new HMACSHA256(key);
            byte[] computedHmac = hmac.ComputeHash(content);

            stream.Position = contentLength;

            byte[] storedHmac = new byte[SecureFileFormat.HmacSize];
            await stream.ReadExactlyAsync(storedHmac, cancellationToken);

            if(!CryptographicOperations.FixedTimeEquals( computedHmac, storedHmac))
            {
                throw new CryptographicException( "Ошибка проверки HMAC. Данные повреждены " + "или пароль неверен.");
            }
        }
    }
}

