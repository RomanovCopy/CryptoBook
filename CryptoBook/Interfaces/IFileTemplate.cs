using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IFileTemplate
    {
        string Id { get; }                    // "text", "md", "json", "xml" ...
        string DisplayName { get; }           // "Текстовый файл", "Markdown"
        string DefaultExtension { get; }      // ".txt", ".md"
        string SuggestedBaseName { get; }     // "New file"
                                              // Возвращает начальный контент (можно пустой). Например, JSON "{}\n".
        Task<byte[]> GetInitialContentAsync(CancellationToken ct);
        // Опционально: кодировка подписи/комментария и т.п. Если null — оставим как есть.
        Encoding? DefaultEncoding => null;
    }
}
