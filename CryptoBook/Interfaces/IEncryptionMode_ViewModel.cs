using CryptoBook.Security;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IEncryptionMode_ViewModel:IViewModel, IWindowOptions, IWindowWithId
    {
        public string Title { get; }
        public string MessageMode { get; }
        public string MessageModeTop { get; }
        public string MessageModeBottom { get; }
        public string Path { get; }
        public string WarningMessage { get; }
        public EncryptionTargetMode SelectedMode { get; set; }
        public ISystemItem ProcessedItem { get; }

        public ICommand ButtonOk{ get; }
        public ICommand ButtonCancel{ get; }
    }
}
