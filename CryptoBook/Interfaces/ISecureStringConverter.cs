using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface ISecureStringConverter
    {
        char[] ToCharArray(SecureString secureString);

        bool ContentEquals( SecureString left, SecureString right);
    }
}
