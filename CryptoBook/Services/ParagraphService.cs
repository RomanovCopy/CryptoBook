using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;

using Media = System.Windows.Media;

namespace CryptoBook.Services
{
    public class ParagraphService: Paragraph, IParagraphService
    {

        #region IParagraphService — properties

        public Paragraph Element => this;

        Media.Brush IParagraphService.Background
        {
            get => base.Background;
            set => base.Background = value;
        }

        Media.Brush IParagraphService.Foreground
        {
            get => base.Foreground;
            set => base.Foreground = value;
        }

        System.Windows.Documents.TextElement IParagraphService.Parent
            => base.Parent as System.Windows.Documents.TextElement
               ?? throw new InvalidOperationException("This paragraph has no TextElement parent.");

        public FlowDocument FlowDocumentParent
            => GetFlowDocumentAncestor()
               ?? throw new InvalidOperationException("This paragraph is not attached to a FlowDocument.");

        #endregion

        #region IParagraphService — inline ops

        public void AddInline(Inline inline)
        {
            if(inline is null)
                throw new ArgumentNullException(nameof(inline));
            Inlines.Add(inline);
        }

        public bool RemoveInline(Inline inline)
        {
            if(inline is null)
                return false;
            return Inlines.Remove(inline);
        }

        public void AddText(string text)
        {
            if(text is null)
                return;
            Inlines.Add(new Run(text));
        }

        public void AddInlines(params Inline[] inlines)
        {
            if(inlines is null || inlines.Length == 0)
                return;
            foreach(var i in inlines.Where(i => i != null))
                Inlines.Add(i);
        }

        public bool ContainsInline(Inline inline)
            => inline != null && Inlines.Contains(inline);

        public void Clear() => Inlines.Clear();

        #endregion

        #region IParagraphService — style/brushes

        public void SetStyle(Style style) => this.Style = style;

        public Style GetStyle() => this.Style;

        public void ApplyForeground(Media.Brush brush) => this.Foreground = brush;

        public void ApplyBackground(Media.Brush brush) => this.Background = brush;

        #endregion

        #region IParagraphService — clone/copy formatting

        public IParagraphService Clone()
        {
            // Глубокая копия через XAML (сохраняет все свойства и вложенные Inline)
            var xaml = XamlWriter.Save(this);
            var clonedParagraph = (Paragraph)XamlReader.Parse(xaml);

            var result = new ParagraphService();
            // Копируем форматирование «как есть»
            result.CopyFormattingFrom(this, copyOnlyLocal: false);

            // Переносим клоны инлайнов из временного параграфа
            foreach(var inline in clonedParagraph.Inlines.ToList())
                result.Inlines.Add(inline);

            return result;
        }

        public void CopyFormattingFrom(IParagraphService state, bool copyOnlyLocal)
        {
            if(state is null)
                throw new ArgumentNullException(nameof(state));
            var src = state.Element;

            // Набор DependencyProperty, которые обычно имеет смысл переносить
            var dps = new DependencyProperty[]
            {
                // TextElement-level
                ParagraphService.BackgroundProperty,
                ParagraphService.ForegroundProperty,
                ParagraphService.FontFamilyProperty,
                ParagraphService.FontStyleProperty,
                ParagraphService.FontWeightProperty,
                ParagraphService.FontStretchProperty,
                ParagraphService.FontSizeProperty,
                ParagraphService.TextEffectsProperty,
                ParagraphService.FlowDirectionProperty,
                ParagraphService.KeepTogetherProperty,
                ParagraphService.KeepWithNextProperty,

                // Block-level
                Block.TextAlignmentProperty,
                Block.LineHeightProperty,
                Block.LineStackingStrategyProperty,
                Block.MarginProperty,
                Block.BreakPageBeforeProperty,

                // Paragraph-level
                Paragraph.TextIndentProperty
            };

            foreach(var dp in dps)
            {
                var value = copyOnlyLocal
                    ? src.ReadLocalValue(dp)
                    : src.GetValue(dp);

                if(copyOnlyLocal && value == DependencyProperty.UnsetValue)
                    continue;

                // ReadLocalValue может вернуть DependencyProperty.UnsetValue
                if(!ReferenceEquals(value, DependencyProperty.UnsetValue))
                    this.SetValue(dp, value);
            }
        }

        #endregion

        #region helpers

        private FlowDocument? GetFlowDocumentAncestor()
        {
            // Поднимаемся вверх по логическому дереву
            DependencyObject current = this;
            while(current != null)
            {
                if(current is FlowDocument fd)
                    return fd;

                // Для TextElement.Parent — это DependencyObject
                if(current is System.Windows.Documents.TextElement te)
                {
                    current = te.Parent;
                } else
                {
                    current = LogicalTreeHelper.GetParent(current);
                }
            }
            return null;
        }

        #endregion
    }
}
