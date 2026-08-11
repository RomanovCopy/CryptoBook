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
        string DisplayName { get; }
        string DefaultExtension { get; }
        IReadOnlyCollection<string> Extensions { get; }
        string SuggestedBaseName { get; }     // "New file"
                                              // Возвращает начальный контент (можно пустой). Например, JSON "{}\n"
                                              
        bool DefaultIsHidden => false;
        bool DefaultIsReadOnly => false;
        bool CanCreate => true;
        FileOpenMode OpenMode => FileOpenMode.Document;

        /// <summary>
        /// Формат способен сохранять свойства отдельных символов и абзацев.
        /// Для обычных текстовых форматов оформление является только общим
        /// представлением документа и применяется ко всему документу.
        /// </summary>
        bool PreservesTextFormatting => true;

        Task<byte[]> GetInitialContentAsync(CancellationToken ct);
        // Опционально: кодировка подписи/комментария и т.п. Если null — оставим как есть.
        Encoding? DefaultEncoding => null;

        bool CanHandleExtension(string extension)
        {
            return Extensions.Contains( extension, StringComparer.OrdinalIgnoreCase);
        }
    }

    public enum FileOpenMode
    {
        Document,
        Media,
        External
    }
}
