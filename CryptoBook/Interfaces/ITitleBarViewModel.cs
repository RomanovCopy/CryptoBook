using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface ITitleBarViewModel:IViewModel
    {

        public ICommand TitleBarMouseLeftButtonDown { get; }
        public ICommand TitleBarMouseMove { get; }
        public ICommand ButtonBack_Click { get; }
        public ICommand ButtonForward_Click { get; }
        public ICommand ToggleMenu_Click { get; }
        public ICommand MinButtonClick { get; }
        public ICommand MaxButtonClick { get; }
        public ICommand CloseButtonClick { get; }

    }
}
