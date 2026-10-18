using Microsoft.Win32.SafeHandles;

using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace CryptoBook.Services;

internal static class WindowsCatalogSignatureReader
{
    internal sealed record CatalogSignature(string Path, string Algorithm, string Hash, int StatusCode);

    public static IReadOnlyList<CatalogSignature> Read(string path, CancellationToken token)
    {
        var signatures = new List<CatalogSignature>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using SafeFileHandle file = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        foreach(string algorithm in new[] { "SHA256", "SHA1" })
        {
            token.ThrowIfCancellationRequested();
            if(!CryptCATAdminAcquireContext2(out IntPtr admin, IntPtr.Zero, algorithm, IntPtr.Zero, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                uint size = 0;
                if(!CryptCATAdminCalcHashFromFileHandle2(admin, file, ref size, null, 0))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                byte[] hash = new byte[checked((int)size)];
                if(!CryptCATAdminCalcHashFromFileHandle2(admin, file, ref size, hash, 0))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                IntPtr previous = IntPtr.Zero;
                try
                {
                    while(true)
                    {
                        token.ThrowIfCancellationRequested();
                        // The enumeration frees the previous context when advancing.
                        IntPtr catalog = CryptCATAdminEnumCatalogFromHash(admin, hash, size, 0, ref previous);
                        previous = catalog;
                        if(catalog == IntPtr.Zero) break;
                        var info = new CatalogInfo { Size = (uint)Marshal.SizeOf<CatalogInfo>() };
                        if(!CryptCATCatalogInfoFromContext(catalog, ref info, 0))
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                        if(!seen.Add(info.Path)) continue;
                        int status = new WindowsAuthenticodeVerifier().VerifyCatalogOffline(
                            info.Path, path, hash, file.DangerousGetHandle(), admin);
                        signatures.Add(new(info.Path, algorithm, Convert.ToHexString(hash), status));
                    }
                }
                finally
                {
                    if(previous != IntPtr.Zero) CryptCATAdminReleaseCatalogContext(admin, previous, 0);
                }
            }
            finally { CryptCATAdminReleaseContext(admin, 0); }
        }
        return signatures;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CatalogInfo
    {
        public uint Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Path;
    }
    [DllImport("wintrust.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATAdminAcquireContext2(out IntPtr admin, IntPtr subsystem,
        string algorithm, IntPtr policy, uint flags);
    [DllImport("wintrust.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATAdminCalcHashFromFileHandle2(IntPtr admin, SafeFileHandle file,
        ref uint size, [Out] byte[]? hash, uint flags);
    [DllImport("wintrust.dll", SetLastError = true)]
    private static extern IntPtr CryptCATAdminEnumCatalogFromHash(IntPtr admin, byte[] hash,
        uint size, uint flags, ref IntPtr previous);
    [DllImport("wintrust.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATCatalogInfoFromContext(IntPtr catalog, ref CatalogInfo info, uint flags);
    [DllImport("wintrust.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATAdminReleaseCatalogContext(IntPtr admin, IntPtr catalog, uint flags);
    [DllImport("wintrust.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATAdminReleaseContext(IntPtr admin, uint flags);
}
