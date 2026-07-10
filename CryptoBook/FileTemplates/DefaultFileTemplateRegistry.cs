using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.FileTemplates
{
    /// <summary>
    /// дефолтный реестр шаблонов файлов
    /// </summary>
    public sealed class DefaultFileTemplateRegistry: IFileTemplateRegistry
    {
        private readonly IFileTemplate[] _items;

        public DefaultFileTemplateRegistry()
        {
            //_items =
            //[
            //    new Text(),
            //    new ImageFileTemplate()
            //];
        }

        public IReadOnlyList<IFileTemplate> GetAll() => _items;

        public IFileTemplate? GetById(string id)
            => _items.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
    }
}
