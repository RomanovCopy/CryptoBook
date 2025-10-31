using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.FileTemplates
{
    public sealed class FileTemplateRegistry: IFileTemplateRegistry
    {
        private readonly IReadOnlyDictionary<string, IFileTemplate> _templates;

        public FileTemplateRegistry(IEnumerable<IFileTemplate> templates)
        {
            if(templates is null)
                throw new ArgumentNullException(nameof(templates));

            // Гарантируем уникальность Id и делаем словарь единожды
            _templates = templates
                .GroupBy(t => t.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.First(),
                    StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyList<IFileTemplate> GetAll() => _templates.Values.ToList();

        public IFileTemplate? GetById(string id)
        {
            if(string.IsNullOrWhiteSpace(id))
                return null;

            return _templates.TryGetValue(id, out var t) ? t : null;
        }
    }
}
