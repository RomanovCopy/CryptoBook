using CryptoBook.Interfaces;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CryptoBook.Services
{
    public sealed class SystemIconService:ISystemIconService
    {

        private readonly ConcurrentDictionary<string, ImageSource> _cache = new(StringComparer.OrdinalIgnoreCase);


        public ImageSource GetFolderIcon(SystemIconSize size = SystemIconSize.Small, bool open = false)
        {
            // open-иконку достать через SHGetFileInfo тоже можно, но проще ключом отличать.
            // Для open обычно нужен реальный PIDL/IShellItem (сложнее). Здесь оставим закрытую папку.
            var key = $"folder|{size}|{open}";
            return _cache.GetOrAdd(key, _ => GetFromShell(
                fakePathOrExt: "folder",
                isFolder: true,
                size: size));
        }

        public ImageSource GetIconForPath(string path, SystemIconSize size = SystemIconSize.Small)
        {
            if(string.IsNullOrWhiteSpace(path))
                return GetIconForExtension(".bin", size);

            path = NormalizeDriveRoot(path);

            // ✅ 1) Диск (root) — хотим настоящую иконку диска
            if(IsDriveRoot(path))
            {
                // Важно: диски существуют, поэтому можно без USEFILEATTRIBUTES
                var key = $"drive|{path}|{size}";
                return _cache.GetOrAdd(key, _ => GetFromShellRealPath(path, size));
            }

            // Для директорий
            if(Directory.Exists(path))
                return GetFolderIcon(size);

            // Для файлов по расширению (чтобы не трогать диск) лучше уйти в GetIconForExtension
            var ext = Path.GetExtension(path);
            return GetIconForExtension(ext, size);
        }

        public ImageSource GetIconForExtension(string extension, SystemIconSize size = SystemIconSize.Small)
        {
            extension = NormalizeExt(extension);
            var key = $"ext|{extension}|{size}";

            return _cache.GetOrAdd(key, _ => GetFromShell(
                fakePathOrExt: extension,
                isFolder: false,
                size: size));
        }


        private static string NormalizeExt(string ext)
        {
            if(string.IsNullOrWhiteSpace(ext))
                return ".bin";
            ext = ext.Trim();
            if(!ext.StartsWith(".", StringComparison.Ordinal))
                ext = "." + ext;
            return ext;
        }

        private static string NormalizeDriveRoot(string path)
        {
            // "C:" -> "C:\"
            if(path.Length == 2 && char.IsLetter(path[0]) && path[1] == ':')
                return path + "\\";
            return path;
        }

        private static bool IsDriveRoot(string path)
            => path.Length == 3 && char.IsLetter(path[0]) && path[1] == ':' &&
               (path[2] == '\\' || path[2] == '/');


        // 2) Иконка по "реальному пути" (диск/существующий файл/спец-папки)
        private static ImageSource GetFromShellRealPath(string realPath, SystemIconSize size)
        {
            var flags = SHGFI_ICON;
            flags |= size == SystemIconSize.Small ? SHGFI_SMALLICON : SHGFI_LARGEICON;

            var shfi = new SHFILEINFO();
            // dwFileAttributes = 0, без USEFILEATTRIBUTES
            var res = SHGetFileInfo(realPath, 0, ref shfi, (uint)Marshal.SizeOf(shfi), flags);

            if(res == IntPtr.Zero || shfi.hIcon == IntPtr.Zero)
                return MakeFallback();

            try
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(shfi.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                if(source.CanFreeze)
                    source.Freeze();
                return source;
            } finally
            {
                DestroyIcon(shfi.hIcon);
            }
        }

        private static ImageSource GetFromShell(string fakePathOrExt, bool isFolder, SystemIconSize size)
        {
            // Мы не хотим зависеть от наличия файла: используем SHGFI_USEFILEATTRIBUTES.
            // Для файлов передаём ".txt", для папки — что угодно + атрибут DIRECTORY.
            var attrs = isFolder ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;

            var flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES;
            flags |= size == SystemIconSize.Small ? SHGFI_SMALLICON : SHGFI_LARGEICON;

            var shfi = new SHFILEINFO();
            var res = SHGetFileInfo(fakePathOrExt, attrs, ref shfi, (uint)Marshal.SizeOf(shfi), flags);

            if(res == IntPtr.Zero || shfi.hIcon == IntPtr.Zero)
                return MakeFallback();

            try
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(
                    shfi.hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                if(source.CanFreeze)
                    source.Freeze(); // важно для кросс-потокового использования/кэша
                return source;
            } finally
            {
                DestroyIcon(shfi.hIcon);
            }
        }

        private static ImageSource MakeFallback()
        {
            // Минимальный fallback (прозрачный 1x1), чтобы не падать.
            var bmp = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4);
            if(bmp.CanFreeze)
                bmp.Freeze();
            return bmp;
        }

        #region P/Invoke

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000;
        private const uint SHGFI_SMALLICON = 0x000000001;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;

        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            ref SHFILEINFO psfi,
            uint cbFileInfo,
            uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        #endregion
    }
}
