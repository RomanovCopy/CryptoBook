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
        public ICommand FrameListAddPage { get; }

        public ICommand FrameListRemovePage { get; }

        public ICommand FramelistGoForward { get; }

        public ICommand FramelistGoBack { get; }

        public ICommand PageClosed { get; }

    }
}
