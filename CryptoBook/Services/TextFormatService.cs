using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

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
        public void SetParagraphIndent(double indent=20)
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
                newParagraph = new Paragraph();
                newParagraph.TextIndent = indent;
                currentParagraph?.SiblingBlocks.InsertBefore(currentParagraph, newParagraph);
            } else
            {
                // Вставляем пустой абзац + новый
                Paragraph emptyParagraph = new Paragraph(new Run(""));
                newParagraph = new Paragraph();
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

    }
}
