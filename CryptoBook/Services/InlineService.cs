using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Linq;
using System.Reflection;
using System.Windows.Media;

namespace CryptoBook.Services
{
    public class InlineService: IInlineService
    {
        public readonly IRichTextBoxService service;

        public InlineService(IRichTextBoxService richTextBoxService)
        {
            service = richTextBoxService ?? throw new ArgumentNullException(nameof(richTextBoxService));
        }


        // ---------- СКОУП ИЗМЕНЕНИЙ ----------

        private sealed class ChangeScope: IInlineChangeScope
        {
            private readonly FlowDocument _doc;
            private readonly IRichTextBoxService service;
            private bool _disposed;

            public ChangeScope(IRichTextBoxService richTextBoxService)
            {
                service = richTextBoxService ?? throw new ArgumentNullException(nameof(richTextBoxService));
                _doc = service.Document ?? throw new ArgumentNullException(nameof(_doc));
                service.BeginChange();
            }

            public void Dispose()
            {
                if(_disposed)
                    return;
                _disposed = true;
                service.EndChange();
            }

            public void Cancel()
            {
                throw new NotImplementedException();
            }
        }

        public IInlineChangeScope BeginChangeScope()
        {
            if(service.Document is null)
                throw new InvalidOperationException("Document is null.");
            return new ChangeScope(service);
        }


        // ---------- ВСТАВКИ ----------

        public Run InsertRunAtCaret(RunInsertOptions options)
        {
            if(service.Document is null)
                throw new InvalidOperationException("Document is null.");

            var caret = EnsureInsertionPosition(service.CaretPosition);
           

            string text = ReadProp<string>(options, "Text") ?? string.Empty;
            var style = ReadProp<object>(options, "Style");
            var configure = ReadProp<Delegate>(options, "Configure"); // Action<Run>
            bool moveCaret = ReadProp<bool?>(options, "MoveCaretAfterInsert") ?? true;

            var run = new Run(text);
            if(style != null)
                ApplyStyle(run, style, overwriteNullsOnly: false);

            if(configure is Action<Run> conf)
                conf(run);

            InsertInlineAt(caret, run);

            if(moveCaret)
            {
                // Переместим каретку за только что вставленный Run
                service.CaretPosition = run.ElementEnd;
            }

            return run;
        }

        public Inline InsertInlineAt(TextPointer position, Inline inline)
        {
            throw new NotImplementedException();
        }

        public LineBreak InsertLineBreakAtCaret()
        {
            throw new NotImplementedException();
        }

        public Hyperlink InsertHyperlinkAtCaret(string text, Uri? navigateUri, InlineStyle? style = null, Action<Hyperlink>? configure = null)
        {
            throw new NotImplementedException();
        }

        public InlineUIContainer InsertInlineUIElementAtCaret(UIElement element, Action<InlineUIContainer>? configure = null)
        {
            throw new NotImplementedException();
        }

        public Run ReplaceSelection(string text, InlineStyle? style = null, Action<Run>? configure = null)
        {
            throw new NotImplementedException();
        }

        public Span WrapSelectionInSpan(InlineStyle? spanStyle = null, Action<Span>? configure = null)
        {
            throw new NotImplementedException();
        }

        public void ApplyStyle(Inline inline, InlineStyle style, bool overwriteNullsOnly = false)
        {
            throw new NotImplementedException();
        }

        public void ApplyStyleToSelection(InlineStyle style)
        {
            throw new NotImplementedException();
        }

        public void ToggleBoldOnSelection()
        {
            throw new NotImplementedException();
        }

        public void ToggleItalicOnSelection()
        {
            throw new NotImplementedException();
        }

        public void ToggleUnderlineOnSelection()
        {
            throw new NotImplementedException();
        }

        public (Run? left, Run? right) SplitRunAt(TextPointer position)
        {
            throw new NotImplementedException();
        }

        public int MergeAdjacentRuns(Paragraph paragraph)
        {
            throw new NotImplementedException();
        }

        public void NormalizeParagraphInlines(Paragraph paragraph)
        {
            throw new NotImplementedException();
        }

        public Inline? GetInlineAtCaret()
        {
            throw new NotImplementedException();
        }

        public InlineStyle GetEffectiveStyleAtCaret()
        {
            throw new NotImplementedException();
        }

        private TextPointer EnsureInsertionPosition(TextPointer caret)
        {
            if(caret == null)
                throw new ArgumentNullException(nameof(caret));

            return caret.IsAtInsertionPosition
                ? caret
                : caret.GetInsertionPosition(LogicalDirection.Forward);
        }

    }
}
