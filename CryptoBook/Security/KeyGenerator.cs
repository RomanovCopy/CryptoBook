using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Security
{
    internal class KeyGenerator
    {
        public static byte[] GenerateKeyFromPassword(string password, byte[] salt)
        {
            try
            {
                using(var rfc2898 = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256))
                {
                    return rfc2898.GetBytes(32); // Генерация 256-битного ключа
                }
            } catch(Exception ex)
            {
                Console.Error.WriteLine($"Ошибка генерации ключа: {ex.Message}");
                throw;
            }
        }
    }
}
