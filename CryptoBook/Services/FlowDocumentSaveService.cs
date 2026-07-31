using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;
using CryptoBook.Security;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Threading;

namespace CryptoBook.Services
{
    public sealed class FlowDocumentSaveService: IFlowDocumentSaveService
    {
        private readonly IDispatcherService _dispatcherService;
        private readonly IDocumentFormatHandlerRegistry _formatHandlers;

        public FlowDocumentSaveService(
            IDispatcherService dispatcherService,
            IDocumentFormatHandlerRegistry formatHandlers)
        {
            _dispatcherService = dispatcherService ?? throw new ArgumentNullException(nameof(dispatcherService));
            _formatHandlers = formatHandlers ?? throw new ArgumentNullException(nameof(formatHandlers));
        }

        public async Task  SaveToFileAsync(IRichTextBoxService richTextBoxService, string filePath, IFileTemplate template, 
        CancellationToken cancellationToken = default, IProgressReporter? progress = null)
        {
            ArgumentNullException.ThrowIfNull(richTextBoxService);
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNull(template);

            string fullPath = Path.GetFullPath(filePath);
            string? directory = Path.GetDirectoryName(fullPath);

            if(!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string temporaryPath = CreateTemporaryPath(fullPath);

            try
            {
                await using(FileStream stream = new( temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 81920,
                useAsync: true))
                {
                    await SaveToStreamAsync( richTextBoxService, stream, template, cancellationToken, progress);

                    await stream.FlushAsync(cancellationToken);
                    stream.Flush(flushToDisk: true);
                }

                cancellationToken.ThrowIfCancellationRequested();

                

                AtomicFileCommit.CommitWithBackup(
                    temporaryPath,
                    fullPath);
                progress?.Report(1.0, "Файл сохранён");
            } 
            catch
            {
                TryDeleteFile(temporaryPath);
                throw;
            }
        }

        public async Task SaveToStreamAsync(IRichTextBoxService richTextBoxService, Stream destination, IFileTemplate template, 
        CancellationToken cancellationToken = default, IProgressReporter? progress = null)
        {
            ArgumentNullException.ThrowIfNull(richTextBoxService);
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentNullException.ThrowIfNull(template);
            var document = richTextBoxService.Document;


            if(!destination.CanWrite)
            {
                throw new ArgumentException(
                    "Поток не поддерживает запись.",
                    nameof(destination));
            }

            cancellationToken.ThrowIfCancellationRequested();

            byte[] buffer = await SerializeAsync( document, template, cancellationToken);
            const int chunkSize = 81920;
            for(int offset = 0; offset < buffer.Length; offset += chunkSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int count = Math.Min(chunkSize, buffer.Length - offset);
                await destination.WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
                progress?.Report(
                    buffer.Length == 0 ? 1.0 : (double)(offset + count) / buffer.Length,
                    "Запись файла");
            }
        }


        private async Task<byte[]> SerializeAsync( FlowDocument document, IFileTemplate template, CancellationToken cancellationToken)
        {
            IDocumentFormatHandler? formatHandler =
                _formatHandlers.Find(template);
            if(formatHandler is not null)
            {
                return await formatHandler.SerializeAsync(
                    document,
                    cancellationToken);
            }

            return await _dispatcherService.InvokeAsync( () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using MemoryStream memory = new();

                    TextRange range = new( document.ContentStart, document.ContentEnd);

                    range.Save( memory, GetDataFormat(template), preserveTextElements: true);

                    return memory.ToArray();

                });
        }

        private static string GetDataFormat( IFileTemplate template)
        {
            return template switch
            {
                ImageFileTemplate => DataFormats.Bitmap,
                SecureFileTemplate => System.Windows.DataFormats.XamlPackage,
                _ => throw new NotSupportedException(
                    $"Шаблон '{template.GetType().Name}' не поддерживается.")
            };
        }

        private static string CreateTemporaryPath( string targetPath)
        {
            string directory = Path.GetDirectoryName(targetPath) ?? Directory.GetCurrentDirectory();

            string fileName = Path.GetFileName(targetPath);

            return Path.Combine( directory, $".{fileName}.{Guid.NewGuid():N}.tmp");
        }

        private static void TryDeleteFile( string filePath)
        {
            try
            {
                if(File.Exists(filePath))
                    File.Delete(filePath);
            } catch
            {
                // Ошибка очистки не должна скрывать исходное исключение.
            }
        }
    }
}
