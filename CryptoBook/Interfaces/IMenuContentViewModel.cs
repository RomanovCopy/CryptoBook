using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IMenuContentViewModel:IViewModel
    {
        public ICommand Reading { get; }
        public ICommand InsertImage { get; }
        public ICommand InsertText { get; }
        public ICommand OpenDocumentTree { get; }
        public ICommand MediaPlayer { get; }

    }
}
