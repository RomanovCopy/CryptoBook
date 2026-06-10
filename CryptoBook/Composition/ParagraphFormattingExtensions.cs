using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

namespace CryptoBook.Composition
{
    public static class ParagraphFormattingExtensions
    {

        private static readonly DependencyProperty[] ParaProps =
        {
            Block.MarginProperty,
            Paragraph.TextAlignmentProperty,
            Paragraph.TextIndentProperty,
            Paragraph.LineHeightProperty,
            Paragraph.LineStackingStrategyProperty,
            Paragraph.KeepTogetherProperty,
            Paragraph.KeepWithNextProperty,
            Paragraph.BreakPageBeforeProperty,

            TextElement.BackgroundProperty,
            TextElement.ForegroundProperty,
            TextElement.FontFamilyProperty,
            TextElement.FontSizeProperty,
            TextElement.FontStyleProperty,
            TextElement.FontWeightProperty,
            TextElement.FontStretchProperty,
            TextElement.TextEffectsProperty,
        };

        public static void CopyFormattingFrom(this Paragraph target, Paragraph source, bool copyOnlyLocal)
        {
            foreach(var dp in ParaProps)
            {
                var local = source.ReadLocalValue(dp);
                if(!copyOnlyLocal || local != DependencyProperty.UnsetValue)
                {
                    var value = local != DependencyProperty.UnsetValue ? local : source.GetValue(dp);
                    target.SetValue(dp, value);
                }
            }

            // Typography (добавьте нужные при необходимости)
            CopyTypography(target, source, copyOnlyLocal);

            // TextDecorations (часто наследуется, но поддержим копирование)
            var tdLocal = source.ReadLocalValue(Inline.TextDecorationsProperty);
            if(!copyOnlyLocal || tdLocal != DependencyProperty.UnsetValue)
                target.TextDecorations = source.TextDecorations;
        }

        private static void CopyTypography(Paragraph target, Paragraph source, bool copyOnlyLocal)
        {
            CopyTypo(target, source, Typography.StylisticSet1Property, copyOnlyLocal);
            CopyTypo(target, source, Typography.DiscretionaryLigaturesProperty, copyOnlyLocal);
            CopyTypo(target, source, Typography.NumeralAlignmentProperty, copyOnlyLocal);
            CopyTypo(target, source, Typography.NumeralStyleProperty, copyOnlyLocal);
        }

        private static void CopyTypo(Paragraph target, Paragraph source, DependencyProperty dp, bool copyOnlyLocal)
        {
            var local = source.ReadLocalValue(dp);
            if(!copyOnlyLocal || local != DependencyProperty.UnsetValue)
            {
                var value = local != DependencyProperty.UnsetValue ? local : source.GetValue(dp);
                target.SetValue(dp, value);
            }
        }

    }
}
