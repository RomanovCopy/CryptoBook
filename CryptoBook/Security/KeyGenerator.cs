using System.Security.Cryptography;
using System.Text;

namespace CryptoBook.Security
{
    internal class KeyGenerator
    {
        public static byte[] GenerateKeyFromPassword( ReadOnlySpan<char> password, byte[] salt)
        {
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password.ToArray());

            try
            {
                using Rfc2898DeriveBytes rfc2898 =new Rfc2898DeriveBytes( passwordBytes, salt, 100_000, HashAlgorithmName.SHA256);

                return rfc2898.GetBytes(32);
            } 
            finally
            {
                CryptographicOperations.ZeroMemory(passwordBytes);
            }
        }
    }
}
