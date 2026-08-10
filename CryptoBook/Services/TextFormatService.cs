using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;

namespace CryptoBook.Services
{
    /// <summary>
    /// Выполняет форматирование абзацев и общие операции редактирования документа.
    /// </summary>
    public sealed class TextFormatService: ITextFormatService
    {
        private readonly IRichTextBoxService service;
        private readonly IDocumentLineSpacingPreferenceStore preferenceStore;
        private readonly IDocumentLineSpacingService lineSpacingService;
        private double lineHeight;
        private double lineSpacingRatio;

        public double LineHeight
        {
            get => lineHeight;
            set
            {
                if(double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
                    return;

                lineHeight = value;
                SetAbsoluteLineHeight(value);
            }
        }

        public bool CanUndo => service.CanUndo;
        public bool CanRedo => service.CanRedo;

        public TextFormatService(
            IRichTextBoxService richTextBoxService,
            IDocumentLineSpacingPreferenceStore preferenceStore,
            IDocumentLineSpacingService lineSpacingService)
        {
            service = richTextBoxService ?? throw new ArgumentNullException(nameof(richTextBoxService));
            this.preferenceStore = preferenceStore ??
                throw new ArgumentNullException(nameof(preferenceStore));
            this.lineSpacingService = lineSpacingService ??
                throw new ArgumentNullException(nameof(lineSpacingService));
            lineSpacingRatio = lineSpacingService.Normalize(
                preferenceStore.Load());
        }

        public void SetTextAlignment(TextAlignment? alignment)
        {
            if(alignment is not TextAlignment value)
                return;

            foreach(var paragraph in GetTargetParagraphs())
                paragraph.TextAlignment = value;
        }

        public void SetParagraphIndent(double indent)
        {
            if(double.IsNaN(indent) || double.IsInfinity(indent) || indent < 0)
                return;

            foreach(var paragraph in GetTargetParagraphs())
                paragraph.TextIndent = indent;
        }

        /// <summary>
        /// Изменяет высоту строки на один шаг. Панель передаёт -1 или +1.
        /// Положительное значение, отличное от 1, задаёт абсолютную высоту.
        /// </summary>
        public void SetLineHeight(double value)
        {
            if(double.IsNaN(value) || double.IsInfinity(value) || value == 0)
                return;

            if(value == -1 || value == 1)
            {
                AdjustLineHeight(value);
                return;
            }

            if(value > 0)
            {
                lineHeight = value;
                SetAbsoluteLineHeight(value);
            }
        }

        public void SetLineSpacing(double spacing) => SetLineHeight(spacing);

        public void ToggleBulletList()
        {
            service.RestoreSelection();
            EditingCommands.ToggleBullets.Execute(null, service.Service);
        }

        public void ToggleNumberedList()
        {
            service.RestoreSelection();
            EditingCommands.ToggleNumbering.Execute(null, service.Service);
        }

        public void InsertHyperlink(string url, string displayText)
        {
            if(service.IsReadOnly ||
               !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
               (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return;

            service.RestoreSelection();
            var selection = service.Selection;
            if(IsInsideHyperlink(selection.Start) ||
               IsInsideHyperlink(selection.End) ||
               SelectionContainsHyperlink(selection))
            {
                return;
            }

            service.BeginChange();
            try
            {
                if(!selection.IsEmpty)
                {
                    var link = new Hyperlink(selection.Start, selection.End) { NavigateUri = uri };
                    service.CaretPosition = link.ContentEnd;
                }
                else
                {
                    var text = string.IsNullOrWhiteSpace(displayText) ? url : displayText;
                    var link = new Hyperlink(
                        service.CaretPosition,
                        service.CaretPosition)
                    {
                        NavigateUri = uri
                    };
                    link.Inlines.Add(new Run(text));
                    service.CaretPosition = link.ContentEnd;
                }

                service.ClearSelection();
            }
            finally
            {
                service.EndChange();
            }
        }

        private static bool IsInsideHyperlink(TextPointer position)
        {
            return IsHyperlinkOrDescendant(position.Parent) ||
                   IsHyperlinkOrDescendant(
                       position.GetAdjacentElement(LogicalDirection.Forward) as DependencyObject) ||
                   IsHyperlinkOrDescendant(
                       position.GetAdjacentElement(LogicalDirection.Backward) as DependencyObject);
        }

        private static bool IsHyperlinkOrDescendant(DependencyObject? current)
        {
            while(current != null)
            {
                if(current is Hyperlink)
                    return true;

                current = current is FrameworkContentElement element
                    ? element.Parent
                    : null;
            }

            return false;
        }

        private static bool SelectionContainsHyperlink(TextSelection selection)
        {
            for(TextPointer? position = selection.Start;
                position != null && position.CompareTo(selection.End) < 0;
                position = position.GetNextContextPosition(LogicalDirection.Forward))
            {
                if(position.GetAdjacentElement(LogicalDirection.Forward) is Hyperlink)
                    return true;
            }

            return false;
        }

        public void ClearAllFormatting()
        {
            service.RestoreSelection();
            var selection = service.Selection;
            if(selection.IsEmpty)
                return;

            selection.ClearAllProperties();
            foreach(var paragraph in GetTargetParagraphs())
            {
                paragraph.ClearValue(Block.TextAlignmentProperty);
                paragraph.ClearValue(Paragraph.TextIndentProperty);
                paragraph.ClearValue(Paragraph.LineHeightProperty);
                paragraph.LineStackingStrategy = LineStackingStrategy.MaxHeight;
            }
        }

        public TextRange GetSelectedTextRange()
        {
            service.RestoreSelection();
            return new TextRange(service.Selection.Start, service.Selection.End);
        }

        public void ReplaceSelectedText(string newText)
        {
            service.RestoreSelection();
            service.Selection.Text = newText ?? string.Empty;
            service.CaretPosition = service.Selection.End;
            service.ClearSelection();
        }

        public void Undo()
        {
            if(CanUndo)
                service.Undo();
        }

        public void Redo()
        {
            if(CanRedo)
                service.Redo();
        }

        public void MoveCaretToStart()
        {
            var position = service.Document.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
            service.CaretPosition = position;
            service.Selection.Select(position, position);
            service.ScrollToStart();
        }

        public void MoveCaretToEnd()
        {
            var position = service.Document.ContentEnd.GetInsertionPosition(LogicalDirection.Backward);
            service.CaretPosition = position;
            service.Selection.Select(position, position);
            service.ScrollToEnd();
        }

        private void SetAbsoluteLineHeight(double value)
        {
            foreach(var paragraph in GetTargetParagraphs())
            {
                paragraph.LineStackingStrategy = LineStackingStrategy.MaxHeight;
                paragraph.LineHeight = value;
            }
        }

        private void AdjustLineHeight(double direction)
        {
            double updatedRatio = lineSpacingService.Adjust(
                lineSpacingRatio,
                Math.Sign(direction));

            foreach(var paragraph in GetTargetParagraphs())
            {
                lineSpacingService.Apply(paragraph, updatedRatio);
                lineHeight = paragraph.LineHeight;
            }

            if(updatedRatio != lineSpacingRatio)
            {
                lineSpacingRatio = updatedRatio;
                preferenceStore.Save(updatedRatio);
            }
        }

        private IReadOnlyList<Paragraph> GetTargetParagraphs()
        {
            service.RestoreSelection();

            if(service.Selection.IsEmpty)
            {
                var current = service.CaretPosition.Paragraph;
                return current == null ? Array.Empty<Paragraph>() : [current];
            }

            var start = service.Selection.Start.Paragraph;
            var end = service.Selection.End.Paragraph;
            if(start == null || end == null)
                return Array.Empty<Paragraph>();

            var result = new List<Paragraph>();
            for(var paragraph = start; paragraph != null; paragraph = GetNextParagraph(paragraph))
            {
                result.Add(paragraph);
                if(ReferenceEquals(paragraph, end))
                    break;
            }

            return result;
        }

        private static Paragraph? GetNextParagraph(Paragraph paragraph)
        {
            var position = paragraph.ElementEnd.GetNextContextPosition(LogicalDirection.Forward);
            while(position != null && position.Paragraph == null)
                position = position.GetNextContextPosition(LogicalDirection.Forward);
            return position?.Paragraph;
        }

    }
}
