using CryptoBook.Interfaces;

using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using WpfImage = System.Windows.Controls.Image;

namespace CryptoBook.Services
{
    public sealed class DocumentImageInserter: IDocumentImageInserter
    {
        private readonly IRichTextBoxService richTextBox;
        private readonly IInlineService inlineService;
        private readonly IEmbeddedImageEditor imageEditor;
        private readonly IEmbeddedImageLayoutService imageLayoutService;
        private readonly IDocumentLayoutMetrics layoutMetrics;
        private readonly IDispatcherService dispatcher;

        public DocumentImageInserter(
            IRichTextBoxService richTextBox,
            IInlineService inlineService,
            IEmbeddedImageEditor imageEditor,
            IEmbeddedImageLayoutService imageLayoutService,
            IDocumentLayoutMetrics layoutMetrics,
            IDispatcherService dispatcher)
        {
            this.richTextBox = richTextBox
                ?? throw new ArgumentNullException(nameof(richTextBox));
            this.inlineService = inlineService
                ?? throw new ArgumentNullException(nameof(inlineService));
            this.imageEditor = imageEditor
                ?? throw new ArgumentNullException(nameof(imageEditor));
            this.imageLayoutService = imageLayoutService
                ?? throw new ArgumentNullException(
                    nameof(imageLayoutService));
            this.layoutMetrics = layoutMetrics
                ?? throw new ArgumentNullException(nameof(layoutMetrics));
            this.dispatcher = dispatcher
                ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public Task<WpfImage> InsertAsync(
            BitmapSource source,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);

            return dispatcher.InvokeAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var image = new WpfImage
                {
                    Source = source,
                    Stretch = Stretch.Uniform,
                    StretchDirection =
                        System.Windows.Controls.StretchDirection.Both,
                    SnapsToDevicePixels = true
                };

                imageEditor.FitWithin(
                    image,
                    layoutMetrics.AvailableWidth,
                    layoutMetrics.AvailableHeight);
                inlineService.InsertInlineUIElementAtCaret(
                    image,
                    container =>
                        container.BaselineAlignment =
                            System.Windows.BaselineAlignment.Center);
                imageLayoutService.SetLayout(
                    image,
                    DTO.ImageLayoutMode.Inline);
                richTextBox.CaretPosition =
                    imageLayoutService.GetTextInsertionPosition(
                        image,
                        DTO.ImageLayoutMode.Inline);

                richTextBox.Focus();
                return image;
            });
        }
    }
}
