using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IMenuSettingsViewModel:IViewModel
    {
        public ICommand SetFontWeight { get; }
        public ICommand SetFontFamily { get; }
        public ICommand SetFontSize { get; }
        public ICommand SetFontColor { get; }
        public ICommand SetFontBackColor { get; }
        public ICommand SetPaperColor { get; }
        public ICommand SetEncoding { get; }
        public ICommand Localization { get; }

    }
}
