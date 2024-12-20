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
        public static void EncryptFile(string inputFile, string outputFile, string password)
        {
            try
            {
                byte[] salt = RandomNumberGenerator.GetBytes(16);
                byte[] key = KeyGenerator.GenerateKeyFromPassword(password, salt);

                using(FileStream outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
                {
                    // Записываем соль
                    outputStream.Write(salt, 0, salt.Length);

                    using(Aes aes = Aes.Create())
                    {
                        aes.Key = key;
                        aes.GenerateIV();

                        // Записываем IV
                        outputStream.Write(aes.IV, 0, aes.IV.Length);

                        using(CryptoStream cryptoStream = new CryptoStream(outputStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
                        {
                            // Сохраняем и шифруем расширение файла
                            string fileExtension = Path.GetExtension(inputFile);
                            byte[] extensionBytes = System.Text.Encoding.UTF8.GetBytes(fileExtension);
                            cryptoStream.Write(BitConverter.GetBytes(extensionBytes.Length), 0, sizeof(int));
                            cryptoStream.Write(extensionBytes, 0, extensionBytes.Length);

                            // Шифруем содержимое файла
                            using(FileStream inputStream = new FileStream(inputFile, FileMode.Open, FileAccess.Read))
                            {
                                inputStream.CopyTo(cryptoStream);
                            }
                        }

                        // Генерация HMAC для проверки целостности
                        using(HMACSHA256 hmac = new HMACSHA256(key))
                        {
                            outputStream.Position = 0;
                            byte[] hmacHash = hmac.ComputeHash(outputStream);
                            outputStream.Write(hmacHash, 0, hmacHash.Length);
                        }
                    }
                }
            } catch(Exception ex)
            {
                Console.Error.WriteLine($"Ошибка при шифровании файла: {ex.Message}");
                throw;
            }
        }

        public static void DecryptFile(string inputFile, string outputFile, string password)
        {
            try
            {
                using(FileStream inputStream = new FileStream(inputFile, FileMode.Open, FileAccess.Read))
                {
                    byte[] salt = new byte[16];
                    inputStream.Read(salt, 0, salt.Length);

                    byte[] key = KeyGenerator.GenerateKeyFromPassword(password, salt);

                    using(Aes aes = Aes.Create())
                    {
                        byte[] iv = new byte[aes.BlockSize / 8];
                        inputStream.Read(iv, 0, iv.Length);
                        aes.Key = key;
                        aes.IV = iv;

                        // Проверка HMAC
                        long hmacPosition = inputStream.Length - 32; // Длина HMACSHA256 = 32 байта
                        inputStream.Position = 0;
                        using(HMACSHA256 hmac = new HMACSHA256(key))
                        {
                            byte[] computedHmac = hmac.ComputeHash(inputStream);
                            inputStream.Position = hmacPosition;
                            byte[] storedHmac = new byte[32];
                            inputStream.Read(storedHmac, 0, storedHmac.Length);

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
                            cryptoStream.Read(extensionLengthBytes, 0, sizeof(int));
                            int extensionLength = BitConverter.ToInt32(extensionLengthBytes, 0);

                            byte[] extensionBytes = new byte[extensionLength];
                            cryptoStream.Read(extensionBytes, 0, extensionBytes.Length);
                            string fileExtension = System.Text.Encoding.UTF8.GetString(extensionBytes);

                            // Расшифровка содержимого файла
                            using(FileStream outputStream = new FileStream(outputFile + fileExtension, FileMode.Create, FileAccess.Write))
                            {
                                cryptoStream.CopyTo(outputStream);
                            }
                        }
                    }
                }
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
