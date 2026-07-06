using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Security
{
    /// <summary>
    /// Класс для проверки целостности и подлинности зашифрованных файлов.
    /// </summary>
    public class SecureFileValidator:ISecureFileValidator
    {
        /// <summary>
        /// Определяет состояние защищённого файла: не зашифрован, зашифрован и корректен,
        /// зашифрован но повреждён/неверный пароль либо имеет неверный формат.
        /// </summary>
        /// <param name="filePath">Путь к файлу для проверки.</param>
        /// <param name="password">Пароль для генерации ключа и проверки HMAC.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Состояние файла в виде SecureFileState.</returns>
        public async Task<SecureFileState> GetStateAsync( string filePath, string password, CancellationToken cancellationToken = default)
        {
            try
            {
                // Открываем поток для чтения файла
                using FileStream stream = new FileStream( filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                // Проверяем «магическую» шапку формата — если нет, файл не зашифрован нашим форматом
                if(!await HasValidHeaderAsync(stream, cancellationToken))
                    return SecureFileState.NotEncrypted;

                // Минимальная возможная длина файла с корректным форматом
                if(stream.Length < GetMinimalFileLength())
                    return SecureFileState.InvalidFormat;

                // Читаем соль, используем её для получения ключа из пароля
                byte[] salt = new byte[SecureFileFormat.SaltSize];
                await stream.ReadExactlyAsync(salt, cancellationToken);

                byte[] key = KeyGenerator.GenerateKeyFromPassword( password, salt);

                using Aes aes = Aes.Create();

                // Читаем IV (инициализационный вектор) для AES
                byte[] iv = new byte[aes.BlockSize / 8];
                await stream.ReadExactlyAsync(iv, cancellationToken);

                aes.Key = key;
                aes.IV = iv;

                // Длина содержимого без HMAC (HMAC находится в конце файла)
                long contentLength = stream.Length - SecureFileFormat.HmacSize;

                // Проверяем HMAC: если неверен — либо неверный пароль, либо файл повреждён
                if(!await IsHmacValidAsync( stream, contentLength, key, cancellationToken))
                {
                    return SecureFileState.EncryptedWithInvalidPasswordOrDamaged;
                }

                // Перемещаемся к зашифрованной области, сразу после шапки + соли + IV
                stream.Position = SecureFileFormat.MagicHeader.Length + SecureFileFormat.SaltSize + iv.Length;

                // Дешифруем поток для чтения метаданных файла (расширения)
                using CryptoStream cryptoStream = new CryptoStream( stream, aes.CreateDecryptor(), CryptoStreamMode.Read);

                // Читаем длину расширения (int)
                byte[] extensionLengthBytes = new byte[sizeof(int)];
                await cryptoStream.ReadExactlyAsync( extensionLengthBytes, cancellationToken);

                int extensionLength = BitConverter.ToInt32( extensionLengthBytes, 0);

                // Проверяем корректность длины расширения
                if(extensionLength <= 0 || extensionLength > SecureFileFormat.MaxExtensionLength)
                {
                    return SecureFileState.InvalidFormat;
                }

                // Читаем сами байты расширения и декодируем в строку
                byte[] extensionBytes = new byte[extensionLength];
                await cryptoStream.ReadExactlyAsync( extensionBytes, cancellationToken);

                string extension = Encoding.UTF8.GetString(extensionBytes);

                // Ожидаем, что расширение начинается с точки, иначе формат некорректен
                if(!extension.StartsWith('.'))
                    return SecureFileState.InvalidFormat;

                return SecureFileState.Encrypted;
            } catch(OperationCanceledException)
            {
                throw;
            } catch(CryptographicException)
            {
                return SecureFileState.EncryptedWithInvalidPasswordOrDamaged;
            } catch(EndOfStreamException)
            {
                return SecureFileState.InvalidFormat;
            } catch(IOException)
            {
                return SecureFileState.InvalidFormat;
            } catch(UnauthorizedAccessException)
            {
                return SecureFileState.InvalidFormat;
            }
        }

        public async Task<bool> IsEncryptedAsync( string filePath, string password, CancellationToken cancellationToken = default)
        {
            SecureFileState state = await GetStateAsync( filePath, password, cancellationToken);

            return state == SecureFileState.Encrypted;
        }

        public async Task<bool> HasCryptoBookHeaderAsync( string filePath, CancellationToken cancellationToken = default)
        {
            using FileStream stream = new FileStream( filePath, FileMode.Open,  FileAccess.Read, FileShare.Read);

            return await HasValidHeaderAsync(stream, cancellationToken);
        }

        private async Task<bool> HasValidHeaderAsync( Stream stream, CancellationToken cancellationToken)
        {
            stream.Position = 0;

            if(stream.Length < SecureFileFormat.MagicHeader.Length)
                return false;

            byte[] header = new byte[SecureFileFormat.MagicHeader.Length];
            await stream.ReadExactlyAsync(header, cancellationToken);

            return header.SequenceEqual(SecureFileFormat.MagicHeader);
        }

        private async Task<bool> IsHmacValidAsync( Stream stream, long contentLength, byte[] key, CancellationToken cancellationToken)
        {
            if(contentLength <= 0)
                return false;

            stream.Position = 0;

            byte[] content = new byte[contentLength];
            await stream.ReadExactlyAsync(content, cancellationToken);

            using HMACSHA256 hmac = new HMACSHA256(key);
            byte[] computedHmac = hmac.ComputeHash(content);

            stream.Position = contentLength;

            byte[] storedHmac = new byte[SecureFileFormat.HmacSize];
            await stream.ReadExactlyAsync(storedHmac, cancellationToken);

            return CryptographicOperations.FixedTimeEquals(
                computedHmac,
                storedHmac);
        }

        private long GetMinimalFileLength()
        {
            using Aes aes = Aes.Create();

            return SecureFileFormat.MagicHeader.Length + SecureFileFormat.SaltSize + aes.BlockSize / 8 + sizeof(int) + SecureFileFormat.HmacSize;
        }
    }
}
