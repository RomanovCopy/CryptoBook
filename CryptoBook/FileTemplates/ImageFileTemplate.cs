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

        public Task<byte[]> GetInitialContentAsync(CancellationToken ct)
        {
            throw new NotImplementedException();
        }

    }
}
