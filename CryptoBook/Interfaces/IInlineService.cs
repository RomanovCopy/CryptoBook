using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

namespace CryptoBook.Interfaces
{

    public interface IInlineService
    {
        // --- Управление транзакциями изменений (Undo-группа, оптимизация перерисовки) ---
        IInlineChangeScope BeginChangeScope();

        // --- Вставка ---
        /// <summary>Вставить новый Run в позицию каретки. При наличии выделения — заменить его.</summary>
        Run InsertRunAtCaret(RunInsertOptions options);

        /// <summary>Вставить Inline в заданную позицию (между символами). Если позиция внутри Run — корректно распилит.</summary>
        Inline InsertInlineAt(TextPointer position, Inline inline);

        /// <summary>Вставить перенос строки (LineBreak) в позицию каретки.</summary>
        LineBreak InsertLineBreakAtCaret();

        /// <summary>Создать и вставить Hyperlink в позицию каретки.</summary>
        Hyperlink InsertHyperlinkAtCaret(string text, Uri? navigateUri, InlineStyle? style = null, Action<Hyperlink>? configure = null);

        /// <summary>Вставить InlineUIContainer (например, кнопку/иконку) в позицию каретки.</summary>
        InlineUIContainer InsertInlineUIElementAtCaret(UIElement element, Action<InlineUIContainer>? configure = null);

        // --- Замена/обёртка выделения ---
        /// <summary>Заменить выделение текстом (Run) с указанным стилем. Если выделения нет — вставить в каретку.</summary>
        Run ReplaceSelection(string text, InlineStyle? style = null, Action<Run>? configure = null);

        /// <summary>Обёрнуть текущее выделение в Span, применив стиль к Span (наследуется вниз).</summary>
        Span WrapSelectionInSpan(InlineStyle? spanStyle = null, Action<Span>? configure = null);

        // --- Модификация существующих Inline ---
        /// <summary>Применить стиль к указанному Inline (точечно, без влияния на соседей).</summary>
        void ApplyStyle(Inline inline, InlineStyle style, bool overwriteNullsOnly = false);

        /// <summary>Применить стиль к текущему выделению. При необходимости бьёт Run-ы на сегменты.</summary>
        void ApplyStyleToSelection(InlineStyle style);

        /// <summary>Переключатели для часто используемых атрибутов.</summary>
        void ToggleBoldOnSelection();
        void ToggleItalicOnSelection();
        void ToggleUnderlineOnSelection();

        // --- Низкоуровневые операции с Run ---
        /// <summary>Распилить Run в заданной позиции. Возвращает левую/правую части (вставка не выполняется).</summary>
        (Run? left, Run? right) SplitRunAt(TextPointer position);

        /// <summary>Слить смежные Run с совпадающим форматированием (в пределах параграфа).</summary>
        int MergeAdjacentRuns(Paragraph paragraph);

        /// <summary>Нормализовать инлайны параграфа: удалить пустые Run, слить совместимые, выровнять артефакты.</summary>
        void NormalizeParagraphInlines(Paragraph paragraph);

        // --- Вспомогательные ---
        /// <summary>Получить Inline под кареткой (если каретка внутри текста — вернёт Run).</summary>
        Inline? GetInlineAtCaret();

        /// <summary>Получить актуальное форматирование в позиции каретки (наследованное/эффективное).</summary>
        InlineStyle GetEffectiveStyleAtCaret();

        void CopyStyleProp(object style, string propName, Action<object> applyValue, bool overwriteNullsOnly,
            object? currentValue);
    }


    /// <summary>Набор стилистических опций для вставляемых/изменяемых Inline.</summary>
    public sealed record InlineStyle(
        FontFamily? FontFamily = null,
        double? FontSize = null,
        FontWeight? FontWeight = null,
        System.Windows.FontStyle? FontStyle = null,
        Brush? Foreground = null,
        Brush? Background = null,
        TextDecorationCollection? TextDecorations = null,
        BaselineAlignment? Baseline = null);

    /// <summary>Параметры вставки Run.</summary>
    public sealed record RunInsertOptions(
        string Text,
        InlineStyle? Style = null,
        Action<Run>? Configure = null);
}
