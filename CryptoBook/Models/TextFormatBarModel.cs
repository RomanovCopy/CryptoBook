using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Globalization;
using System.Windows;

namespace CryptoBook.Models
{
    internal class TextFormatBarModel:ViewModelBase
    {
        private readonly IRichTextBoxService service;
        private readonly ITextFormatService textFormatService;

        public TextFormatBarModel(IRichTextBoxService richTextBoxService,ITextFormatService formatService)
        {
            service = richTextBoxService ?? throw new ArgumentNullException(nameof(richTextBoxService));
            textFormatService = formatService ?? throw new ArgumentNullException(nameof(formatService));
        }

        //форматирование выделеного текста
        internal bool CanExecute_SetTextAlignment(object? obj)
        {
            return obj is TextAlignment;
        }
        internal void Execute_SetTextAlignment(object? obj)
        {
            if (obj is TextAlignment alignment)
            {
                textFormatService.SetTextAlignment(alignment);
            }
        }

        //создание нового параграфа с заданным отступом от начала строки
        internal bool CanExecute_SetParagraphIndent(object? obj)
        {
            return TryGetDouble(obj, out var indent) && indent >= 0;
        }
       internal void Execute_SetParagraphIndent(object? obj)
        {
            if(TryGetDouble(obj, out var indent) && indent >= 0)
                textFormatService.SetParagraphIndent(indent);
        }

        internal bool CanExecute_SetLineHeight(object? obj) => obj is double;
        internal void Execute_SetLineHeight(object? obj)
        {
            if(obj is double d)
                textFormatService.SetLineHeight(d);
        }

        private static bool TryGetDouble(object? value, out double result)
        {
            if(value is double number)
            {
                result = number;
                return !double.IsNaN(result) && !double.IsInfinity(result);
            }

            return double.TryParse(
                value as string,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out result);
        }

    }
}
