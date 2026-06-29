using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Security
{
    internal enum SecureFileState
    {
        NotEncrypted,
        Encrypted,
        EncryptedWithInvalidPasswordOrDamaged,
        InvalidFormat
    }
}
