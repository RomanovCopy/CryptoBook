using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

using Media = System.Windows.Media;
using Draving = System.Drawing;
using Controls = System.Windows.Controls;


namespace CryptoBook.Interfaces
{
    public interface IFlowDocumentService
    {

        // Форматирование
        void ToggleBold(TextSelection selection);
        void ToggleItalic(TextSelection selection);
        void ToggleUnderline(TextSelection selection);
        void ClearFormatting(TextSelection selection);
        void ApplyFontSize(TextSelection selection, double fontSize);
        void ApplyFontFamily(TextSelection selection, string fontFamily);
        void ApplyForegroundColor(TextSelection selection, Color color);
        void ApplyBackgroundColor(TextSelection selection, Color color);
        void ApplyTextAlignment(TextSelection selection, TextAlignment alignment);

        // Списки и отступы
        void ApplyBulletedList(TextSelection selection);
        void ApplyNumberedList(TextSelection selection);
        void RemoveListFormatting(TextSelection selection);
        void IncreaseIndent(TextSelection selection);
        void DecreaseIndent(TextSelection selection);

        // Вставка элементов
        void InsertImageAt(TextPointer position, string imagePath, double width, double height);
        void InsertHyperlinkAt(TextPointer position, string uri, string displayText);
        void InsertParagraphAt(TextPointer position);
        void InsertLineBreakAt(TextPointer position);
        void InsertTableAt(TextPointer position, int rows, int columns);

        // Работа с текстом
        string GetPlainText();
        void LoadPlainText(string text);
        string GetRtf();
        void LoadRtf(string rtf);

        // Поиск и замена
        bool FindText(string text, bool matchCase = false, bool wholeWord = false);
        void ReplaceText(string searchText, string replaceText, bool matchCase = false, bool wholeWord = false);
        void ReplaceAllText(string searchText, string replaceText, bool matchCase = false, bool wholeWord = false);

        // Документ
        void ClearDocument();
        void SetDocumentMargin(Thickness margin);

        // Работа с XAML (опционально)
        string ExportToXaml();
        void LoadFromXaml(string xaml);
    }
}
