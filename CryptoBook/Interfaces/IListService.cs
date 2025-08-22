using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace CryptoBook.Interfaces
{
    public interface IListService
    {
        void ToggleBulleted();
        void ToggleNumbered(int startIndex = 1);
        void ClearLists();                 // снять список с выделения
        bool CanToggle { get; }            // есть ли параграфы под выделением
        bool CanClear { get; }
    }

    public interface IDocumentSelection
    {
        /// Абсолютный RTB.Selection (нужен минимально)
        TextSelection Selection { get; }

        /// Параграфы в выделении или текущий параграф, если выделения нет
        IReadOnlyList<Paragraph> GetSelectedParagraphsOrCurrent();
    }

    public interface IEditTransaction
    {
        /// Оборачивает изменения в BeginChange/EndChange (или вашу транзакцию)
        IDisposable Begin();
    }
}
