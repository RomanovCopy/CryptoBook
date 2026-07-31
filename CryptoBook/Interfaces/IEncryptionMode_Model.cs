using CryptoBook.Security;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IEncryptionMode_Model: IModel,IWindowOptions, IWindowWithId
    {
        public string Title { get; }
        public string MessageMode { get; }
        public string MessageModeTop { get; }
        public string MessageModeBottom { get; }
        public string Path { get; }
        public string WarningMessage { get; }
        public EncryptionTargetMode SelectedMode{ get; set;}
        public ISystemItem ProcessedItem {  get; }

        public bool CanExecute_ButtonOk(object? obj);
        public void Execute_ButtonOk(object? obj);

        public bool CanExecute_ButtonCancel(object? obj);
        public void Execute_ButtonCancel(object? obj);
    }
}
