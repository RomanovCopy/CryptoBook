using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface ITextFormatBarViewModel:IViewModel
    {
        public ICommand SetTextAlignment { get; }
        public ICommand SetParagraphIndent { get; }
        public ICommand SetLineHeight { get; }
    }
}
