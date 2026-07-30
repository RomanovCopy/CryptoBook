using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.FileTemplates
{
    public class ImageFileTemplate: IFileTemplate
    {
        public string Id => "Image";
        public string DisplayName => "Изображение";
        public string DefaultExtension => ".png";

        public IReadOnlyCollection<string> Extensions => 
        [
            ".png",
            ".jpg",
            ".jpeg",
            ".bmp",
            ".gif",
            ".webp"
        ];

        public string SuggestedBaseName => "New Image";
        public FileOpenMode OpenMode => FileOpenMode.Media;

        public Task<byte[]> GetInitialContentAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            // Валидное прозрачное PNG-изображение размером 1x1.
            return Task.FromResult(Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJ" +
                "AAAADUlEQVR42mNk+M/wHwAF/gL+X1WQ0gAAAABJRU5ErkJggg=="));
        }

    }
}
