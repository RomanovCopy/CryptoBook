using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace CryptoBook.Services
{
    internal class TextFormatService: ITextFormatService
    {

        private readonly IRichTextBoxService service;

        public double LineHeight { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public TextFormatService(IRichTextBoxService richTextBoxService)
        {
            service = richTextBoxService ?? throw new ArgumentNullException(nameof(richTextBoxService));
        }

        /// <summary>
        /// форматирование выделенного текста
        /// </summary>
        /// <param name="alignment">вид форматирования</param>
        public void SetTextAlignment(TextAlignment? alignment)
        {
            if(alignment == null)
                return;
            else if(alignment == TextAlignment.Left)
            {
                service.Selection.ApplyPropertyValue(Block.TextAlignmentProperty, TextAlignment.Left);
            } else if(alignment == TextAlignment.Center)
            {
                service.Selection.ApplyPropertyValue(Block.TextAlignmentProperty, TextAlignment.Center);
            } else if(alignment == TextAlignment.Right)
            {
                service.Selection.ApplyPropertyValue(Block.TextAlignmentProperty, TextAlignment.Right);
            } else
            {
                service.Selection.ApplyPropertyValue(Block.TextAlignmentProperty, TextAlignment.Justify);
            }
        }

        /// <summary>
        /// создание нового параграфа
        /// </summary>
        /// <param name="indent">отступ от начала строки</param>
        public void SetParagraphIndent(double indent = 20)
        {
            if(indent < 0)
                return;

            var caretPos = service.CaretPosition.GetInsertionPosition(LogicalDirection.Forward);
            var currentParagraph = caretPos.Paragraph;
            if(currentParagraph == null)
                return;

            bool isAtStartOfParagraph =
                currentParagraph != null &&
                caretPos.CompareTo(currentParagraph.ContentStart) == 0;

            Paragraph newParagraph;

            if(isAtStartOfParagraph)
            {
                // Вставляем новый параграф перед текущим
                newParagraph = CreateParagraphWithCaretFormatting(caretPos);
                newParagraph.TextIndent = indent;
                currentParagraph?.SiblingBlocks.InsertBefore(currentParagraph, newParagraph);
            } else
            {
                // Вставляем пустой абзац + новый
                Paragraph emptyParagraph = new Paragraph(new Run(""));
                newParagraph = CreateParagraphWithCaretFormatting(caretPos);
                newParagraph.TextIndent = indent;

                currentParagraph?.SiblingBlocks.InsertAfter(currentParagraph, emptyParagraph);
                emptyParagraph.SiblingBlocks.InsertAfter(emptyParagraph, newParagraph);
            }

            // Переносим курсор в новый параграф
            service.CaretPosition = newParagraph.ContentStart;
            service.Focus();
        }



        public void SetLineHeight(double lineHeight)
        {
            throw new NotImplementedException();
        }

        public void SetLineSpacing(double spacing)
        {
            throw new NotImplementedException();
        }

        public void ToggleBulletList()
        {
            throw new NotImplementedException();
        }

        public void ToggleNumberedList()
        {
            throw new NotImplementedException();
        }

        public void InsertHyperlink(string url, string displayText)
        {
            throw new NotImplementedException();
        }

        public void ClearAllFormatting()
        {
            throw new NotImplementedException();
        }

        public TextRange GetSelectedTextRange()
        {
            throw new NotImplementedException();
        }

        public void ReplaceSelectedText(string newText)
        {
            throw new NotImplementedException();
        }

        public void Undo()
        {
            throw new NotImplementedException();
        }

        public void Redo()
        {
            throw new NotImplementedException();
        }

        public bool CanUndo => throw new NotImplementedException();

        public bool CanRedo => throw new NotImplementedException();

        public void MoveCaretToStart()
        {
            throw new NotImplementedException();
        }

        public void MoveCaretToEnd()
        {
            throw new NotImplementedException();
        }



        /// <summary>
        /// создание нового параграфа с копированием свойств форматирования из текущего параграфа
        /// </summary>
        /// <param name="currentParagraph">текущий параграф</param>
        /// <returns>новый параграф</returns>
        /// <exception cref="ArgumentNullException"></exception>
        private Paragraph CreateParagraphWithCopiedProperties(Paragraph currentParagraph)
        {
            if(currentParagraph == null)
                throw new ArgumentNullException(nameof(currentParagraph));

            Paragraph newParagraph = new Paragraph();

            // Копируем свойства форматирования из текущего параграфа
            newParagraph.TextAlignment = currentParagraph.TextAlignment;
            newParagraph.FlowDirection = currentParagraph.FlowDirection;
            newParagraph.LineHeight = currentParagraph.LineHeight;
            newParagraph.Margin = currentParagraph.Margin;
            newParagraph.Padding = currentParagraph.Padding;
            newParagraph.Background = currentParagraph.Background;
            newParagraph.Foreground = currentParagraph.Foreground;
            newParagraph.FontFamily = currentParagraph.FontFamily;
            newParagraph.FontSize = currentParagraph.FontSize;
            newParagraph.FontStretch = currentParagraph.FontStretch;
            newParagraph.FontStyle = currentParagraph.FontStyle;
            newParagraph.FontWeight = currentParagraph.FontWeight;

            return newParagraph;
        }

        public Paragraph CreateParagraphWithCaretFormatting(System.Windows.Documents.TextPointer caretPosition)
        {
            if(caretPosition == null)
                throw new ArgumentNullException(nameof(caretPosition));

            Paragraph newParagraph = new Paragraph();

            // Создаем TextRange из позиции каретки (пустой диапазон)
            TextRange range = new TextRange(caretPosition, caretPosition);

            var foreground = range.GetPropertyValue(System.Windows.Documents.TextElement.ForegroundProperty);
            if(foreground != DependencyProperty.UnsetValue)
                newParagraph.Foreground = (System.Windows.Media.Brush)foreground;

            var background = range.GetPropertyValue(System.Windows.Documents.TextElement.BackgroundProperty);
            if(background != DependencyProperty.UnsetValue)
                newParagraph.Background = (System.Windows.Media.Brush)background;

            var fontFamily = range.GetPropertyValue(System.Windows.Documents.TextElement.FontFamilyProperty);
            if(fontFamily != DependencyProperty.UnsetValue)
                newParagraph.FontFamily = (System.Windows.Media.FontFamily)fontFamily;

            var fontSize = range.GetPropertyValue(System.Windows.Documents.TextElement.FontSizeProperty);
            if(fontSize != DependencyProperty.UnsetValue)
                newParagraph.FontSize = (double)fontSize;

            var fontStretch = range.GetPropertyValue(System.Windows.Documents.TextElement.FontStretchProperty);
            if(fontStretch != DependencyProperty.UnsetValue)
                newParagraph.FontStretch = (FontStretch)fontStretch;

            var fontStyle = range.GetPropertyValue(System.Windows.Documents.TextElement.FontStyleProperty);
            if(fontStyle != DependencyProperty.UnsetValue)
                newParagraph.FontStyle = (System.Windows.FontStyle)fontStyle;

            var fontWeight = range.GetPropertyValue(System.Windows.Documents.TextElement.FontWeightProperty);
            if(fontWeight != DependencyProperty.UnsetValue)
                newParagraph.FontWeight = (FontWeight)fontWeight;

            Paragraph currentParagraph = caretPosition.Paragraph;
            if(currentParagraph != null)
            {
                newParagraph.TextAlignment = currentParagraph.TextAlignment;
                newParagraph.FlowDirection = currentParagraph.FlowDirection;
            }

            return newParagraph;
        }
    }
}
