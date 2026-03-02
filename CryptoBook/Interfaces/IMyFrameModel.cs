using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IMyFrameModel:INotifyPropertyChanged, IModel
    {
        string CurrentPageKey { get; }

        bool CanExecute_FrameListAddPage(object? obj);
        void Execute_FrameListAddPage(object? obj);
        bool CanExecute_FramelistGoForward(object? obj);
        void Execute_FramelistGoForward(object? obj);
        bool CanExecute_FramelistGoBack(object? obj);
        void Execute_FramelistGoBack(object? obj);
        bool CanExecute_FrameListRemovePage(object? obj);
        void Execute_FrameListRemovePage(object? obj);   
    }
}
