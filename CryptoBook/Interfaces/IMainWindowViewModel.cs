using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IMainWindowViewModel:IViewModel
    {
        public ICommand FrameListAddPage();

        public ICommand FrameListRemovePage();

        public ICommand Frmelist_GoForward();

        public ICommand Frmelist_GoBack();

        public ICommand PageClosed();

    }
}
