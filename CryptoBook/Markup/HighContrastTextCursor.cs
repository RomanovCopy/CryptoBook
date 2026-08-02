using System.IO;

using WpfCursor = System.Windows.Input.Cursor;

namespace CryptoBook.Markup
{
    /// <summary>
    /// Предоставляет представлению общий I-beam курсор с контрастным контуром.
    /// </summary>
    public static class HighContrastTextCursor
    {
        private const int Width = 32;
        private const int Height = 32;
        private const int BytesPerPixel = 4;
        private const int MaskRowSize = 4;

        public static WpfCursor Instance { get; } = Create();

        private static WpfCursor Create()
        {
            byte[] pixels = CreatePixels();
            byte[] transparencyMask = CreateTransparencyMask(pixels);
            const int bitmapHeaderSize = 40;
            int imageSize = bitmapHeaderSize + pixels.Length + transparencyMask.Length;

            var stream = new MemoryStream(capacity: 22 + imageSize);
            using(var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                // Заголовок CUR и единственная запись изображения.
                writer.Write((ushort)0);
                writer.Write((ushort)2);
                writer.Write((ushort)1);
                writer.Write((byte)Width);
                writer.Write((byte)Height);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)(Width / 2));
                writer.Write((ushort)(Height / 2));
                writer.Write(imageSize);
                writer.Write(22);

                // Высота DIB включает цветное изображение и AND-маску.
                writer.Write(bitmapHeaderSize);
                writer.Write(Width);
                writer.Write(Height * 2);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write(0);
                writer.Write(pixels.Length);
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
                writer.Write(pixels);
                writer.Write(transparencyMask);
            }

            stream.Position = 0;
            return new WpfCursor(stream);
        }

        private static byte[] CreatePixels()
        {
            var pixels = new byte[Width * Height * BytesPerPixel];
            for(int y = 0; y < Height; y++)
            {
                for(int x = 0; x < Width; x++)
                {
                    bool core = IsCore(x, y);
                    bool outline = !core && HasCoreNeighbour(x, y);
                    if(!core && !outline)
                        continue;

                    int offset = ((Height - 1 - y) * Width + x) * BytesPerPixel;
                    byte channel = outline ? (byte)255 : (byte)0;
                    pixels[offset] = channel;
                    pixels[offset + 1] = channel;
                    pixels[offset + 2] = channel;
                    pixels[offset + 3] = 255;
                }
            }

            return pixels;
        }

        private static byte[] CreateTransparencyMask(byte[] pixels)
        {
            var mask = new byte[Height * MaskRowSize];
            for(int y = 0; y < Height; y++)
            {
                for(int x = 0; x < Width; x++)
                {
                    int pixelOffset = ((Height - 1 - y) * Width + x) * BytesPerPixel;
                    if(pixels[pixelOffset + 3] != 0)
                        continue;

                    int maskOffset = (Height - 1 - y) * MaskRowSize + x / 8;
                    mask[maskOffset] |= (byte)(1 << (7 - x % 8));
                }
            }

            return mask;
        }

        private static bool IsCore(int x, int y) =>
            x is >= 14 and <= 16 && y is >= 4 and <= 27 ||
            x is >= 10 and <= 20 && (y is >= 4 and <= 6 || y is >= 25 and <= 27);

        private static bool HasCoreNeighbour(int x, int y)
        {
            for(int dy = -1; dy <= 1; dy++)
            {
                for(int dx = -1; dx <= 1; dx++)
                {
                    if(IsCore(x + dx, y + dy))
                        return true;
                }
            }

            return false;
        }
    }
}
