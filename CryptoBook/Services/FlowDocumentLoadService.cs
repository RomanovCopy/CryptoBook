using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Media.Imaging;

namespace CryptoBook.Services
{
    public class FlowDocumentLoadService: IFlowDocumentLoadService
    {
        private readonly IDispatcherService _dispatcherService;
        private readonly IBookmarkService _bookmarkService;

        /// <summary>
        /// Сервис загрузки содержимого в FlowDocument (текст, RTF, изображения и т.д.).
        /// Выполняет чтение потока и обновление документа через диспетчер для UI-потока.
        /// </summary>
        public FlowDocumentLoadService(
            IDispatcherService dispatcherService,
            IBookmarkService bookmarkService)
        {
            _dispatcherService = dispatcherService ?? throw new ArgumentNullException(nameof(dispatcherService));
            _bookmarkService = bookmarkService ?? throw new ArgumentNullException(nameof(bookmarkService));
        }


        /// <summary>
        /// Асинхронно загружает данные из <paramref name="source"/> в документ, связанный с <paramref name="richTextBoxService"/>.
        /// Выбор формата загрузки зависит от переданного <paramref name="template"/>.
        /// Операция читает весь поток в память, затем выполняет обновление FlowDocument в контексте диспетчера.
        /// </summary>
        /// <param name="richTextBoxService">Сервис, предоставляющий FlowDocument для загрузки.</param>
        /// <param name="source">Исходный поток с данными файла.</param>
        /// <param name="template">Шаблон файла, определяющий формат загрузки.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Задача, представляющая асинхронную операцию загрузки.</returns>
        public async Task LoadAsync(
            IRichTextBoxService richTextBoxService,
            Stream source,
            IFileTemplate template,
            CancellationToken cancellationToken = default,
            IProgressReporter? progress = null)
        {
            ArgumentNullException.ThrowIfNull(richTextBoxService);
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(template);

            byte[] buffer = await ReadAllBytesAsync(source, cancellationToken, progress).ConfigureAwait(false);

            if(buffer == null || buffer.Length == 0)
            {
                return;
            }

            await _dispatcherService.InvokeAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                FlowDocument document = richTextBoxService.Document;

                document.Blocks.Clear();

                if(template is ImageFileTemplate)
                {
                    LoadImage(document, buffer);
                    _bookmarkService.RebuildIndexFromDocument(richTextBoxService);
                    return;
                }

                LoadDocument(document, buffer, template);
                _bookmarkService.RebuildIndexFromDocument(richTextBoxService);
            });
            progress?.Report(1.0, "Файл загружен");
        }

        /// <summary>
        /// Загружает текстовое содержимое (RTF/XAML/PlainText) в переданный FlowDocument.
        /// </summary>
        /// <param name="document">Целевой FlowDocument.</param>
        /// <param name="buffer">Буфер с данными файла.</param>
        /// <param name="template">Шаблон файла, используемый для определения формата данных.</param>
        private static void LoadDocument( FlowDocument document, byte[] buffer, IFileTemplate template)
        {
            using var stream = new MemoryStream( buffer, writable: false);

            var range = new TextRange( document.ContentStart, document.ContentEnd);

            range.Load( stream, ToDataFormat(template));
        }


        /// <summary>
        /// Создаёт BitmapImage из байтового массива и добавляет его в документ как BlockUIContainer.
        /// Использует BitmapCacheOption.OnLoad и Freeze() для безопасного использования в UI-потоке.
        /// </summary>
        /// <param name="document">Целевой FlowDocument.</param>
        /// <param name="buffer">Байтовый массив с изображением.</param>
        private static void LoadImage( FlowDocument document, byte[] buffer)
        {
            using var stream = new MemoryStream( buffer, writable: false);

            var bitmap = new System.Windows.Media.Imaging.BitmapImage();

            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            var image = new System.Windows.Controls.Image
            {
                Source = bitmap,
                Stretch = System.Windows.Media.Stretch.Uniform
            };

            document.Blocks.Add( new BlockUIContainer(image));
        }

        /// <summary>
        /// Преобразует тип шаблона файла в строковый идентификатор формата данных, используемый TextRange.Load.
        /// Бросает NotSupportedException для неподдерживаемых шаблонов.
        /// </summary>
        /// <param name="template">Шаблон файла.</param>
        /// <returns>Строка с форматом данных (DataFormats.* или DataFormats.XamlPackage).</returns>
        private static string ToDataFormat(IFileTemplate template)
        {

            return template switch
            {
                XamlFileTemplate => DataFormats.Text,
                RichTextFileTemplate => DataFormats.Rtf,
                PlainTextTemplate => DataFormats.Text,
                ImageFileTemplate => DataFormats.Bitmap,
                SecureFileTemplate => System.Windows.DataFormats.XamlPackage,
                XamlPackageFileTemplate => System.Windows.DataFormats.XamlPackage,
                _ => throw new NotSupportedException($"The template type '{template.GetType().Name}' is not supported."),
            };

        }

        /// <summary>
        /// Асинхронно читает все байты из потока. Если источник уже является MemoryStream с доступным буфером и позиция в начале,
        /// возвращает внутренний массив без копирования для эффективности.
        /// </summary>
        /// <param name="source">Исходный поток.</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <returns>Байтовый массив, содержащий все данные из потока.</returns>
        private static async Task<byte[]> ReadAllBytesAsync(
            Stream source,
            CancellationToken cancellationToken,
            IProgressReporter? progress)
        {
            if(source is MemoryStream memoryStream && memoryStream.TryGetBuffer(out ArraySegment<byte> segment) &&
               memoryStream.Position == 0 && segment.Offset == 0 && segment.Count == segment.Array!.Length)
            {
                progress?.Report(1.0, "Чтение завершено");
                return segment.Array;
            }

            using var buffer = new MemoryStream();
            byte[] chunk = new byte[81920];
            long totalBytes = source.CanSeek ? Math.Max(0, source.Length - source.Position) : 0;
            long readBytes = 0;

            while(true)
            {
                int read = await source.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
                if(read == 0)
                    break;

                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                readBytes += read;
                progress?.Report(
                    totalBytes > 0 ? Math.Min(1.0, (double)readBytes / totalBytes) : null,
                    "Чтение файла");
            }

            return buffer.ToArray();
        }

    }
}
