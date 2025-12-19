using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CryptoBook.Converters
{
    public class FilePathToIconConverter:IValueConverter
    {

        // Кэш для ускорения повторных вызовов
        private static readonly ConcurrentDictionary<string, ImageSource> _iconCache = new ConcurrentDictionary<string, ImageSource>();

        #region PInvoke

        [DllImport("Shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [StructLayout(LayoutKind.Sequential)]
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

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_LARGEICON = 0x0;  // 32x32
        private const uint SHGFI_SMALLICON = 0x1;  // 16x16

        [DllImport("User32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        #endregion

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string filePath = value as string;
            if(string.IsNullOrEmpty(filePath))
                return null;

            int size = 32;
            if(parameter != null && int.TryParse(parameter.ToString(), out int parsedSize))
                size = parsedSize;

            // Используем ключ:расширение + размер
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            string cacheKey = $"{ext}_{size}";

            if(_iconCache.TryGetValue(cacheKey, out ImageSource cached))
                return cached;

            ImageSource result = GetIcon(filePath, size);

            if(result != null)
                _iconCache[cacheKey] = result; // сохраняем в кэш

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private ImageSource? GetIcon(string filePath, int size)
        {
            if(!File.Exists(filePath))
                return null;

            Icon icon = null;

            if(size == 16 || size == 32)
                icon = GetIconViaSHGetFileInfo(filePath, size == 32);

            if(icon == null)
            {
                try
                {
                    icon = Icon.ExtractAssociatedIcon(filePath);
                } catch
                {
                    return null;
                }
            }

            if(icon == null)
                return null;

            using(var bmp = new Bitmap(icon.ToBitmap(), size, size))
            {
                return BitmapToImageSource(bmp);
            }
        }

        private Icon? GetIconViaSHGetFileInfo(string filePath, bool large)
        {
            SHFILEINFO shinfo = new SHFILEINFO();
            uint flags = SHGFI_ICON | (large ? SHGFI_LARGEICON : SHGFI_SMALLICON);
            IntPtr hImg = SHGetFileInfo(filePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);

            if(shinfo.hIcon == IntPtr.Zero)
                return null;

            Icon icon = (Icon)Icon.FromHandle(shinfo.hIcon).Clone();
            DestroyIcon(shinfo.hIcon);
            return icon;
        }

        private ImageSource BitmapToImageSource(Bitmap bitmap)
        {
            using(var ms = new MemoryStream())
            {
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Seek(0, SeekOrigin.Begin);
                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.StreamSource = ms;
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                return bi;
            }
        }

    }
}
