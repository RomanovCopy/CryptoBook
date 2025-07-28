using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace CryptoBook.Interfaces
{
    /// <summary>
    /// Контекст, содержащий общий FlowDocument, используемый всеми сервисами.
    /// Предназначен для использования как singleton.
    /// </summary>
    public interface IRichTextDocumentContext
    {
        public FlowDocument Document { get; }
    }
}
