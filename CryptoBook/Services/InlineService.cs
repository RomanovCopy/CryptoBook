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
using CryptoBook.Accessors;
using System.Globalization;
using System.ComponentModel;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using System.Collections.Concurrent;
using System.IO;

namespace CryptoBook.Services
{
    public class InlineService: IInlineService
    {
        private readonly IRichTextBoxService service;
        private readonly IPropertyAccessor accessor;
        private readonly IParagraphFactory paragraphFactory;

        public InlineService(IRichTextBoxService richTextBoxService, IPropertyAccessor accessor,
            IParagraphFactory paragraphFactory)
        {
            service = richTextBoxService ?? throw new ArgumentNullException(nameof(richTextBoxService));
            this.accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
            this.paragraphFactory = paragraphFactory ?? throw new ArgumentNullException(nameof(paragraphFactory));
        }


        // ---------- СКОУП ИЗМЕНЕНИЙ ----------

        private sealed class ChangeScope: IInlineChangeScope
        {
            private readonly IRichTextBoxService service;
            private readonly FlowDocument _doc;

            private readonly string _snapshotXaml;
            private readonly int _caretOffset;
            private readonly int _selStartOffset;
            private readonly int _selEndOffset;

            private bool _disposed;
            private bool _canceled;

            public ChangeScope(IRichTextBoxService richTextBoxService)
            {
                service = richTextBoxService ?? throw new ArgumentNullException(nameof(richTextBoxService));
                _doc = service.Document ?? throw new ArgumentNullException(nameof(_doc));

                // ---- снимок документа
                _snapshotXaml = SaveDocumentToXaml(_doc);

                // ---- снимок позиций
                var caret = EnsureInsertionPosition(service.CaretPosition);
                _caretOffset = GetOffset(caret);

                var sel = service.Selection;
                if(sel != null && !sel.IsEmpty)
                {
                    _selStartOffset = GetOffset(sel.Start);
                    _selEndOffset = GetOffset(sel.End);
                } else
                {
                    _selStartOffset = _caretOffset;
                    _selEndOffset = _caretOffset;
                }

                service.BeginChange();
            }

            public void Dispose()
            {
                if(_disposed)
                    return;
                _disposed = true;

                if(!_canceled)
                    service.EndChange();
            }

            public void Cancel()
            {
                if(_disposed)
                    return;
                _canceled = true;

                // 1) восстановим документ из XAML
                var restored = LoadDocumentFromXaml(_snapshotXaml);

                _doc.Blocks.Clear();
                foreach(var block in restored.Blocks.ToList())
                {
                    restored.Blocks.Remove(block);
                    _doc.Blocks.Add(block);
                }

                // 2) восстановим каретку/выделение по сохранённым смещениям
                RestoreCaretAndSelection(_caretOffset, _selStartOffset, _selEndOffset);

                // завершить BeginChange
                service.EndChange();
                _disposed = true;
            }

            // ---------- helpers ----------

            private static TextPointer EnsureInsertionPosition(TextPointer pos) =>
                pos.IsAtInsertionPosition ? pos : pos.GetInsertionPosition(LogicalDirection.Forward);

            private int GetOffset(TextPointer position)
            {
                var start = _doc.ContentStart;
                // Смещение вправо от начала документа (всегда >= 0)
                int back = position.GetOffsetToPosition(start);       // отрицательно/0
                return Math.Abs(back);
            }

            private void RestoreCaretAndSelection(int caretOffset, int selStartOffset, int selEndOffset)
            {
                // Пересобираем позиции из смещений.
                var start = _doc.ContentStart;
                var end = _doc.ContentEnd;

                int max = Math.Max(0, Math.Abs(end.GetOffsetToPosition(start))); // общая длина

                int co = Clamp(caretOffset, 0, max);
                int sst = Clamp(selStartOffset, 0, max);
                int sen = Clamp(selEndOffset, 0, max);

                // Получаем позиции (вперёд от ContentStart)
                var caretPos = start.GetPositionAtOffset(co, LogicalDirection.Forward) ?? end;
                var sPos = start.GetPositionAtOffset(sst, LogicalDirection.Forward) ?? caretPos;
                var ePos = start.GetPositionAtOffset(sen, LogicalDirection.Forward) ?? caretPos;

                // нормализуем порядок
                if(sPos.CompareTo(ePos) > 0)
                    (sPos, ePos) = (ePos, sPos);

                // Применяем
                var sel = service.Selection;
                if(sel != null)
                {
                    sel.Select(sPos, ePos);
                }

                service.CaretPosition = EnsureInsertionPosition(caretPos);
            }

            private static int Clamp(int v, int min, int max) =>
                v < min ? min : (v > max ? max : v);

            private static string SaveDocumentToXaml(FlowDocument doc)
            {
                using var ms = new MemoryStream();
                System.Windows.Markup.XamlWriter.Save(doc, ms);
                ms.Position = 0;
                using var reader = new StreamReader(ms, Encoding.UTF8);
                return reader.ReadToEnd();
            }

            private static FlowDocument LoadDocumentFromXaml(string xaml)
            {
                using var ms = new MemoryStream(Encoding.UTF8.GetBytes(xaml));
                return (FlowDocument)System.Windows.Markup.XamlReader.Load(ms);
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


            string text = accessor.Read<string>(options, "Text");
            var style = accessor.Read<InlineStyle>(options, "Style");
            var configure = accessor.Read<Delegate>(options, "Configure");
            bool moveCaret = accessor.Read<bool?>(options, "MoveCaretAfterInsert") ?? true;

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
            if(position is null)
                throw new ArgumentNullException(nameof(position));
            if(inline is null)
                throw new ArgumentNullException(nameof(inline));

            position = EnsureInsertionPosition(position);

            // Найдём параграф. Если его нет — создадим.
            var paragraph = position.Paragraph;
            if(paragraph == null)
            {
                paragraph = (Paragraph)paragraphFactory.Create();
                if(service.Document.Blocks.Count == 0)
                    service.Document.Blocks.Add(paragraph);
                else
                    service.Document.Blocks.InsertBefore(service.Document.Blocks.FirstBlock, paragraph);

                position = paragraph.ContentStart;
            }

            // Если стоим внутри Run — расщепим его и вставим inline между половинками
            if(position.Parent is Run)
            {
                var (left, _) = SplitRunAt(position);
                if(left != null)
                {
                    paragraph.Inlines.InsertAfter(left, inline);
                    return inline;
                }
            }

            // Если стоим в Span/Paragraph — найдём ближайший Inline-ориентир
            var anchor = GetInlineFromPosition(position);
            if(anchor != null)
            {
                // Вставляем перед/после якоря — решаем по направлению
                if(position.CompareTo(anchor.ContentStart) == 0)
                    paragraph.Inlines.InsertBefore(anchor, inline);
                else
                    paragraph.Inlines.InsertAfter(anchor, inline);
            } else
            {
                // Пустой параграф
                paragraph.Inlines.Add(inline);
            }

            return inline;
        }

        public LineBreak InsertLineBreakAtCaret()
        {
            var lb = new LineBreak();
            InsertInlineAt(EnsureInsertionPosition(service.CaretPosition), lb);
            service.CaretPosition = lb.ElementEnd;
            return lb;
        }

        public Hyperlink InsertHyperlinkAtCaret(string text, Uri? navigateUri, InlineStyle? style = null, Action<Hyperlink>? configure = null)
        {
            var link = new Hyperlink(new Run(text ?? string.Empty));
            if(navigateUri != null)
                link.NavigateUri = navigateUri;
            if(style != null)
                ApplyStyle(link, style, overwriteNullsOnly: false);
            configure?.Invoke(link);

            InsertInlineAt(EnsureInsertionPosition(service.CaretPosition), link);
            service.CaretPosition = link.ElementEnd;
            return link;
        }

        public InlineUIContainer InsertInlineUIElementAtCaret(UIElement element, Action<InlineUIContainer>? configure = null)
        {
            if(element is null)
                throw new ArgumentNullException(nameof(element));
            var container = new InlineUIContainer(element);
            configure?.Invoke(container);

            InsertInlineAt(EnsureInsertionPosition(service.CaretPosition), container);
            service.CaretPosition = container.ElementEnd;
            return container;
        }

        public Run ReplaceSelection(string text, InlineStyle? style = null, Action<Run>? configure = null)
        {
            var sel = service.Selection;
            using(BeginChangeScope())
            {
                if(!sel.IsEmpty)
                    sel.Text = string.Empty;

                var run = new Run(text ?? string.Empty);
                if(style != null)
                    ApplyStyle(run, style, overwriteNullsOnly: false);
                configure?.Invoke(run);

                InsertInlineAt(EnsureInsertionPosition(sel.Start), run);
                service.CaretPosition = run.ElementEnd;
                return run;
            }
        }

        public Span WrapSelectionInSpan(InlineStyle? spanStyle = null, Action<Span>? configure = null)
        {
            var sel = service.Selection;
            if(sel.IsEmpty)
            {
                // Пустое выделение — создаём пустой Span и вставляем.
                var emptySpan = new Span();
                if(spanStyle != null)
                    ApplyStyle(emptySpan, spanStyle, overwriteNullsOnly: false);
                configure?.Invoke(emptySpan);
                InsertInlineAt(EnsureInsertionPosition(service.CaretPosition), emptySpan);
                service.CaretPosition = emptySpan.ContentEnd;
                return emptySpan;
            }
            using(BeginChangeScope())
            {
                var span = new Span(sel.Start, sel.End); // WPF: оборачивает существующие инлайны в Span
                if(spanStyle != null)
                    ApplyStyle(span, spanStyle, overwriteNullsOnly: false);
                configure?.Invoke(span);

                // Переместим каретку в конец новой обертки
                service.CaretPosition = span.ContentEnd;
                return span;
            }
        }

        public void ApplyStyle(Inline inline, InlineStyle style, bool overwriteNullsOnly = false)
        {
            if(inline is null || style is null)
                return;
            // Пробуем массово перенести известные свойства по имени (если они есть в style)
            CopyStyleProp(style, "FontFamily", val => inline.FontFamily = (System.Windows.Media.FontFamily)val, overwriteNullsOnly, inline.FontFamily);
            CopyStyleProp(style, "FontSize", val => inline.FontSize = Convert.ToDouble(val), overwriteNullsOnly, inline.FontSize);
            CopyStyleProp(style, "FontWeight", val => inline.FontWeight = (FontWeight)val, overwriteNullsOnly, inline.FontWeight);
            CopyStyleProp(style, "FontStyle", val => inline.FontStyle = (System.Windows.FontStyle)val, overwriteNullsOnly, inline.FontStyle);
            CopyStyleProp(style, "Foreground", val => inline.Foreground = (System.Windows.Media.Brush)val, overwriteNullsOnly, inline.Foreground);
            CopyStyleProp(style, "Background", val => inline.Background = (System.Windows.Media.Brush)val, overwriteNullsOnly, inline.Background);
            CopyStyleProp(style, "TextDecorations", val => inline.TextDecorations = (TextDecorationCollection)val, overwriteNullsOnly, inline.TextDecorations);
            CopyStyleProp(style, "FontStretch", val => inline.FontStretch = (FontStretch)val, overwriteNullsOnly, inline.FontStretch);
            CopyStyleProp(style, "BaselineAlignment", val => inline.BaselineAlignment = (BaselineAlignment)val, overwriteNullsOnly, inline.BaselineAlignment);
            CopyStyleProp(style, "TextEffects", val => inline.TextEffects = (TextEffectCollection)val, overwriteNullsOnly, inline.TextEffects);
        }

        public void ApplyStyleToSelection(InlineStyle style)
        {
            if(style is null)
                return;
            var sel = service.Selection;
            if(sel is null)
                return;

            using(BeginChangeScope())
            {
                ApplySelectionProp(sel, Inline.FontFamilyProperty, style, "FontFamily");
                ApplySelectionProp(sel, Inline.FontSizeProperty, style, "FontSize");
                ApplySelectionProp(sel, Inline.FontWeightProperty, style, "FontWeight");
                ApplySelectionProp(sel, Inline.FontStyleProperty, style, "FontStyle");
                ApplySelectionProp(sel, TextElement.ForegroundProperty, style, "Foreground");
                ApplySelectionProp(sel, TextElement.BackgroundProperty, style, "Background");
                ApplySelectionProp(sel, Inline.TextDecorationsProperty, style, "TextDecorations");
                ApplySelectionProp(sel, TextElement.FontStretchProperty, style, "FontStretch");
                ApplySelectionProp(sel, Inline.BaselineAlignmentProperty, style, "BaselineAlignment");
                ApplySelectionProp(sel, TextElement.TextEffectsProperty, style, "TextEffects");
            }
        }

        public void ToggleBoldOnSelection()
        {
            var sel = service.Selection;
            var current = sel.GetPropertyValue(Inline.FontWeightProperty);
            var isBold = current is FontWeight fw && fw == FontWeights.Bold;
            sel.ApplyPropertyValue(Inline.FontWeightProperty, isBold ? FontWeights.Normal : FontWeights.Bold);
        }

        public void ToggleItalicOnSelection()
        {
            var sel = service.Selection;
            var current = sel.GetPropertyValue(Inline.FontStyleProperty);
            var isItalic = current is System.Windows.FontStyle fs && fs == FontStyles.Italic;
            sel.ApplyPropertyValue(Inline.FontStyleProperty, isItalic ? FontStyles.Normal : FontStyles.Italic);
        }

        public void ToggleUnderlineOnSelection()
        {
            var sel = service.Selection;
            var current = sel.GetPropertyValue(Inline.TextDecorationsProperty);
            bool hasUnderline = current is TextDecorationCollection tdc &&
                tdc != null && tdc.Contains(TextDecorations.Underline[0]);
            sel.ApplyPropertyValue(Inline.TextDecorationsProperty, hasUnderline ? null : TextDecorations.Underline);
        }

        public (Run? left, Run? right) SplitRunAt(TextPointer position)
        {
            position = EnsureInsertionPosition(position);
            if(position.Parent is not Run run)
                return (null, null);

            int offset = position.GetOffsetToPosition(run.ContentStart);
            // offset < 0 означает, что position правее ContentStart — переведём в положительное
            offset = Math.Abs(offset);

            string text = run.Text ?? string.Empty;
            if(offset <= 0 || offset >= text.Length)
                return (run, null); // на границе — нечего резать

            string leftText = text.Substring(0, offset);
            string rightText = text.Substring(offset);

            var paragraph = (Paragraph)run.Parent;

            var left = new Run(leftText)
            {
                FontFamily = run.FontFamily,
                FontSize = run.FontSize,
                FontWeight = run.FontWeight,
                FontStyle = run.FontStyle,
                FontStretch = run.FontStretch,
                Foreground = run.Foreground,
                Background = run.Background,
                TextDecorations = run.TextDecorations,
                BaselineAlignment = run.BaselineAlignment,
                TextEffects = run.TextEffects
            };

            var right = new Run(rightText)
            {
                FontFamily = run.FontFamily,
                FontSize = run.FontSize,
                FontWeight = run.FontWeight,
                FontStyle = run.FontStyle,
                FontStretch = run.FontStretch,
                Foreground = run.Foreground,
                Background = run.Background,
                TextDecorations = run.TextDecorations,
                BaselineAlignment = run.BaselineAlignment,
                TextEffects = run.TextEffects
            };

            paragraph.Inlines.InsertBefore(run, left);
            paragraph.Inlines.InsertAfter(left, right);
            paragraph.Inlines.Remove(run);

            return (left, right);
        }

        public int MergeAdjacentRuns(Paragraph paragraph)
        {
            if(paragraph is null)
                throw new ArgumentNullException(nameof(paragraph));
            int merges = 0;

            Inline? current = paragraph.Inlines.FirstInline;
            while(current != null)
            {
                var a = current as Run;
                var b = current.NextInline as Run;

                if(a != null && b != null && CanMerge(a, b))
                {
                    a.Text = (a.Text ?? string.Empty) + (b.Text ?? string.Empty);
                    paragraph.Inlines.Remove(b);
                    merges++;
                    // не двигаем current, чтобы проверить следующий после объединённого
                } else
                {
                    current = current.NextInline;
                }
            }

            return merges;
        }

        public void NormalizeParagraphInlines(Paragraph paragraph)
        {
            if(paragraph is null)
                return;

            using(BeginChangeScope())
            {
                // Удалим пустые Runs
                var toRemove = paragraph.Inlines
                    .OfType<Run>()
                    .Where(r => string.IsNullOrEmpty(r.Text))
                    .ToList();
                foreach(var r in toRemove)
                    paragraph.Inlines.Remove(r);

                // Сольём соседние совместимые Runs
                MergeAdjacentRuns(paragraph);

                // Развернём пустые Span'ы
                var emptySpans = paragraph.Inlines.OfType<Span>()
                    .Where(s => s.Inlines.Count == 0)
                    .ToList();
                foreach(var s in emptySpans)
                    paragraph.Inlines.Remove(s);
            }
        }

        public Inline? GetInlineAtCaret()
        {
            return GetInlineFromPosition(EnsureInsertionPosition(service.CaretPosition));
        }

        public InlineStyle GetEffectiveStyleAtCaret()
        {
            var caret = EnsureInsertionPosition(service.CaretPosition)
                ?? throw new InvalidOperationException("Caret position is null.");

            var tr = new TextRange(caret, caret);

            object? Get(DependencyProperty dp)
            {
                var v = tr.GetPropertyValue(dp);
                return ReferenceEquals(v, DependencyProperty.UnsetValue) ? null : v;
            }

            var style = new InlineStyle();

            TrySet(style, "FontFamily", Get(Inline.FontFamilyProperty));
            TrySet(style, "FontSize", Get(Inline.FontSizeProperty));
            TrySet(style, "FontWeight", Get(Inline.FontWeightProperty));
            TrySet(style, "FontStyle", Get(Inline.FontStyleProperty));
            TrySet(style, "Foreground", Get(TextElement.ForegroundProperty));
            TrySet(style, "Background", Get(TextElement.BackgroundProperty));
            TrySet(style, "TextDecorations", Get(Inline.TextDecorationsProperty));
            TrySet(style, "FontStretch", Get(TextElement.FontStretchProperty));
            TrySet(style, "BaselineAlignment", Get(Inline.BaselineAlignmentProperty));
            //TrySet(style, "TextEffects", Get(TextElement.TextEffectsProperty));

            return style;
        }





        private TextPointer EnsureInsertionPosition(TextPointer caret)
        {
            if(caret == null)
                throw new ArgumentNullException(nameof(caret));

            return caret.IsAtInsertionPosition
                ? caret
                : caret.GetInsertionPosition(LogicalDirection.Forward);
        }
        private Inline? GetInlineFromPosition(TextPointer position, bool preferForward = true)
        {
            if(position is null)
                throw new ArgumentNullException(nameof(position));

            // Каретка должна стоять в допустимой точке вставки
            var pos = position.IsAtInsertionPosition
                ? position
                : position.GetInsertionPosition(LogicalDirection.Forward);

            // 1) Находимся прямо внутри Inline?
            if(pos.Parent is Inline direct)
                return direct;

            // 2) Соседние элементы по обе стороны
            var forward = pos.GetAdjacentElement(LogicalDirection.Forward) as Inline;
            var backward = pos.GetAdjacentElement(LogicalDirection.Backward) as Inline;

            if(forward != null && backward != null)
            {
                // На границе двух инлайнов: уточним по фактическим границам
                if(pos.CompareTo(forward.ContentStart) == 0)
                    return forward;
                if(pos.CompareTo(backward.ContentEnd) == 0)
                    return backward;

                // Если однозначно определить нельзя — отдадим предпочтение направлению
                return preferForward ? forward : backward;
            }
            if(forward != null)
                return forward;
            if(backward != null)
                return backward;

            // 3) Возможно стоим прямо в параграфе на его начале/конце
            var paragraph = pos.Paragraph;
            if(paragraph != null)
            {
                if(pos.CompareTo(paragraph.ContentStart) == 0)
                    return paragraph.Inlines.FirstInline;
                if(pos.CompareTo(paragraph.ContentEnd) == 0)
                    return paragraph.Inlines.LastInline;
            }

            // 4) Последняя попытка — подняться по логическому дереву
            // (бывает полезно, если позиция вложена глубже в контейнер)
            DependencyObject? current = pos.Parent;
            while(current != null && current is not Inline)
                current = LogicalTreeHelper.GetParent(current);

            return current as Inline;
        }
        public void CopyStyleProp(object style, string propName, Action<object> applyValue, bool overwriteNullsOnly,
            object? currentValue)
        {
            if(style == null)
                return;

            // Находим свойство по имени (без учета регистра)
            var prop = style.GetType().GetProperty(
                propName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

            if(prop == null)
                return; // В стиле такого свойства нет

            var value = prop.GetValue(style);
            if(value == null)
                return; // В стиле значение не задано

            // Если разрешено перезаписывать только null'ы, а текущее значение уже есть — выходим
            if(overwriteNullsOnly && currentValue != null)
                return;

            try
            {
                // Применяем
                applyValue(value);
            } catch(Exception ex)
            {
                // Игнорируем несовпадения типов или ошибки приведения
                System.Diagnostics.Debug.WriteLine(
                    $"CopyStyleProp: невозможно применить {propName} — {ex.Message}");
            }
        }
        private bool ApplySelectionProp(TextSelection sel, DependencyProperty dp, object style, string propName)
        {
            if(sel is null || dp is null || style is null)
                return false;

            // 1) достаём значение из style
            var srcProp = style.GetType().GetProperty(propName,
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.IgnoreCase);
            if(srcProp == null)
                return false;

            var rawVal = srcProp.GetValue(style);
            if(rawVal == null || Equals(rawVal, DependencyProperty.UnsetValue))
                return false;

            // 2) приводим к типу свойства DP
            var targetType = dp.PropertyType;
            if(!TryConvertValue(rawVal, targetType, out var converted))
                return false;

            // 3) сравним с текущим значением, чтобы не делать лишнюю запись
            var current = sel.GetPropertyValue(dp);
            if(!ReferenceEquals(current, DependencyProperty.UnsetValue) &&
                ValuesEqualForText(current, converted))
            {
                return false; // уже задано такое же значение
            }

            // 4) применяем
            try
            {
                sel.ApplyPropertyValue(dp, converted);
                return true;
            } catch(Exception)
            {
                // В редких случаях DP может отвергнуть значение (несовместимость контекста и т.п.)
                return false;
            }
        }
        private bool TryConvertValue(object value, Type targetType, out object? converted)
        {
            converted = null;
            if(value == null)
                return false;

            // Already compatible
            if(targetType.IsInstanceOfType(value))
            {
                converted = value;
                return true;
            }

            // Nullable<T> => T
            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

            try
            {
                // Спец. кейсы

                // Color -> Brush
                if(underlying == typeof(System.Windows.Media.Brush) && value is System.Windows.Media.Color c)
                {
                    converted = new SolidColorBrush(c);
                    // Чтобы избежать проблем заморозки при шаринге, клонировать необязательно — WPF сам справится.
                    return true;
                }

                // string -> FontFamily / Brush / FontStyle / FontWeight / FontStretch / TextDecorations / BaselineAlignment
                if(value is string s)
                {
                    if(underlying == typeof(System.Windows.Media.FontFamily))
                    {
                        converted = new System.Windows.Media.FontFamily(s);
                        return true;
                    }
                    if(underlying == typeof(System.Windows.Media.Brush))
                    {
                        var bc = new BrushConverter();
                        if(bc.CanConvertFrom(typeof(string)))
                        {
                            converted = bc.ConvertFrom(null, CultureInfo.InvariantCulture, s);
                            return converted != null;
                        }
                    }
                    if(underlying == typeof(System.Windows.FontStyle))
                    {
                        var conv = new FontStyleConverter();
                        if(conv.CanConvertFrom(typeof(string)))
                        {
                            converted = conv.ConvertFrom(null, CultureInfo.InvariantCulture, s);
                            return converted != null;
                        }
                    }
                    if(underlying == typeof(FontWeight))
                    {
                        var conv = new FontWeightConverter();
                        if(conv.CanConvertFrom(typeof(string)))
                        {
                            converted = conv.ConvertFrom(null, CultureInfo.InvariantCulture, s);
                            return converted != null;
                        }
                    }
                    if(underlying == typeof(FontStretch))
                    {
                        var conv = new FontStretchConverter();
                        if(conv.CanConvertFrom(typeof(string)))
                        {
                            converted = conv.ConvertFrom(null, CultureInfo.InvariantCulture, s);
                            return converted != null;
                        }
                    }
                    if(underlying == typeof(TextDecorationCollection))
                    {
                        // Поддержка коротких алиасов
                        if(string.Equals(s, "Underline", StringComparison.OrdinalIgnoreCase))
                        {
                            converted = TextDecorations.Underline;
                            return true;
                        }
                        if(string.Equals(s, "Strikethrough", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(s, "StrikeThrough", StringComparison.OrdinalIgnoreCase))
                        {
                            converted = TextDecorations.Strikethrough;
                            return true;
                        }
                        if(string.Equals(s, "OverLine", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(s, "Overline", StringComparison.OrdinalIgnoreCase))
                        {
                            converted = TextDecorations.OverLine;
                            return true;
                        }
                        if(string.Equals(s, "Baseline", StringComparison.OrdinalIgnoreCase))
                        {
                            converted = TextDecorations.Baseline;
                            return true;
                        }

                        // Попытка стандартного парсинга через TypeConverter
                        var conv = TypeDescriptor.GetConverter(typeof(TextDecorationCollection));
                        if(conv != null && conv.CanConvertFrom(typeof(string)))
                        {
                            converted = conv.ConvertFrom(null, CultureInfo.InvariantCulture, s);
                            return converted != null;
                        }
                    }
                    if(underlying.IsEnum)
                    {
                        converted = Enum.Parse(underlying, s, ignoreCase: true);
                        return true;
                    }
                }

                // Числа (FontSize и пр.)
                if(underlying == typeof(double) && value is IConvertible)
                {
                    converted = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    return true;
                }

                // Общий путь — TypeConverter целевого типа
                var tc = TypeDescriptor.GetConverter(underlying);
                if(tc != null && tc.CanConvertFrom(value.GetType()))
                {
                    converted = tc.ConvertFrom(null, CultureInfo.InvariantCulture, value);
                    return converted != null;
                }

                // Попробуем стандартную Convert.ChangeType (для простых типов)
                if(value is IConvertible)
                {
                    converted = Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
                    return true;
                }
            } catch
            {
                // падающие конвертации — просто возвращаем false
                converted = null;
                return false;
            }

            return false;
        }
        private bool ValuesEqualForText(object a, object b)
        {
            if(ReferenceEquals(a, b))
                return true;
            if(a == null || b == null)
                return false;

            // Brush: если обе SolidColorBrush — сравним по Color
            if(a is System.Windows.Media.Brush ba && b is System.Windows.Media.Brush bb)
            {
                if(ba is SolidColorBrush sa && bb is SolidColorBrush sb)
                    return sa.Color.Equals(sb.Color);
                // Иначе — откатываемся к Equals
                return Equals(ba, bb);
            }

            // FontFamily: сравним по Source (имя/путь)
            if(a is System.Windows.Media.FontFamily fa && b is System.Windows.Media.FontFamily fb)
                return string.Equals(fa.Source, fb.Source, StringComparison.OrdinalIgnoreCase);

            // TextDecorationCollection: сравнение по содержимому
            if(a is TextDecorationCollection ta && b is TextDecorationCollection tb)
            {
                if(ta.Count != tb.Count)
                    return false;
                // Для стандартных декораций — SequenceEqual нормально работает
                return ta.Cast<TextDecoration>().SequenceEqual(tb.Cast<TextDecoration>());
            }

            // double — учтём возможные мелкие расхождения
            if(a is double da && b is double db)
                return Math.Abs(da - db) < 0.0001;

            return Equals(a, b);
        }
        private bool CanMerge(Run a, Run b)
        {
            if(a == null || b == null)
                return false;
            if(!ReferenceEquals(a.Parent, b.Parent))
                return false;     // на всякий случай
            if(a.TextEffects != null || b.TextEffects != null)
            {
                if(!TextEffectsEqual(a.TextEffects, b.TextEffects))
                    return false;
            }

            // 1) Базовые шрифтовые и геометрические свойства
            if(!Equals(a.FontFamily, b.FontFamily))
                return false;
            if(!a.FontSize.Equals(b.FontSize))
                return false;
            if(!a.FontWeight.Equals(b.FontWeight))
                return false;
            if(!a.FontStyle.Equals(b.FontStyle))
                return false;
            if(!a.FontStretch.Equals(b.FontStretch))
                return false;
            if(!a.BaselineAlignment.Equals(b.BaselineAlignment))
                return false;

            // 2) Кисти
            if(!BrushesEqual(a.Foreground, b.Foreground))
                return false;
            if(!BrushesEqual(a.Background, b.Background))
                return false;

            // 3) Декорации текста
            if(!TextDecorationsEqual(a.TextDecorations, b.TextDecorations))
                return false;

            // 4) Язык и направление потока (могут влиять на рендеринг/лигауры)
            if(!Equals(a.Language, b.Language))
                return false;
            if(!Equals(a.FlowDirection, b.FlowDirection))
                return false;

            // 5) (Опционально) служебная метка
            if(!Equals(a.Tag, b.Tag))
                return false;

            return true;
        }
        /* ---------- Brushes ---------- */
        private bool BrushesEqual(System.Windows.Media.Brush a, System.Windows.Media.Brush b)
        {
            if(ReferenceEquals(a, b))
                return true;
            if(a is null || b is null)
                return a is null && b is null;

            if(!TransformsEqual(a.Transform, b.Transform))
                return false;
            if(!TransformsEqual(a.RelativeTransform, b.RelativeTransform))
                return false;
            if(!DoubleEquals(a.Opacity, b.Opacity))
                return false;

            if(a is SolidColorBrush sa && b is SolidColorBrush sb)
                return sa.Color.Equals(sb.Color);

            if(a is LinearGradientBrush la && b is LinearGradientBrush lb)
                return GradientBrushEqual(la, lb);

            if(a is RadialGradientBrush ra && b is RadialGradientBrush rb)
                return GradientBrushEqual(ra, rb);

            // Для остальных (ImageBrush/VisualBrush/…): лучшая эвристика — ReferenceEquals или Equals
            return a.Equals(b);
        }
        private bool GradientBrushEqual(GradientBrush a, GradientBrush b)
        {
            // Общие для всех градиентов
            if(!a.ColorInterpolationMode.Equals(b.ColorInterpolationMode))
                return false;
            if(!a.MappingMode.Equals(b.MappingMode))
                return false;
            if(!a.SpreadMethod.Equals(b.SpreadMethod))
                return false;
            if(!GradientStopsEqual(a.GradientStops, b.GradientStops))
                return false;

            // Специализация по типу
            if(a is LinearGradientBrush la && b is LinearGradientBrush lb)
            {
                // Только у Linear есть Start/End
                return PointEquals(la.StartPoint, lb.StartPoint)
                    && PointEquals(la.EndPoint, lb.EndPoint);
            }

            if(a is RadialGradientBrush ra && b is RadialGradientBrush rb)
            {
                // Только у Radial есть Center/GradientOrigin/RadiusX/RadiusY
                return PointEquals(ra.Center, rb.Center)
                    && PointEquals(ra.GradientOrigin, rb.GradientOrigin)
                    && DoubleEquals(ra.RadiusX, rb.RadiusX)
                    && DoubleEquals(ra.RadiusY, rb.RadiusY);
            }

            // Разные подклассы градиента — точно не равны визуально
            return false;
        }
        private bool GradientStopsEqual(GradientStopCollection a, GradientStopCollection b)
        {
            if(a == null || b == null)
                return a == null && b == null;
            if(a.Count != b.Count)
                return false;

            // Порядок стопов важен для результата, сравниваем по индексу
            for(int i = 0; i < a.Count; i++)
            {
                if(!a[i].Color.Equals(b[i].Color))
                    return false;
                if(!DoubleEquals(a[i].Offset, b[i].Offset))
                    return false;
            }
            return true;
        }
        private bool TransformsEqual(Transform a, Transform b)
        {
            if(ReferenceEquals(a, b))
                return true;
            if(a is null || b is null)
                return a is null && b is null;
            // сравним итоговые матрицы
            var ma = a.Value;
            var mb = b.Value;
            return DoubleEquals(ma.M11, mb.M11) && DoubleEquals(ma.M12, mb.M12) &&
                   DoubleEquals(ma.M21, mb.M21) && DoubleEquals(ma.M22, mb.M22) &&
                   DoubleEquals(ma.OffsetX, mb.OffsetX) && DoubleEquals(ma.OffsetY, mb.OffsetY);
        }
        private bool PointEquals(Point a, Point b) => DoubleEquals(a.X, b.X) && DoubleEquals(a.Y, b.Y);
        /* ---------- TextDecorations ---------- */
        private bool TextDecorationsEqual(TextDecorationCollection a, TextDecorationCollection b)
        {
            // Treat null and empty as equal
            if(a == null || a.Count == 0)
                return b == null || b.Count == 0;
            if(b == null || b.Count == 0)
                return false;
            if(a.Count != b.Count)
                return false;

            // Сравним «мультимножества»: сортируем по Location и PenThicknessUnit
            var aa = a.Select(Norm).OrderBy(k => k.Location).ThenBy(k => k.Unit).ToArray();
            var bb = b.Select(Norm).OrderBy(k => k.Location).ThenBy(k => k.Unit).ToArray();

            for(int i = 0; i < aa.Length; i++)
            {
                if(aa[i].Location != bb[i].Location)
                    return false;
                if(aa[i].Unit != bb[i].Unit)
                    return false;
                if(!PensEqual(aa[i].Pen, bb[i].Pen))
                    return false;
            }
            return true;

            static (TextDecorationLocation Location, TextDecorationUnit Unit, Pen Pen) Norm(TextDecoration d)
                => (d.Location, d.PenThicknessUnit, d.Pen);
        }
        private bool PensEqual(Pen a, Pen b)
        {
            if(ReferenceEquals(a, b))
                return true;
            if(a is null || b is null)
                return a is null && b is null;

            if(!DoubleEquals(a.Thickness, b.Thickness))
                return false;
            if(!BrushesEqual(a.Brush, b.Brush))
                return false;
            if(!Equals(a.StartLineCap, b.StartLineCap))
                return false;
            if(!Equals(a.EndLineCap, b.EndLineCap))
                return false;
            if(!Equals(a.DashCap, b.DashCap))
                return false;
            if(!Equals(a.LineJoin, b.LineJoin))
                return false;

            // Шаблон штриховки
            var ad = a.DashStyle;
            var bd = b.DashStyle;
            if(ad == null || bd == null)
                return ad == null && bd == null;
            if(!DoubleEquals(ad.Offset, bd.Offset))
                return false;

            var ac = ad.Dashes;
            var bc = bd.Dashes;
            if(ac == null || bc == null)
                return ac == null && bc == null;
            if(ac.Count != bc.Count)
                return false;
            for(int i = 0; i < ac.Count; i++)
                if(!DoubleEquals(ac[i], bc[i]))
                    return false;

            return true;
        }
        /* ---------- TextEffects ---------- */
        private bool TextEffectsEqual(TextEffectCollection a, TextEffectCollection b)
        {
            // null и пустая коллекция считаем эквивалентными
            if(a == null || a.Count == 0)
                return b == null || b.Count == 0;
            if(b == null || b.Count == 0)
                return false;
            if(a.Count != b.Count)
                return false;

            // Порядок эффектов влияет на результат — сравниваем по индексу
            for(int i = 0; i < a.Count; i++)
            {
                var x = a[i];
                var y = b[i];

                if(x.PositionStart != y.PositionStart)
                    return false;
                if(x.PositionCount != y.PositionCount)
                    return false;

                if(!BrushesEqual(x.Foreground, y.Foreground))
                    return false;
                if(!TransformsEqual(x.Transform, y.Transform))
                    return false;
                if(!GeometriesEqual(x.Clip, y.Clip))
                    return false;
            }
            return true;
        }
        private bool GeometriesEqual(Geometry a, Geometry b)
        {
            if(ReferenceEquals(a, b))
                return true;
            if(a is null || b is null)
                return a is null && b is null;
            // Геометрии сравнить строго трудно; хорошая эвристика — сравнить «плоские» PathGeometry
            var ga = a.GetFlattenedPathGeometry();
            var gb = b.GetFlattenedPathGeometry();
            return ga.ToString(CultureInfo.InvariantCulture) == gb.ToString(CultureInfo.InvariantCulture);
        }
        /* ---------- числовая погрешность ---------- */
        private bool DoubleEquals(double a, double b, double eps = 1e-9) => Math.Abs(a - b) <= eps;


        #region Type lookup

        private readonly ConcurrentDictionary<string, Type?> _typeCache =
            new(StringComparer.Ordinal);

        // Находит тип по короткому имени или по полному (c namespace или AssemblyQualifiedName).
        // Приоритет: Entry/Executing/Calling assembly, затем остальные загруженные сборки.
        // Кэширует результат. Возвращает null, если тип не найден.
        private Type? FindTypeByName(string typeName)
        {
            if(string.IsNullOrWhiteSpace(typeName))
                return null;

            // Кэш
            if(_typeCache.TryGetValue(typeName, out var cached))
                return cached;

            // 1) Если пришло полное имя с asm-частью — пробуем сразу
            //    (Type.GetType понимает AssemblyQualifiedName).
            if(typeName.IndexOf(',') >= 0 || typeName.Contains('.'))
            {
                var t = Type.GetType(typeName, throwOnError: false, ignoreCase: true);
                if(t != null)
                {
                    _typeCache[typeName] = t;
                    return t;
                }
            }

            // Список сборок с приоритетом на "пользовательские"
            var loaded = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a =>
                {
                    try
                    { return !a.IsDynamic; } catch { return false; }
                })
                .ToList();

            // Слегка эвристически сортируем: сначала Entry/Executing/Calling,
            // затем сборки без "Microsoft"/"System".
            Assembly? entry = null, exec = null, calling = null;
            try
            { entry = Assembly.GetEntryAssembly(); } catch { }
            try
            { exec = Assembly.GetExecutingAssembly(); } catch { }
            try
            { calling = Assembly.GetCallingAssembly(); } catch { }

            loaded = loaded
                .OrderByDescending(a => a == entry || a == exec || a == calling)
                .ThenByDescending(a =>
                {
                    var n = a.GetName().Name ?? "";
                    return !(n.StartsWith("System", StringComparison.OrdinalIgnoreCase)
                             || n.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase)
                             || n.StartsWith("Windows", StringComparison.OrdinalIgnoreCase)
                             || n.StartsWith("Presentation", StringComparison.OrdinalIgnoreCase)
                             || n.StartsWith("PresentationCore", StringComparison.OrdinalIgnoreCase)
                             || n.StartsWith("PresentationFramework", StringComparison.OrdinalIgnoreCase)
                             || n.StartsWith("WindowsBase", StringComparison.OrdinalIgnoreCase));
                })
                .ToList();

            // 2) Пробуем точное совпадение по Name (без namespace)
            foreach(var asm in loaded)
            {
                Type? t = null;
                try
                {
                    t = asm.GetTypes().FirstOrDefault(x =>
                        x.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));
                } catch { /* некоторые сборки могут бросить */ }

                if(t != null)
                {
                    _typeCache[typeName] = t;
                    return t;
                }
            }

            // 3) Пробуем совпадение по окончанию FullName: ".InlineStyle"
            foreach(var asm in loaded)
            {
                Type? t = null;
                try
                {
                    t = asm.GetTypes().FirstOrDefault(x =>
                        (x.FullName ?? "").EndsWith("." + typeName, StringComparison.OrdinalIgnoreCase));
                } catch { }

                if(t != null)
                {
                    _typeCache[typeName] = t;
                    return t;
                }
            }

            _typeCache[typeName] = null;
            return null;
        }

        #endregion

        #region Safe property set

        // Надёжная установка свойства по имени со множеством fallback-конвертаций.
        // Тихо игнорирует несовместимые типы и DependencyProperty.UnsetValue.
        private void TrySet(object target, string propName, object value)
        {
            if(target == null || string.IsNullOrWhiteSpace(propName))
                return;

            // UnsetValue/Null — нечего устанавливать
            if(value == null || ReferenceEquals(value, DependencyProperty.UnsetValue))
                return;

            var t = target.GetType();
            var p = t.GetProperty(propName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

            if(p == null || !p.CanWrite)
                return;

            var destType = p.PropertyType;
            var (underlying, isNullable) = UnwrapNullable(destType);

            try
            {
                // 1) Если уже совместимо по типу — ставим напрямую
                if(underlying.IsInstanceOfType(value))
                {
                    p.SetValue(target, PrepareAssignable(value, underlying));
                    return;
                }

                // 2) Enum: строка/число -> enum
                if(underlying.IsEnum)
                {
                    object? enumVal = TryConvertToEnum(underlying, value);
                    if(enumVal != null)
                    {
                        p.SetValue(target, enumVal);
                        return;
                    }
                }

                // 3) String -> тип (через TypeConverter целевого типа)
                if(value is string s)
                {
                    var conv = TypeDescriptor.GetConverter(underlying);
                    if(conv is not null && conv.CanConvertFrom(typeof(string)))
                    {
                        var converted = conv.ConvertFrom(null, CultureInfo.CurrentCulture, s);
                        if(converted != null)
                        {
                            p.SetValue(target, PrepareAssignable(converted, underlying));
                            return;
                        }
                    }
                }

                // 4) Конвертер целевого типа из исходного
                {
                    var conv = TypeDescriptor.GetConverter(underlying);
                    if(conv is not null && conv.CanConvertFrom(value.GetType()))
                    {
                        var converted = conv.ConvertFrom(null, CultureInfo.CurrentCulture, value);
                        if(converted != null)
                        {
                            p.SetValue(target, PrepareAssignable(converted, underlying));
                            return;
                        }
                    }
                }

                // 5) Конвертер исходного типа в целевой
                {
                    var conv = TypeDescriptor.GetConverter(value);
                    if(conv is not null && conv.CanConvertTo(underlying))
                    {
                        var converted = conv.ConvertTo(null, CultureInfo.CurrentCulture, value, underlying);
                        if(converted != null)
                        {
                            p.SetValue(target, PrepareAssignable(converted, underlying));
                            return;
                        }
                    }
                }

                // 6) IConvertible -> ChangeType
                if(value is IConvertible)
                {
                    var converted = Convert.ChangeType(value, underlying, CultureInfo.CurrentCulture);
                    p.SetValue(target, PrepareAssignable(converted, underlying));
                    return;
                }

                // 7) Brush/Freezable и др.: последняя попытка — если тип совпадает по AssignableFrom
                if(underlying.IsAssignableFrom(value.GetType()))
                {
                    p.SetValue(target, PrepareAssignable(value, underlying));
                    return;
                }
            } catch
            {
                // Глушим — «надежность» = отсутствие падений при странных значениях
            }
            // Если сюда дошли — установить не удалось; тихо игнорируем.
        }

        // Если значение — Freezable (Brush, TextDecorationCollection, и т.п.), возвращаем «безопасную» копию.
        // Иначе — исходное значение.
        private object PrepareAssignable(object value, Type targetType)
        {
            // Freezable-клонирование: избегаем попыток присвоить "замороженный" объект,
            // к которому потом кто-то попробует писать.
            if(value is Freezable fz)
            {
                try
                {
                    // Если заморожен — клонируем; иначе достаточно CloneCurrentValue
                    return fz.IsFrozen ? fz.Clone() : fz.CloneCurrentValue();
                } catch
                {
                    // На всякий случай — отдадим оригинал
                    return value;
                }
            }

            // Некоторые типы в WPF неизменяемые (FontFamily, Typeface, etc.) — возвращаем как есть.
            return value;
        }

        private (Type underlying, bool isNullable) UnwrapNullable(Type type)
        {
            var u = Nullable.GetUnderlyingType(type);
            return (u ?? type, u != null);
        }

        private object? TryConvertToEnum(Type enumType, object value)
        {
            try
            {
                if(value is string s)
                {
                    if(Enum.TryParse(enumType, s, ignoreCase: true, out var parsed))
                        return parsed;
                    return null;
                }

                if(value is IConvertible)
                {
                    var num = Convert.ChangeType(value, Enum.GetUnderlyingType(enumType), CultureInfo.InvariantCulture);
                    return Enum.ToObject(enumType, num!);
                }
            } catch { }
            return null;
        }

        #endregion

    }
}
