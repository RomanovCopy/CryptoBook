using CryptoBook.Interfaces;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

    public sealed class StockIconService: IStockIconService
    {
        private readonly ConcurrentDictionary<CacheKey, ImageSource> _cache = new();

        public ImageSource GetStockIcon(DTO.SHSTOCKICONID id, bool small = true)
        {
            var key = new CacheKey(id, small);

            return _cache.GetOrAdd(key, static k =>
            {
                var info = new DTO.SHSTOCKICONINFO
                {
                    cbSize = (uint)Marshal.SizeOf<DTO.SHSTOCKICONINFO>()
                };

                var flags = DTO.SHGSI.SHGSI_ICON | (k.Small ? DTO.SHGSI.SHGSI_SMALLICON : DTO.SHGSI.SHGSI_LARGEICON);

                int hr = SHGetStockIconInfo(k.Id, flags, ref info);
                if(hr != 0)
                    Marshal.ThrowExceptionForHR(hr);

                if(info.hIcon == IntPtr.Zero)
                    throw new InvalidOperationException($"SHGetStockIconInfo returned S_OK but hIcon is NULL for {k.Id}.");

                try
                {
                    BitmapSource image = Imaging.CreateBitmapSourceFromHIcon(
                        info.hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

                    // Важно: иначе кэширование в WPF будет опасным (thread affinity).
                    image.Freeze();
                    return image;
                } finally
                {
                    DestroyIcon(info.hIcon);
                }
            });
        }

        public void ClearCache() => _cache.Clear();

        private readonly record struct CacheKey(DTO.SHSTOCKICONID Id, bool Small);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHGetStockIconInfo(
            DTO.SHSTOCKICONID siid,
            DTO.SHGSI uFlags,
            ref DTO.SHSTOCKICONINFO psii);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}
