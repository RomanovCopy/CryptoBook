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
    internal class TextFormatService:ITextFormatService
    {

        private readonly IRichTextBoxService service;

        public double LineHeight { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public TextFormatService(IRichTextBoxService richTextBoxService)
        {
            service = richTextBoxService ?? throw new ArgumentNullException(nameof(richTextBoxService));
        }


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

        public void SetParagraphIndent(double indent)
        {
            throw new NotImplementedException();
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
