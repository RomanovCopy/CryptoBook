using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.FileTemplates
{
    public sealed class DefaultFileTemplateRegistry: IFileTemplateRegistry
    {
        private readonly IFileTemplate[] _items;

        public DefaultFileTemplateRegistry()
        {
            _items = new IFileTemplate[]
            {
            new TextFileTemplate(),
            new MarkdownFileTemplate(),
            new JsonFileTemplate(),
            new RichTextFileTemplate(),
            new SourceCodeTemplate(),

            };
        }

        public IReadOnlyList<IFileTemplate> GetAll() => _items;

        public IFileTemplate? GetById(string id)
            => _items.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
    }
}
