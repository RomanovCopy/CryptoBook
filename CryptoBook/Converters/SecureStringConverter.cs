using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CryptoBook.Interfaces;

using System.Runtime.InteropServices;
using System.Security;

namespace CryptoBook.Converters
{

    public sealed class SecureStringConverter: ISecureStringConverter
    {
        public char[] ToCharArray(SecureString secureString)
        {
            ArgumentNullException.ThrowIfNull(secureString);

            var chars = new char[secureString.Length];
            IntPtr ptr = IntPtr.Zero;

            try
            {
                ptr = Marshal.SecureStringToCoTaskMemUnicode(secureString);
                Marshal.Copy(ptr, chars, 0, chars.Length);

                return chars;
            } finally
            {
                if(ptr != IntPtr.Zero)
                    Marshal.ZeroFreeCoTaskMemUnicode(ptr);
            }
        }

        public bool ContentEquals(
            SecureString left,
            SecureString right)
        {
            char[]? leftChars = null;
            char[]? rightChars = null;

            try
            {
                leftChars = ToCharArray(left);
                rightChars = ToCharArray(right);

                return leftChars.AsSpan().SequenceEqual(rightChars);
            } finally
            {
                if(leftChars != null)
                    Array.Clear(leftChars);

                if(rightChars != null)
                    Array.Clear(rightChars);
            }
        }
    }
}
