using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.FileTemplates
{
    public sealed class MarkdownFileTemplate: IFileTemplate
    {
        public string Id => "md";
        public string DisplayName => "Markdown";
        public string DefaultExtension => ".md";
        public string SuggestedBaseName => "New document";
        public Task<byte[]> GetInitialContentAsync(CancellationToken ct)
            => Task.FromResult(System.Text.Encoding.UTF8.GetBytes("# New document\n\n"));
    }
}
