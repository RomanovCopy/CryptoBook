using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CryptoBook.Services
{
    public class ImageService:IImageService
    {

        //Базовый каркас

        private IRichTextBoxService? _editor;

        public void Attach(IRichTextBoxService editor)
        {
            ArgumentNullException.ThrowIfNull(editor);
            _editor = editor;
        }

        public void Detach()
        {
            _editor = null;
        }

        public bool IsAttached => _editor != null;

        public IRichTextBoxService? Editor => _editor;


        public event EventHandler<IImageService.ImageInsertedEventArgs>? ImageInserted;
        public event EventHandler<IImageService.ImageChangedEventArgs>? ImageChanged;
        public event EventHandler<IImageService.ImageRemovedEventArgs>? ImageRemoved;


        private IRichTextBoxService RequireEditor()
        {
            return _editor ??throw new InvalidOperationException("ImageService is not attached.");
        }


        public ImageService(IRichTextBoxService richTextBoxService)
        {
            Attach(richTextBoxService);
        }


        //Загрузка изображений

        public BitmapSource LoadFromFile(string path, IImageService.ImageLoadOptions? options = null)
        {
            options ??= new();

            using var stream = File.OpenRead(path);

            return LoadFromStream(stream, options);
        }

        public BitmapSource LoadFromStream(Stream stream, IImageService.ImageLoadOptions? options = null)
        {
            options ??= new();

            var bitmap = new BitmapImage();

            bitmap.BeginInit();

            if(options.DecodePixelWidth.HasValue)
                bitmap.DecodePixelWidth =
                    options.DecodePixelWidth.Value;

            if(options.DecodePixelHeight.HasValue)
                bitmap.DecodePixelHeight =
                    options.DecodePixelHeight.Value;

            bitmap.CacheOption = options.CacheOption;
            bitmap.StreamSource = stream;

            bitmap.EndInit();

            if(options.Freeze)
                bitmap.Freeze();

            return bitmap;
        }

        public BitmapSource? LoadFromClipboard(IImageService.ImageLoadOptions? options = null)
        {
            if(!System.Windows.Clipboard.ContainsImage())
                return null;

            return System.Windows.Clipboard.GetImage();
        }


        //Инлайн-вставка

        public InlineUIContainer InsertInline(BitmapSource source, IImageService.InlineInsertOptions? options = null)
        {
            var editor = RequireEditor();

            options ??= new();

            var image = CreateImage( source, options.Visual);

            var container = new InlineUIContainer(image, editor.CaretPosition)
            {
                BaselineAlignment = options.Baseline
            };

            RaiseInserted( image, container, editor.CaretPosition);

            return container;
        }

        //Замена выделения

        public InlineUIContainer ReplaceSelection(BitmapSource source, IImageService.InlineInsertOptions? options = null)
        {
            var editor = RequireEditor();

            editor.Selection.Text = string.Empty;

            return InsertInline(source, options);
        }


        //Блочная вставка

        public BlockUIContainer InsertBlock(BitmapSource source, IImageService.BlockInsertOptions? options = null)
        {
            var editor = RequireEditor();

            options ??= new();

            var image = CreateImage( source, options.Visual);

            var block = new BlockUIContainer(image);

            var paragraph = editor.CaretPosition.Paragraph;

            if(paragraph != null)
            {
                paragraph.SiblingBlocks.InsertAfter( paragraph, block);
            } else
            {
                editor.Document.Blocks.Add(block);
            }

            SetBlockAlignment( block, options.Alignment);

            RaiseInserted( image, block, block.ContentStart);

            return block;
        }


        // Поиск всех изображений

        public IReadOnlyList<System.Windows.Controls.Image> GetAllImages()
        {
            var editor = RequireEditor();

            var result = new List<System.Windows.Controls.Image>();

            Traverse( editor.Document, result);

            return result;
        }

        private static void Traverse( DependencyObject root, ICollection<System.Windows.Controls.Image> images)
        {
            if(root is System.Windows.Controls.Image image)
            {
                images.Add(image);
            }

            var count =
                VisualTreeHelper.GetChildrenCount(root);

            for(int i = 0; i < count; i++)
            {
                Traverse(
                    VisualTreeHelper.GetChild(root, i),
                    images);
            }
        }




        public Figure InsertFigure(BitmapSource source, IImageService.FigureInsertOptions options)
        {
            throw new NotImplementedException();
        }


        public Image? GetImageAtCaret()
        {
            throw new NotImplementedException();
        }

        public bool TryGetImageAt(TextPointer position, out Image image)
        {
            throw new NotImplementedException();
        }


        public void SetSize(Image image, double widthDip, double? heightDip = null)
        {
            throw new NotImplementedException();
        }

        public void Scale(Image image, double scaleFactor)
        {
            throw new NotImplementedException();
        }

        public void FitToContentWidth(Image image, double optionalPadding = 0)
        {
            throw new NotImplementedException();
        }

        public void SetStretch(Image image, Stretch stretch)
        {
            throw new NotImplementedException();
        }

        public void SetInlineBaseline(InlineUIContainer container, BaselineAlignment baseline)
        {
            throw new NotImplementedException();
        }

        public void SetBlockAlignment(BlockUIContainer container, TextAlignment alignment)
        {
            throw new NotImplementedException();
        }

        public void SetFigurePlacement(Figure figure, IImageService.FigurePlacementOptions options)
        {
            throw new NotImplementedException();
        }

        public void MoveBefore(Image image, TextPointer insertionPosition)
        {
            throw new NotImplementedException();
        }

        public bool Remove(Image image)
        {
            throw new NotImplementedException();
        }

        public System.Windows.Size GetSizeDip(Image image)
        {
            throw new NotImplementedException();
        }

        public (double dpiX, double dpiY) GetDpi(Image image)
        {
            throw new NotImplementedException();
        }



        //Создание WPF Image

        private static System.Windows.Controls.Image CreateImage( BitmapSource source, IImageService.ImageVisualOptions? options)
        {
            var image = new System.Windows.Controls.Image
            {
                Source = source
            };

            if(options != null)
            {
                if(options.WidthDip.HasValue)
                    image.Width = options.WidthDip.Value;

                if(options.HeightDip.HasValue)
                    image.Height = options.HeightDip.Value;

                image.Stretch = options.Stretch;

                if(!string.IsNullOrWhiteSpace(options.AltText))
                {
                    AutomationProperties.SetName(
                        image,
                        options.AltText);
                }
            }

            return image;
        }

        //События

        private void RaiseInserted( System.Windows.Controls.Image image, TextElement container, TextPointer position)
        {
            ImageInserted?.Invoke( this, new IImageService.ImageInsertedEventArgs( image, position, container));
        }

        private void RaiseChanged(System.Windows.Controls.Image image, IImageService.ImageChangeKind kind)
        {
            ImageChanged?.Invoke( this, new IImageService.ImageChangedEventArgs( image, kind));
        }
    }

}
}
