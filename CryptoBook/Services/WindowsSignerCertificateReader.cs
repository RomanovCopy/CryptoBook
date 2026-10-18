using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace CryptoBook.Services;

internal static class WindowsSignerCertificateReader
{
    // Query the embedded PKCS#7 message as data; never load the PE as a module.
    public static X509Certificate2? Read(string path)
    {
        IntPtr store = IntPtr.Zero, message = IntPtr.Zero, signer = IntPtr.Zero, context = IntPtr.Zero;
        try
        {
            if(!CryptQueryObject(1, path, (1u << 10) | (1u << 8), 1u << 1, 0,
                out _, out _, out _, out store, out message, IntPtr.Zero))
                return null;
            uint length = 0;
            if(!CryptMsgGetParam(message, 7, 0, IntPtr.Zero, ref length))
                return null;
            signer = Marshal.AllocHGlobal(checked((int)length));
            if(!CryptMsgGetParam(message, 7, 0, signer, ref length))
                return null;
            context = CertFindCertificateInStore(store, 0x10001, 0, 0x000b0000, signer, IntPtr.Zero);
            if(context == IntPtr.Zero)
                return null;
            var certificate = Marshal.PtrToStructure<CertificateContext>(context);
            byte[] bytes = new byte[checked((int)certificate.Length)];
            Marshal.Copy(certificate.Encoded, bytes, 0, bytes.Length);
            return X509CertificateLoader.LoadCertificate(bytes);
        }
        finally
        {
            if(context != IntPtr.Zero) CertFreeCertificateContext(context);
            if(signer != IntPtr.Zero) Marshal.FreeHGlobal(signer);
            if(message != IntPtr.Zero) CryptMsgClose(message);
            if(store != IntPtr.Zero) CertCloseStore(store, 0);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CertificateContext
    {
        public uint Encoding;
        public IntPtr Encoded;
        public uint Length;
        public IntPtr Info, Store;
    }

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptQueryObject(uint type, string path, uint contentFlags,
        uint formatFlags, uint flags, out uint encoding, out uint content, out uint format,
        out IntPtr store, out IntPtr message, IntPtr context);
    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptMsgGetParam(IntPtr message, uint parameter, uint index, IntPtr data, ref uint size);
    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern IntPtr CertFindCertificateInStore(IntPtr store, uint encoding,
        uint flags, uint findType, IntPtr findParameter, IntPtr previous);
    [DllImport("crypt32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CertFreeCertificateContext(IntPtr context);
    [DllImport("crypt32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptMsgClose(IntPtr message);
    [DllImport("crypt32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CertCloseStore(IntPtr store, uint flags);
}
