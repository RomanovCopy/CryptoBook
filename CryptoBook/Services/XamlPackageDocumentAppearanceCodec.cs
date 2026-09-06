using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows.Documents;
using System.Windows.Media.Imaging;

using Media = System.Windows.Media;

namespace CryptoBook.Services
{
    /// <summary>
    /// Сохраняет свойства уровня FlowDocument в дополнительных записях
    /// XamlPackage. Стандартный TextRange сериализует только содержимое диапазона
    /// и поэтому не переносит фон самого документа.
    /// </summary>
    internal static class XamlPackageDocumentAppearanceCodec
    {
        private const int CurrentVersion = 1;
        private const int MaximumMetadataLength = 16 * 1024;
        private const long MaximumImageLength = 64L * 1024 * 1024;
        private const string MetadataEntryPath =
            "CryptoBook/DocumentAppearance.json";
        private const string BackgroundImageEntryPath =
            "CryptoBook/DocumentBackground.png";
        private const string ColorKind = "color";
        private const string ImageKind = "image";

        private static readonly JsonSerializerOptions jsonOptions =
            new(JsonSerializerDefaults.Web);

        public static byte[] Preserve(
            FlowDocument document,
            byte[] packageContent)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(packageContent);

            AppearanceMetadata? metadata = CreateMetadata(
                document.Background);
            if(metadata is null)
                return packageContent;

            using var package = new MemoryStream(
                packageContent.Length + 1024);
            package.Write(packageContent);
            package.Position = 0;

            using(var archive = new ZipArchive(
                package,
                ZipArchiveMode.Update,
                leaveOpen: true))
            {
                DeleteIfPresent(archive, MetadataEntryPath);
                DeleteIfPresent(archive, BackgroundImageEntryPath);

                if(document.Background is Media.ImageBrush
                    {
                        ImageSource: BitmapSource bitmap
                    })
                {
                    WriteBackgroundImage(archive, bitmap);
                }

                ZipArchiveEntry metadataEntry = archive.CreateEntry(
                    MetadataEntryPath,
                    CompressionLevel.Optimal);
                using Stream metadataStream = metadataEntry.Open();
                JsonSerializer.Serialize(
                    metadataStream,
                    metadata,
                    jsonOptions);
            }

            return package.ToArray();
        }

        public static void Restore(
            FlowDocument document,
            ReadOnlyMemory<byte> packageContent)
        {
            ArgumentNullException.ThrowIfNull(document);
            if(packageContent.Length == 0)
                return;

            try
            {
                using MemoryStream package = CreateReadStream(packageContent);
                using var archive = new ZipArchive(
                    package,
                    ZipArchiveMode.Read,
                    leaveOpen: false);
                ZipArchiveEntry? metadataEntry =
                    archive.GetEntry(MetadataEntryPath);
                if(metadataEntry is null ||
                   metadataEntry.Length <= 0 ||
                   metadataEntry.Length > MaximumMetadataLength)
                {
                    return;
                }

                AppearanceMetadata? metadata;
                using(Stream metadataStream = metadataEntry.Open())
                {
                    metadata = JsonSerializer.Deserialize<AppearanceMetadata>(
                        metadataStream,
                        jsonOptions);
                }

                if(metadata?.Version != CurrentVersion)
                    return;

                Media.Brush? background = metadata.BackgroundKind switch
                {
                    ColorKind => RestoreColor(metadata),
                    ImageKind => RestoreImage(archive, metadata),
                    _ => null
                };
                if(background is not null)
                    document.Background = background;
            }
            catch(Exception exception) when(
                exception is InvalidDataException or
                    IOException or
                    JsonException or
                    NotSupportedException or
                    FormatException or
                    ArgumentException)
            {
                // Оформление является необязательной частью пакета. Повреждённые
                // или неизвестные метаданные не должны мешать открыть сам текст.
            }
        }

        private static AppearanceMetadata? CreateMetadata(
            Media.Brush? background)
        {
            if(background is Media.SolidColorBrush solid)
            {
                return new AppearanceMetadata
                {
                    Version = CurrentVersion,
                    BackgroundKind = ColorKind,
                    ColorArgb = ToArgb(solid.Color)
                };
            }

            if(background is Media.ImageBrush
                {
                    ImageSource: BitmapSource
                } image)
            {
                return new AppearanceMetadata
                {
                    Version = CurrentVersion,
                    BackgroundKind = ImageKind,
                    Stretch = (int)image.Stretch,
                    AlignmentX = (int)image.AlignmentX,
                    AlignmentY = (int)image.AlignmentY,
                    Opacity = image.Opacity
                };
            }

            return null;
        }

        private static Media.Brush? RestoreColor(
            AppearanceMetadata metadata)
        {
            if(metadata.ColorArgb is not uint argb)
                return null;

            var brush = new Media.SolidColorBrush(FromArgb(argb));
            brush.Freeze();
            return brush;
        }

        private static Media.Brush? RestoreImage(
            ZipArchive archive,
            AppearanceMetadata metadata)
        {
            ZipArchiveEntry? imageEntry =
                archive.GetEntry(BackgroundImageEntryPath);
            if(imageEntry is null ||
               imageEntry.Length <= 0 ||
               imageEntry.Length > MaximumImageLength)
            {
                return null;
            }

            using var imageBuffer = new MemoryStream(
                checked((int)imageEntry.Length));
            using(Stream imageStream = imageEntry.Open())
                imageStream.CopyTo(imageBuffer);
            imageBuffer.Position = 0;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            bitmap.StreamSource = imageBuffer;
            bitmap.EndInit();
            bitmap.Freeze();

            var brush = new Media.ImageBrush(bitmap)
            {
                Stretch = GetEnumOrDefault(
                    metadata.Stretch,
                    System.Windows.Media.Stretch.UniformToFill),
                AlignmentX = GetEnumOrDefault(
                    metadata.AlignmentX,
                    System.Windows.Media.AlignmentX.Center),
                AlignmentY = GetEnumOrDefault(
                    metadata.AlignmentY,
                    System.Windows.Media.AlignmentY.Center),
                Opacity = double.IsFinite(metadata.Opacity) &&
                    metadata.Opacity is >= 0 and <= 1
                        ? metadata.Opacity
                        : 1
            };
            brush.Freeze();
            return brush;
        }

        private static void WriteBackgroundImage(
            ZipArchive archive,
            BitmapSource bitmap)
        {
            using var encodedImage = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(encodedImage);
            if(encodedImage.Length > MaximumImageLength)
            {
                throw new InvalidDataException(
                    Infrastructure.LocalizationManager.GetString(
                        "Document.BackgroundImageTooLarge"));
            }

            ZipArchiveEntry imageEntry = archive.CreateEntry(
                BackgroundImageEntryPath,
                CompressionLevel.NoCompression);
            using Stream imageStream = imageEntry.Open();
            encodedImage.Position = 0;
            encodedImage.CopyTo(imageStream);
        }

        private static MemoryStream CreateReadStream(
            ReadOnlyMemory<byte> content)
        {
            if(System.Runtime.InteropServices.MemoryMarshal.TryGetArray(
                content,
                out ArraySegment<byte> segment))
            {
                return new MemoryStream(
                    segment.Array!,
                    segment.Offset,
                    segment.Count,
                    writable: false,
                    publiclyVisible: true);
            }

            return new MemoryStream(content.ToArray(), writable: false);
        }

        private static TEnum GetEnumOrDefault<TEnum>(
            int value,
            TEnum fallback)
            where TEnum: struct, Enum =>
            Enum.IsDefined(typeof(TEnum), value)
                ? (TEnum)Enum.ToObject(typeof(TEnum), value)
                : fallback;

        private static uint ToArgb(Media.Color color) =>
            ((uint)color.A << 24) |
            ((uint)color.R << 16) |
            ((uint)color.G << 8) |
            color.B;

        private static Media.Color FromArgb(uint argb) =>
            Media.Color.FromArgb(
                (byte)(argb >> 24),
                (byte)(argb >> 16),
                (byte)(argb >> 8),
                (byte)argb);

        private static void DeleteIfPresent(
            ZipArchive archive,
            string entryPath) =>
            archive.GetEntry(entryPath)?.Delete();

        private sealed class AppearanceMetadata
        {
            public int Version { get; init; }
            public string? BackgroundKind { get; init; }
            public uint? ColorArgb { get; init; }
            public int Stretch { get; init; } =
                (int)System.Windows.Media.Stretch.UniformToFill;
            public int AlignmentX { get; init; } =
                (int)System.Windows.Media.AlignmentX.Center;
            public int AlignmentY { get; init; } =
                (int)System.Windows.Media.AlignmentY.Center;
            public double Opacity { get; init; } = 1;
        }
    }
}
