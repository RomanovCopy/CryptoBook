using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Security
{
    public interface IKeyProvider
    {
        bool HasKey { get; }

        void SetKey(ReadOnlySpan<char> password);

        byte[] DeriveKey(byte[] salt);

        void Clear();
    }
}
