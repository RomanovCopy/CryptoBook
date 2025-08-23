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
            if(options == null)
                throw new ArgumentNullException(nameof(options));

            var caret = EnsureInsertionPosition(service.CaretPosition)
                        ?? throw new InvalidOperationException("Caret is null.");

            var run = new Run(options.Text ?? string.Empty);

            if(options.Style != null)
                ApplyStyle(run, options.Style);

            options.Configure?.Invoke(run);

            InsertInlineAt(caret, run);

            if(options.MoveCaretAfterInsert)
                service.CaretPosition = run.ElementEnd;

            return run;
        }
        public Run InsertRunAtCaret(string text, InlineStyle? style = null, Action<Run>? configure = null, bool moveCaret = true)
        {
            var caret = EnsureInsertionPosition(service.CaretPosition)
                        ?? throw new InvalidOperationException("Caret is null.");

            var run = new Run(text ?? string.Empty);
            if(style != null)
                ApplyStyle(run, style);
            configure?.Invoke(run);

            InsertInlineAt(caret, run);

            if(moveCaret)
                service.CaretPosition = run.ElementEnd;
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
                ApplyStyle(link, style);
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
            var sel = service.Selection ?? throw new InvalidOperationException("Selection is null.");

            using(BeginChangeScope())
            {
                if(!sel.IsEmpty)
                    sel.Text = string.Empty;

                var run = new Run(text ?? string.Empty);
                if(style != null)
                    ApplyStyle(run, style);
                configure?.Invoke(run);

                InsertInlineAt(EnsureInsertionPosition(sel.Start), run);
                service.CaretPosition = run.ElementEnd;
                return run;
            }
        }


        public Span WrapSelectionInSpan(InlineStyle? spanStyle = null, Action<Span>? configure = null)
        {
            var sel = service.Selection ?? throw new InvalidOperationException("Selection is null.");

            using(BeginChangeScope())
            {
                if(sel.IsEmpty)
                {
                    // пустое выделение — вставим пустой Span в каретку
                    var span = new Span();
                    if(spanStyle != null)
                        ApplyStyle(span, spanStyle);
                    configure?.Invoke(span);

                    InsertInlineAt(EnsureInsertionPosition(service.CaretPosition), span);
                    service.CaretPosition = span.ContentEnd;
                    return span;
                }

                // WPF: Span(start,end) оборачивает существующие инлайны без копирования
                var s = new Span(sel.Start, sel.End);
                if(spanStyle != null)
                    ApplyStyle(s, spanStyle);
                configure?.Invoke(s);

                service.CaretPosition = s.ContentEnd;
                return s;
            }
        }


        public void ApplyStyle(Inline inline, InlineStyle style, bool overwriteNullsOnly = false)
        {
            if(inline is null || style is null)
                return;

            foreach(var (dp, value) in style)
            {
                var v = PrepareForAssign(value);
                if(v is null)
                    continue;

                if(overwriteNullsOnly)
                {
                    var current = inline.GetValue(dp);
                    if(current != null && !ReferenceEquals(current, DependencyProperty.UnsetValue))
                        continue;
                }
                inline.SetValue(dp, v);
            }
        }

        public void ApplyStyleToSelection(InlineStyle style)
        {
            var sel = service.Selection;
            if(sel is null || style is null)
                return;

            using(BeginChangeScope())
            {
                foreach(var (dp, value) in style)
                {
                    sel.ApplyPropertyValue(dp, PrepareForAssign(value) ?? DependencyProperty.UnsetValue);
                }
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

            object? Read(DependencyProperty dp)
            {
                var v = tr.GetPropertyValue(dp);
                return ReferenceEquals(v, DependencyProperty.UnsetValue) ? null : v;
            }

            return new InlineStyle
            {
                [Inline.FontFamilyProperty] = Read(Inline.FontFamilyProperty),
                [Inline.FontSizeProperty] = Read(Inline.FontSizeProperty),
                [Inline.FontWeightProperty] = Read(Inline.FontWeightProperty),
                [Inline.FontStyleProperty] = Read(Inline.FontStyleProperty),
                [TextElement.ForegroundProperty] = Read(TextElement.ForegroundProperty),
                [TextElement.BackgroundProperty] = Read(TextElement.BackgroundProperty),
                [Inline.TextDecorationsProperty] = Read(Inline.TextDecorationsProperty),
                [TextElement.FontStretchProperty] = Read(TextElement.FontStretchProperty),
                [Inline.BaselineAlignmentProperty] = Read(Inline.BaselineAlignmentProperty),
            };
        }



        private object? PrepareForAssign(object? value)
        {
            if(value == null || ReferenceEquals(value, DependencyProperty.UnsetValue))
                return null;

            if(value is Freezable fz)
            {
                try
                { return fz.IsFrozen ? fz.Clone() : fz.CloneCurrentValue(); } catch { return value; }
            }

            return value;
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
        public void CopyStyleProp( InlineStyle style, DependencyProperty dp, Action<object> applyValue, 
            bool overwriteNullsOnly, object? currentValue)
        {
            if(style is null || dp is null || applyValue is null)
                return;

            if(!style.TryGetValue(dp, out var value) || value is null ||
                ReferenceEquals(value, DependencyProperty.UnsetValue))
                return;

            if(overwriteNullsOnly &&
                currentValue is not null &&
                !ReferenceEquals(currentValue, DependencyProperty.UnsetValue))
                return;

            var v = PrepareForAssign(value); // ваш хелпер (клонирует Freezable и т.п.)
            if(v is not null)
                applyValue(v);
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


    }
}
