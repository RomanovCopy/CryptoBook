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
        public static readonly byte[] V2MagicHeader = Encoding.ASCII.GetBytes("CBKSEC02");

        public const int SaltSize = 16;
        public const int NonceSize = 12;
        public const int GcmTagSize = 16;
        public const int V2ChunkSize = 1024 * 1024;
        public const int HmacSize = 32;
        public const int BufferSize = 8192;
        public const int MaxExtensionLength = 32;
    }
}
