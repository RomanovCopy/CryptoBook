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
    public class FlowDocumentService:FlowDocument, IFlowDocumentService
    {
        FlowDocument IFlowDocumentService.Document
        {
            get => this;
            set
            {
                this.Blocks.Clear();
                if (value != null)
                {
                    foreach (var block in value.Blocks)
                    {
                        this.Blocks.Add(block);
                    }
                }
            }
        }

        void IFlowDocumentService.ToggleBold(TextSelection range)
        {
            ToggleOrClearFormatting(range, TextElement.FontWeightProperty, FontWeights.Bold);
        }

        void IFlowDocumentService.ToggleItalic(TextRange range)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.ToggleUnderline(TextRange range)
        {
            //ToggleOrClearFormatting(range, Inline.TextDecorationsProperty, TextDecorations.Underline);
        }

        void IFlowDocumentService.ClearFormatting(TextRange range)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.ApplyFontSize(TextRange range, double fontSize)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.ApplyFontFamily(TextRange range, string fontFamily)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.ApplyForegroundColor(TextRange range, Color color)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.ApplyBackgroundColor(TextRange range, Color color)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.ApplyTextAlignment(TextRange range, TextAlignment alignment)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.ApplyBulletedList(TextRange range)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.ApplyNumberedList(TextRange range)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.RemoveListFormatting(TextRange range)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.IncreaseIndent(TextRange range)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.DecreaseIndent(TextRange range)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.InsertImageAt(TextPointer position, string imagePath, double width, double height)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.InsertHyperlinkAt(TextPointer position, string uri, string displayText)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.InsertParagraphAt(TextPointer position)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.InsertLineBreakAt(TextPointer position)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.InsertTableAt(TextPointer position, int rows, int columns)
        {
            throw new NotImplementedException();
        }

        string IFlowDocumentService.GetPlainText()
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.LoadPlainText(string text)
        {
            throw new NotImplementedException();
        }

        string IFlowDocumentService.GetRtf()
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.LoadRtf(string rtf)
        {
            throw new NotImplementedException();
        }

        bool IFlowDocumentService.FindText(string text, bool matchCase, bool wholeWord)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.ReplaceText(string searchText, string replaceText, bool matchCase, bool wholeWord)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.ReplaceAllText(string searchText, string replaceText, bool matchCase, bool wholeWord)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.ClearDocument()
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.SetDocumentMargin(Thickness margin)
        {
            throw new NotImplementedException();
        }

        string IFlowDocumentService.ExportToXaml()
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.LoadFromXaml(string xaml)
        {
            throw new NotImplementedException();
        }


        private void ToggleOrClearFormatting(TextSelection selection, DependencyProperty property, object targetValue)
        {

            object current = selection.GetPropertyValue(property);

            bool shouldRemove = current != DependencyProperty.UnsetValue && current.Equals(targetValue);

            if(property == Inline.TextDecorationsProperty)
            {
                // Особый случай для подчеркивания
                selection.ApplyPropertyValue(property, shouldRemove ? null : targetValue);
            } else if(property == TextElement.FontWeightProperty)
            {
                selection.ApplyPropertyValue(property, shouldRemove ? FontWeights.Normal : targetValue);
            } else if(property == TextElement.FontStyleProperty)
            {
                selection.ApplyPropertyValue(property, shouldRemove ? FontStyles.Normal : targetValue);
            } else
            {
                // Общий случай
                selection.ApplyPropertyValue(property, shouldRemove ? null : targetValue);
            }
        }


    }
}
