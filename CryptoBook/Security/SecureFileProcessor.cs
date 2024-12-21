using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Security
{
    internal class SecureFileProcessor
    {
        public static async Task EncryptFileAsync(string inputFile, string outputFile, string password, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                byte[] salt = RandomNumberGenerator.GetBytes(16);
                byte[] key = KeyGenerator.GenerateKeyFromPassword(password, salt);

                using(FileStream outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
                {
                    // Записываем соль
                    await outputStream.WriteAsync(salt, 0, salt.Length, cancellationToken);

                    using(Aes aes = Aes.Create())
                    {
                        aes.Key = key;
                        aes.GenerateIV();

                        // Записываем IV
                        await outputStream.WriteAsync(aes.IV, 0, aes.IV.Length, cancellationToken);

                        using(CryptoStream cryptoStream = new CryptoStream(outputStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
                        {
                            // Сохраняем и шифруем расширение файла
                            string fileExtension = Path.GetExtension(inputFile);
                            byte[] extensionBytes = System.Text.Encoding.UTF8.GetBytes(fileExtension);
                            await cryptoStream.WriteAsync(BitConverter.GetBytes(extensionBytes.Length), 0, sizeof(int), cancellationToken);
                            await cryptoStream.WriteAsync(extensionBytes, 0, extensionBytes.Length, cancellationToken);

                            // Шифруем содержимое файла
                            using(FileStream inputStream = new FileStream(inputFile, FileMode.Open, FileAccess.Read))
                            {
                                byte[] buffer = new byte[8192];
                                long totalBytes = inputStream.Length;
                                long processedBytes = 0;
                                int bytesRead;

                                while((bytesRead = await inputStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                                {
                                    await cryptoStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                                    processedBytes += bytesRead;
                                    progress?.Report((double)processedBytes / totalBytes);
                                    cancellationToken.ThrowIfCancellationRequested();
                                }
                            }
                        }
                    }

                    // Генерация HMAC для проверки целостности
                    using(HMACSHA256 hmac = new HMACSHA256(key))
                    {
                        long contentLength = outputStream.Position;
                        outputStream.Position = 0;
                        byte[] content = new byte[contentLength];
                        await outputStream.ReadAsync(content, 0, content.Length, cancellationToken);

                        byte[] hmacHash = hmac.ComputeHash(content);
                        await outputStream.WriteAsync(hmacHash, 0, hmacHash.Length, cancellationToken);
                    }
                }
            } catch(OperationCanceledException)
            {
                Console.Error.WriteLine("Операция шифрования была отменена.");
                throw;
            } catch(Exception ex)
            {
                Console.Error.WriteLine($"Ошибка при шифровании файла: {ex.Message}");
                throw;
            }
        }

        public static async Task DecryptFileAsync(string inputFile, string outputFile, string password, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using(FileStream inputStream = new FileStream(inputFile, FileMode.Open, FileAccess.Read))
                {
                    byte[] salt = new byte[16];
                    await inputStream.ReadAsync(salt, 0, salt.Length, cancellationToken);

                    byte[] key = KeyGenerator.GenerateKeyFromPassword(password, salt);

                    using(Aes aes = Aes.Create())
                    {
                        byte[] iv = new byte[aes.BlockSize / 8];
                        await inputStream.ReadAsync(iv, 0, iv.Length, cancellationToken);
                        aes.Key = key;
                        aes.IV = iv;

                        long contentLength = inputStream.Length - 32; // Длина без HMAC

                        // Проверка HMAC
                        inputStream.Position = 0;
                        byte[] content = new byte[contentLength];
                        await inputStream.ReadAsync(content, 0, content.Length, cancellationToken);

                        using(HMACSHA256 hmac = new HMACSHA256(key))
                        {
                            byte[] computedHmac = hmac.ComputeHash(content);

                            inputStream.Position = contentLength;
                            byte[] storedHmac = new byte[32];
                            await inputStream.ReadAsync(storedHmac, 0, storedHmac.Length, cancellationToken);

                            if(!CryptographicOperations.FixedTimeEquals(computedHmac, storedHmac))
                            {
                                throw new CryptographicException("Ошибка проверки HMAC. Данные повреждены или пароль неверен.");
                            }
                        }

                        inputStream.Position = salt.Length + iv.Length; // Пропускаем соль и IV

                        using(CryptoStream cryptoStream = new CryptoStream(inputStream, aes.CreateDecryptor(), CryptoStreamMode.Read))
                        {
                            // Расшифровка и чтение расширения файла
                            byte[] extensionLengthBytes = new byte[sizeof(int)];
                            await cryptoStream.ReadAsync(extensionLengthBytes, 0, sizeof(int), cancellationToken);
                            int extensionLength = BitConverter.ToInt32(extensionLengthBytes, 0);

                            byte[] extensionBytes = new byte[extensionLength];
                            await cryptoStream.ReadAsync(extensionBytes, 0, extensionBytes.Length, cancellationToken);
                            string fileExtension = System.Text.Encoding.UTF8.GetString(extensionBytes);

                            // Расшифровка содержимого файла
                            using(FileStream outputStream = new FileStream(outputFile + fileExtension, FileMode.Create, FileAccess.Write))
                            {
                                byte[] buffer = new byte[8192];
                                long totalBytes = contentLength - (salt.Length + iv.Length + sizeof(int) + extensionLength);
                                long processedBytes = 0;
                                int bytesRead;

                                while((bytesRead = await cryptoStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                                {
                                    await outputStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                                    processedBytes += bytesRead;
                                    progress?.Report((double)processedBytes / totalBytes);
                                    cancellationToken.ThrowIfCancellationRequested();
                                }
                            }
                        }
                    }
                }
            } catch(OperationCanceledException)
            {
                Console.Error.WriteLine("Операция расшифровки была отменена.");
                throw;
            } catch(CryptographicException ex)
            {
                Console.Error.WriteLine($"Криптографическая ошибка при расшифровке файла: {ex.Message}");
                throw;
            } catch(Exception ex)
            {
                Console.Error.WriteLine($"Ошибка при расшифровке файла: {ex.Message}");
                throw;
            }
        }
    }

}

