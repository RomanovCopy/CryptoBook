using Microsoft.Win32.SafeHandles;

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CryptoBook.Services;

internal static class WindowsFileStorageDetails
{
    public static (long AllocationSize, uint Links, string FileId, string VolumeId) Read(string path)
    {
        using SafeFileHandle handle = CreateFileW(path, 0, 7, IntPtr.Zero, 3,
            0x02200000, IntPtr.Zero); // BACKUP_SEMANTICS | OPEN_REPARSE_POINT
        if(handle.IsInvalid || !GetFileInformationByHandle(handle, out var info))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        if(!GetFileInformationByHandleEx(handle, 1, out var standard, (uint)Marshal.SizeOf<FileStandardInformation>()))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return (standard.AllocationSize, info.Links,
            $"{info.IndexHigh:X8}{info.IndexLow:X8}", $"{info.VolumeSerial:X8}");
    }

    public static ulong ReadCompressedSize(string path)
    {
        uint low = GetCompressedFileSizeW(path, out uint high);
        if(low == uint.MaxValue && Marshal.GetLastWin32Error() != 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return ((ulong)high << 32) | low;
    }

    public static IReadOnlyList<(string Name, long Size)> ReadStreams(string path, CancellationToken token)
    {
        IntPtr handle = FindFirstStreamW(path, 0, out var stream, 0);
        if(handle == new IntPtr(-1))
        {
            int error = Marshal.GetLastWin32Error();
            if(error == 38) // No streams (e.g. directory).
                return [];
            throw new Win32Exception(error);
        }
        try
        {
            var streams = new List<(string, long)>();
            do
            {
                token.ThrowIfCancellationRequested();
                streams.Add((stream.Name, stream.Size));
            } while(FindNextStreamW(handle, out stream));
            int error = Marshal.GetLastWin32Error();
            if(error != 38)
                throw new Win32Exception(error);
            return streams;
        }
        finally { FindClose(handle); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileStandardInformation
    {
        public long AllocationSize, EndOfFile;
        public uint NumberOfLinks;
        [MarshalAs(UnmanagedType.U1)] public bool DeletePending;
        [MarshalAs(UnmanagedType.U1)] public bool Directory;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Created, Accessed, Written;
        public uint VolumeSerial, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StreamData
    {
        public long Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 296)] public string Name;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share,
        IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation info);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle file, int infoClass,
        out FileStandardInformation info, uint size);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetCompressedFileSizeW(string name, out uint high);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindFirstStreamW(string name, int level, out StreamData data, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindNextStreamW(IntPtr handle, out StreamData data);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindClose(IntPtr handle);
}
