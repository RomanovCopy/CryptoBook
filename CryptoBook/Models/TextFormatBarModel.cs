using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Models
{
    internal class TextFormatBarModel:ViewModelBase
    {
        private readonly IRichTextBoxService service;

        public TextFormatBarModel(IRichTextBoxService richTextBoxService)
        {
            service = richTextBoxService ?? throw new ArgumentNullException(nameof(richTextBoxService));
        }

        internal bool CanExecute_SetTextAlignment(object? obj)
        {
            return true;
        }

        internal void Execute_SetTextAlignment(object? obj)
        {
            
        }
    }
}
