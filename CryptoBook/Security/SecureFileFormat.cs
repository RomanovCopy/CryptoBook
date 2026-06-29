using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Security
{
    internal static class SecureFileFormat
    {
        public static readonly byte[] MagicHeader = Encoding.ASCII.GetBytes("CRYPTOBOOK");

        public const int SaltSize = 16;
        public const int HmacSize = 32;
        public const int BufferSize = 8192;
        public const int MaxExtensionLength = 32;
    }
}
