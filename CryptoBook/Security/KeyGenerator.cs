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
            using(var rfc2898 = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256))
            {
                return rfc2898.GetBytes(32); // Генерация 256-битного ключа
            }
        }
    }
}
