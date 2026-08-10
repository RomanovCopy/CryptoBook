using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace CryptoBook.Services
{
    public sealed class WindowsFilePropertiesService: IFilePropertiesService
    {
        public LaunchResult Show(string path)
        {
            if(string.IsNullOrWhiteSpace(path))
                return LaunchResult.Fail("shell:properties", "", "Path is empty.");

            path = ToNativePath(path.Trim());
            if(!File.Exists(path) && !Directory.Exists(path))
            {
                return LaunchResult.Fail(
                    "shell:properties",
                    path,
                    $"Path not found: {path}");
            }

            var info = new ShellExecuteInfo
            {
                Size = Marshal.SizeOf<ShellExecuteInfo>(),
                Mask = SeeMaskInvokeIdList,
                Verb = "properties",
                File = path,
                Show = ShowNormal
            };

            if(ShellExecuteEx(ref info))
                return LaunchResult.Ok("shell:properties", path);

            int errorCode = Marshal.GetLastWin32Error();
            return LaunchResult.Fail(
                "shell:properties",
                path,
                new Win32Exception(errorCode).Message);
        }

        private const uint SeeMaskInvokeIdList = 0x0000000C;
        private const int ShowNormal = 1;

        private static string ToNativePath(string path)
        {
            int separatorIndex = path.IndexOf("://", StringComparison.Ordinal);
            return separatorIndex > 0 ? path[(separatorIndex + 3)..] : path;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ShellExecuteInfo
        {
            public int Size;
            public uint Mask;
            public IntPtr OwnerWindow;
            [MarshalAs(UnmanagedType.LPWStr)] public string? Verb;
            [MarshalAs(UnmanagedType.LPWStr)] public string? File;
            [MarshalAs(UnmanagedType.LPWStr)] public string? Parameters;
            [MarshalAs(UnmanagedType.LPWStr)] public string? Directory;
            public int Show;
            public IntPtr Instance;
            public IntPtr ItemIdList;
            [MarshalAs(UnmanagedType.LPWStr)] public string? Class;
            public IntPtr ClassKey;
            public uint HotKey;
            public IntPtr IconOrMonitor;
            public IntPtr Process;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShellExecuteEx(ref ShellExecuteInfo executeInfo);
    }
}
