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

        void IFlowDocumentService.ToggleBold(TextSelection selection)
        {
            ToggleOrClearFormatting(selection, TextElement.FontWeightProperty, FontWeights.Bold);
        }

        void IFlowDocumentService.ToggleItalic(TextSelection selection)
        {
            ToggleOrClearFormatting(selection, TextElement.FontStyleProperty, FontStyles.Italic);
        }

        void IFlowDocumentService.ToggleUnderline(TextSelection selection)
        {
            ToggleOrClearFormatting(selection, Inline.TextDecorationsProperty, TextDecorations.Underline);
        }

        void IFlowDocumentService.ClearFormatting(TextSelection selection)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.ApplyFontSize(TextSelection selection, double fontSize)
        {
            ToggleOrClearFormatting(selection, TextElement.FontSizeProperty, fontSize);
        }

        void IFlowDocumentService.ApplyFontFamily(TextSelection selection, string fontFamily)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.ApplyForegroundColor(TextSelection selection, Color color)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.ApplyBackgroundColor(TextSelection selection, Color color)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.ApplyTextAlignment(TextSelection selection, TextAlignment alignment)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.ApplyBulletedList(TextSelection selection)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.ApplyNumberedList(TextSelection selection)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.RemoveListFormatting(TextSelection selection)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.IncreaseIndent(TextSelection selection)
        {
            throw new NotImplementedException();
        }

        void IFlowDocumentService.DecreaseIndent(TextSelection selection)
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


        private void ToggleOrClearFormatting(TextRange range, DependencyProperty property, object targetValue)
        {

            object current = range.GetPropertyValue(property);

            bool shouldRemove = current != DependencyProperty.UnsetValue && current.Equals(targetValue);

            if(property == Inline.TextDecorationsProperty)
            {
                // Особый случай для подчеркивания
                range.ApplyPropertyValue(property, shouldRemove ? null : targetValue);
            } else if(property == TextElement.FontWeightProperty)
            {
                range.ApplyPropertyValue(property, shouldRemove ? FontWeights.Normal : targetValue);
            } else if(property == TextElement.FontStyleProperty)
            {
                range.ApplyPropertyValue(property, shouldRemove ? FontStyles.Normal : targetValue);
            } else
            {
                // Общий случай
                range.ApplyPropertyValue(property, shouldRemove ? null : targetValue);
            }
        }


    }
}
