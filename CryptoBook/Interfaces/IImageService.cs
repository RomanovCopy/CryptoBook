using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CryptoBook.Interfaces
{
    public interface IImageService
    {
        /// <summary>Присоединить/отсоединить сервис к конкретному редактору.</summary>
        void Attach(RichTextBox editor);
        void Detach();
        bool IsAttached { get; }
        RichTextBox? Editor { get; }

        // --------- Загрузка (получение) источников изображения ---------

        BitmapSource LoadFromFile(string path, ImageLoadOptions? options = null);
        BitmapSource LoadFromStream(Stream stream, ImageLoadOptions? options = null);
        BitmapSource? LoadFromClipboard(ImageLoadOptions? options = null);

        // --------- Вставка/размещение ---------

        /// <summary>Вставить изображение инлайном в позицию каретки или заменить выделение.</summary>
        InlineUIContainer InsertInline(BitmapSource source, InlineInsertOptions? options = null);

        /// <summary>Вставить изображение как блок (BlockUIContainer) в отдельной строке/параграфе.</summary>
        BlockUIContainer InsertBlock(BitmapSource source, BlockInsertOptions? options = null);

        /// <summary>
        /// Вставить изображение в Figure с обтеканием текстом (WrapDirection), позиционированием и якорями.
        /// Figure помещается внутрь текущего параграфа.
        /// </summary>
        Figure InsertFigure(BitmapSource source, FigureInsertOptions options);

        /// <summary>Заменить текущее выделение изображением (инлайн).</summary>
        InlineUIContainer ReplaceSelection(BitmapSource source, InlineInsertOptions? options = null);

        // --------- Поиск/доступ к изображению ---------

        /// <summary>Найти Image под кареткой (или ближайший справа) — удобно для редактирования.</summary>
        Image? GetImageAtCaret();

        /// <summary>Попытаться найти Image в указанной позиции.</summary>
        bool TryGetImageAt(TextPointer position, out Image image);

        /// <summary>Вернуть все Image в документе (включая те, что внутри Figure/BlockUIContainer).</summary>
        IReadOnlyList<Image> GetAllImages();

        // --------- Масштабирование и свойства ---------

        /// <summary>Установить точные размеры в DIP. Если heightDip = null — сохранить пропорции.</summary>
        void SetSize(Image image, double widthDip, double? heightDip = null);

        /// <summary>Масштабировать относительно текущего размера (например, 1.25 = +25%).</summary>
        void Scale(Image image, double scaleFactor);

        /// <summary>
        /// Подогнать под ширину контента (учитывает PageWidth/PagePadding/ColumnWidth, если заданы).
        /// optionalPadding — пользовательские поля слева/справа, прибавляемые к расчёту.
        /// </summary>
        void FitToContentWidth(Image image, double optionalPadding = 0);

        /// <summary>Задать режим интерполяции размер/кадрирования.</summary>
        void SetStretch(Image image, Stretch stretch);

        /// <summary>Выравнивание baseline для инлайн-контейнера.</summary>
        void SetInlineBaseline(InlineUIContainer container, BaselineAlignment baseline);

        /// <summary>Выравнивание блока (лево/центр/право) через окружающий абзац.</summary>
        void SetBlockAlignment(BlockUIContainer container, TextAlignment alignment);

        /// <summary>Переустановить параметры Figure (обтекание, якоря, размеры, смещения).</summary>
        void SetFigurePlacement(Figure figure, FigurePlacementOptions options);

        /// <summary>Переместить изображение в новую позицию документа.</summary>
        void MoveBefore(Image image, TextPointer insertionPosition);

        /// <summary>Удалить изображение и его контейнер из документа.</summary>
        bool Remove(Image image);

        // --------- Утилиты/метаданные ---------

        System.Windows.Size GetSizeDip(Image image);
        (double dpiX, double dpiY) GetDpi(Image image);

        // --------- События ---------

        event EventHandler<ImageInsertedEventArgs> ImageInserted;
        event EventHandler<ImageChangedEventArgs> ImageChanged;
        event EventHandler<ImageRemovedEventArgs> ImageRemoved;



        // ==================== Options / DTOs ====================

        /// <summary>Опции загрузки битмапа: экономичная декодировка и освобождение файловых дескрипторов.</summary>
        public sealed record ImageLoadOptions(
            int? DecodePixelWidth = null,
            int? DecodePixelHeight = null,
            BitmapCacheOption CacheOption = BitmapCacheOption.OnLoad,
            bool Freeze = true
        );

        /// <summary>Общие визуальные опции изображения.</summary>
        public sealed record ImageVisualOptions(
            double? WidthDip = null,
            double? HeightDip = null,
            Stretch Stretch = Stretch.Uniform,
            string? AltText = null // можно хранить в AttachedProperty для доступности
        );

        public sealed record InlineInsertOptions(
            ImageVisualOptions? Visual = null,
            BaselineAlignment Baseline = BaselineAlignment.Baseline
        )
        {
            public InlineInsertOptions() : this(new ImageVisualOptions(), BaselineAlignment.Baseline) { }
        }

        public sealed record BlockInsertOptions(
            ImageVisualOptions? Visual = null,
            TextAlignment Alignment = TextAlignment.Left
        )
        {
            public BlockInsertOptions() : this(new ImageVisualOptions(), TextAlignment.Left) { }
        }

        /// <summary>Опции Figure для обтекания и позиционирования.</summary>
        public sealed record FigureInsertOptions(
            ImageVisualOptions Visual,
            WrapDirection WrapDirection,
            FigureLength Width,
            FigureLength Height,
            FigureHorizontalAnchor HorizontalAnchor,
            FigureVerticalAnchor VerticalAnchor,
            double HorizontalOffset = 0,
            double VerticalOffset = 0
        )
        {
            public static FigureInsertOptions LeftWrap(double pixelWidthDip, double? pixelHeightDip = null) =>
                new FigureInsertOptions(
                    new ImageVisualOptions(pixelWidthDip, pixelHeightDip),
                    WrapDirection.Right,                           // текст справа от картинки
                    new FigureLength(pixelWidthDip, FigureUnitType.Pixel),
                    new FigureLength(double.NaN, FigureUnitType.Auto),
                    FigureHorizontalAnchor.ContentLeft,
                    FigureVerticalAnchor.ParagraphTop
                );

            public static FigureInsertOptions RightWrap(double pixelWidthDip, double? pixelHeightDip = null) =>
                new FigureInsertOptions(
                    new ImageVisualOptions(pixelWidthDip, pixelHeightDip),
                    WrapDirection.Left,                            // текст слева от картинки
                    new FigureLength(pixelWidthDip, FigureUnitType.Pixel),
                    new FigureLength(double.NaN, FigureUnitType.Auto),
                    FigureHorizontalAnchor.ContentRight,
                    FigureVerticalAnchor.ParagraphTop
                );
        }

        public sealed record FigurePlacementOptions(
            WrapDirection WrapDirection,
            FigureLength Width,
            FigureLength Height,
            FigureHorizontalAnchor HorizontalAnchor,
            FigureVerticalAnchor VerticalAnchor,
            double HorizontalOffset = 0,
            double VerticalOffset = 0
        );

        // ==================== Events ====================

        public enum ImageChangeKind { Size, Stretch, Position, Wrap, Alignment, Dpi, Other }

        public sealed class ImageInsertedEventArgs: EventArgs
        {
            public Image Image { get; }
            public TextPointer Position { get; }
            public TextElement Container { get; } // InlineUIContainer | BlockUIContainer | Figure
            public ImageInsertedEventArgs(Image image, TextPointer position, TextElement container)
            {
                Image = image;
                Position = position;
                Container = container;
            }
        }

        public sealed class ImageChangedEventArgs: EventArgs
        {
            public Image Image { get; }
            public ImageChangeKind Kind { get; }
            public ImageChangedEventArgs(Image image, ImageChangeKind kind)
            {
                Image = image;
                Kind = kind;
            }
        }

        public sealed class ImageRemovedEventArgs: EventArgs
        {
            public TextPointer RemovePosition { get; }
            public ImageRemovedEventArgs(TextPointer removePosition) => RemovePosition = removePosition;
        }
    }


}
