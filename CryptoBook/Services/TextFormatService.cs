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
            AdjustLineHeight(lineHeight);
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


        /// <summary>
        /// Регулирует LineHeight у абзацев: -1 уменьшить, 0 без изменений, +1 увеличить.
        /// Если нет выделения — применяется к текущему Paragraph (у каретки).
        /// Если выделены несколько Paragraph — применяется ко всем в диапазоне.
        /// </summary>
        /// <param name="service">RichTextBox</param>
        /// <param name="direction">-1, 0, +1</param>
        /// <param name="stepFactor">Шаг как доля FontSize (по умолчанию 0.1 → 10%)</param>
        /// <param name="minFactor">Нижняя граница как доля FontSize (по умолчанию 0.5)</param>
        /// <param name="maxFactor">Верхняя граница как доля FontSize (по умолчанию 2.0)</param>
        private void AdjustLineHeight( double direction, double stepFactor = 0.10, double minFactor = 0.50, double maxFactor = 2.00)
        {
            if(service == null)
                return;
            if(direction != -1 && direction != 0 && direction != 1)
                direction = 0;
            if(direction == 0)
                return; // ничего не меняем

            // Определяем целевые параграфы: либо у каретки, либо все в выделении
            var targets = GetTargetParagraphs();
            if(targets.Count == 0)
                return;

            foreach(var p in targets)
            {
                if(p == null)
                    continue;

                // Текущий размер шрифта абзаца
                double fontSize = GetEffectiveFontSize(p);
                if(fontSize <= 0)
                    fontSize = 12.0; // безопасное значение по умолчанию

                // Текущий lineHeight; если неопределён — возьмём «эффективный» старт от FontSize
                double current = (double.IsNaN(p.LineHeight) || p.LineHeight <= 0)
                                 ? fontSize * 1.0 // базовая отправная точка — 1.0×FontSize
                                 : p.LineHeight;

                // Шаг и границы
                double stepPx = Math.Max(1.0, fontSize * stepFactor);
                double minPx = Math.Max(1.0, fontSize * minFactor);
                double maxPx = Math.Max(minPx + 1.0, fontSize * maxFactor);

                double updated = current + direction * stepPx;
                updated = Math.Max(minPx, Math.Min(maxPx, updated));

                p.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
                p.LineHeight = updated;

                // При желании можно убрать межабзацные поля (раскомментируйте):
                // p.Margin = new Thickness(0);
            }
        }

        /// <summary>
        /// Возвращает Paragraph для каретки либо все Paragraph внутри выделения.
        /// </summary>
        private List<Paragraph> GetTargetParagraphs()
        {
            var result = new List<Paragraph>();

            // Если выделение пустое — текущий параграф
            if(service.Selection == null || service.Selection.IsEmpty)
            {
                var p = service.CaretPosition?.Paragraph;
                if(p != null)
                    result.Add(p);
                return result;
            }

            // Иначе — все параграфы в диапазоне выделения
            var startP = service.Selection.Start?.Paragraph;
            var endP = service.Selection.End?.Paragraph;

            if(startP == null && endP == null)
                return result;

            if(startP == null && endP != null)
                startP = endP;
            if(endP == null && startP != null)
                endP = startP;

            var pIt = startP;
            while(pIt != null)
            {
                result.Add(pIt);
                if(ReferenceEquals(pIt, endP))
                    break;
                pIt = pIt.NextBlock as Paragraph;
            }

            return result;
        }

        /// <summary>
        /// Аккуратно берём FontSize абзаца; если не задан — пробуем у документа/RTB.
        /// </summary>
        private double GetEffectiveFontSize(Paragraph p)
        {
            object val = p.GetValue(TextElement.FontSizeProperty);
            if(val is double d && d > 0)
                return d;

            // Пробуем взять у документа
            if(p.Parent is FlowDocument doc && doc.FontSize > 0)
                return doc.FontSize;

            // Или у самого RichTextBox
            if(service.Service.FontSize > 0)
                return service.Service.FontSize;

            return 12.0;
        }
    }

}
