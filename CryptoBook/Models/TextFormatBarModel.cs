using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

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
            if(obj is not TextAlignment)return false;
            return !service.Selection.IsEmpty;
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
            return true;
        }
       internal void Execute_SetParagraphIndent(object? obj)
        {
            if(obj is string str && double.Parse(str)>0)
            {
                textFormatService.SetParagraphIndent(double.Parse(str));
            }
        }

        internal bool CanExecute_SetLineHeight(object? obj) => obj is double;
        internal void Execute_SetLineHeight(object? obj)
        {
            if(obj is double d)
                textFormatService.SetLineHeight(d);
        }

    }
}
