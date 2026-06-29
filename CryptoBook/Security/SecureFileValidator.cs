using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Security
{
    internal static class SecureFileValidator
    {
        public static async Task<SecureFileState> GetStateAsync( string filePath, string password, CancellationToken cancellationToken = default)
        {
            try
            {
                using FileStream stream = new FileStream( filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                if(!await HasValidHeaderAsync(stream, cancellationToken))
                    return SecureFileState.NotEncrypted;

                if(stream.Length < GetMinimalFileLength())
                    return SecureFileState.InvalidFormat;

                byte[] salt = new byte[SecureFileFormat.SaltSize];
                await stream.ReadExactlyAsync(salt, cancellationToken);

                byte[] key = KeyGenerator.GenerateKeyFromPassword( password, salt);

                using Aes aes = Aes.Create();

                byte[] iv = new byte[aes.BlockSize / 8];
                await stream.ReadExactlyAsync(iv, cancellationToken);

                aes.Key = key;
                aes.IV = iv;

                long contentLength = stream.Length
                    - SecureFileFormat.HmacSize;

                if(!await IsHmacValidAsync( stream, contentLength, key, cancellationToken))
                {
                    return SecureFileState.EncryptedWithInvalidPasswordOrDamaged;
                }

                stream.Position = SecureFileFormat.MagicHeader.Length + SecureFileFormat.SaltSize + iv.Length;

                using CryptoStream cryptoStream = new CryptoStream( stream, aes.CreateDecryptor(), CryptoStreamMode.Read);

                byte[] extensionLengthBytes = new byte[sizeof(int)];
                await cryptoStream.ReadExactlyAsync( extensionLengthBytes, cancellationToken);

                int extensionLength = BitConverter.ToInt32( extensionLengthBytes, 0);

                if(extensionLength <= 0 || extensionLength > SecureFileFormat.MaxExtensionLength)
                {
                    return SecureFileState.InvalidFormat;
                }

                byte[] extensionBytes = new byte[extensionLength];
                await cryptoStream.ReadExactlyAsync( extensionBytes, cancellationToken);

                string extension = Encoding.UTF8.GetString(extensionBytes);

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

        public static async Task<bool> IsEncryptedAsync( string filePath, string password, CancellationToken cancellationToken = default)
        {
            SecureFileState state = await GetStateAsync(
                filePath,
                password,
                cancellationToken);

            return state == SecureFileState.Encrypted;
        }

        public static async Task<bool> HasCryptoBookHeaderAsync( string filePath, CancellationToken cancellationToken = default)
        {
            using FileStream stream = new FileStream( filePath, FileMode.Open,  FileAccess.Read, FileShare.Read);

            return await HasValidHeaderAsync(stream, cancellationToken);
        }

        private static async Task<bool> HasValidHeaderAsync( Stream stream, CancellationToken cancellationToken)
        {
            stream.Position = 0;

            if(stream.Length < SecureFileFormat.MagicHeader.Length)
                return false;

            byte[] header = new byte[SecureFileFormat.MagicHeader.Length];
            await stream.ReadExactlyAsync(header, cancellationToken);

            return header.SequenceEqual(SecureFileFormat.MagicHeader);
        }

        private static async Task<bool> IsHmacValidAsync( Stream stream, long contentLength, byte[] key, CancellationToken cancellationToken)
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

        private static long GetMinimalFileLength()
        {
            using Aes aes = Aes.Create();

            return SecureFileFormat.MagicHeader.Length + SecureFileFormat.SaltSize + aes.BlockSize / 8 + sizeof(int) + SecureFileFormat.HmacSize;
        }
    }
}
